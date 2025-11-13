using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace WhisperTranslator.Web.Services
{
    public class TtsService
    {
        private readonly string _key;
        private readonly string _region;
        private readonly string _defaultVoice;
        private readonly IHttpClientFactory _http;

        public TtsService(IConfiguration cfg, IHttpClientFactory http)
        {
            _key = cfg["Speech:Key"] ?? throw new InvalidOperationException("Missing Speech:Key");
            _region = cfg["Speech:Region"] ?? throw new InvalidOperationException("Missing Speech:Region");
            _defaultVoice = cfg["Speech:Voice"] ?? "en-GB-SoniaNeural";
            _http = http;
        }

        /// <summary>
        /// Speak text, picking a voice based on localeHint when provided.
        /// Returns (bytes, contentType, source, errorMessageOrNull).
        /// </summary>
        public async Task<(byte[] Data, string ContentType, string Source, string? Error)> SpeakAsync(
            string text, string? localeHint = null, CancellationToken ct = default)
        {
            text = NormaliseForSsml(text);
            var (voiceLocale, voiceName) = PickVoice(localeHint);

            string? lastRestError = null;
            string? lastSdkError = null;

            // 1) REST → MP3
            try
            {
                var mp3 = await SpeakViaRestAsync(text, voiceLocale, voiceName, ct).ConfigureAwait(false);
                if (mp3.Length > 0) return (mp3, "audio/mpeg", "rest", null);
            }
            catch (Exception ex)
            {
                lastRestError = ex.Message;
            }

            // 2) SDK → MP3
            try
            {
                var mp3 = await SpeakViaSdkAsync(text, voiceName, ct).ConfigureAwait(false);
                if (mp3.Length > 0) return (mp3, "audio/mpeg", "sdk", lastRestError);
            }
            catch (Exception ex)
            {
                lastSdkError = ex.Message;
            }

            // 3) WAV silence fallback (always playable)
            var wav = GenerateWavSilence(seconds: 1, sampleRate: 16000);
            return (wav, "audio/wav", "fallback", lastSdkError ?? lastRestError);
        }

        // -------- Voice/locale picking --------
        private static (string Locale, string Voice) PickVoice(string? code)
        {
            var c = (code ?? "").ToLowerInvariant();
            return c switch
            {
                "en" or "en-gb" => ("en-GB", "en-GB-SoniaNeural"),
                "en-us" => ("en-US", "en-US-JennyNeural"),
                "fr" or "fr-fr" => ("fr-FR", "fr-FR-DeniseNeural"),
                "es" or "es-es" => ("es-ES", "es-ES-ElviraNeural"),
                "de" or "de-de" => ("de-DE", "de-DE-KatjaNeural"),
                "it" or "it-it" => ("it-IT", "it-IT-ElsaNeural"),
                "pt" or "pt-pt" => ("pt-PT", "pt-PT-FernandaNeural"),
                "pt-br" => ("pt-BR", "pt-BR-FranciscaNeural"),
                "pl" or "pl-pl" => ("pl-PL", "pl-PL-ZofiaNeural"),
                "ru" or "ru-ru" => ("ru-RU", "ru-RU-DmitryNeural"),
                "tr" or "tr-tr" => ("tr-TR", "tr-TR-EmelNeural"),
                "ar" or "ar-eg" => ("ar-EG", "ar-EG-SalmaNeural"),
                "zh" or "zh-hans" or "zh-cn" => ("zh-CN", "zh-CN-XiaoxiaoNeural"),
                "zh-hant" or "zh-tw" => ("zh-TW", "zh-TW-HsiaoChenNeural"),
                "ja" or "ja-jp" => ("ja-JP", "ja-JP-NanamiNeural"),
                "ko" or "ko-kr" => ("ko-KR", "ko-KR-SunHiNeural"),
                "nl" or "nl-nl" => ("nl-NL", "nl-NL-ColetteNeural"),
                "sv" or "sv-se" => ("sv-SE", "sv-SE-HilleviNeural"),
                _ => ("en-GB", "en-GB-SoniaNeural"),
            };
        }

        // -------- REST TTS (MP3) --------
        private async Task<byte[]> SpeakViaRestAsync(string text, string locale, string voice, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text)) text = " ";

            string escaped = System.Security.SecurityElement.Escape(text) ?? " ";
            string ssml =
$@"<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""{locale}"">
  <voice name=""{voice}"" xml:lang=""{locale}"">{escaped}</voice>
</speak>";

            var url = $"https://{_region}.tts.speech.microsoft.com/cognitiveservices/v1";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("WhisperTranslator", "1.0"));
            req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", _key);
            req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Region", _region);
            req.Headers.TryAddWithoutValidation("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
            req.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

            var client = _http.CreateClient();
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"TTS HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {body}");
            }
            return await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }

        // -------- SDK TTS (MP3) --------
        private async Task<byte[]> SpeakViaSdkAsync(string text, string voice, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text)) text = " ";
            var cfg = SpeechConfig.FromSubscription(_key, _region);
            cfg.SpeechSynthesisVoiceName = voice;
            cfg.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3);

            using var synthesizer = new SpeechSynthesizer(cfg, audioConfig: null);
            var result = await synthesizer.SpeakTextAsync(text).ConfigureAwait(false);

            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
            {
                var detail = SpeechSynthesisCancellationDetails.FromResult(result);
                throw new InvalidOperationException($"TTS failed: {result.Reason} - {detail.Reason}. {detail.ErrorDetails}");
            }

            using var stream = AudioDataStream.FromResult(result);
            var output = new List<byte>(64 * 1024);
            var chunk = new byte[32 * 1024];

            while (true)
            {
                uint read = stream.ReadData(chunk);
                if (read == 0) break;
                output.AddRange(chunk.AsSpan(0, (int)read).ToArray());
                if (ct.IsCancellationRequested) break;
            }
            return output.ToArray();
        }

        // -------- Helpers --------
        private static string NormaliseForSsml(string input)
        {
            if (string.IsNullOrEmpty(input)) return " ";
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (ch == 0x9 || ch == 0xA || ch == 0xD ||
                    (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD))
                    sb.Append(ch);
            }
            if (sb.Length > 4000) sb.Length = 4000;
            var s = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(s) ? " " : s;
        }

        private static byte[] GenerateWavSilence(int seconds, int sampleRate)
        {
            int channels = 1;
            short bitsPerSample = 16;
            int totalSamples = sampleRate * seconds * channels;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            int dataSize = totalSamples * (bitsPerSample / 8);
            int fmtChunkSize = 16;
            int riffChunkSize = 4 + (8 + fmtChunkSize) + (8 + dataSize);

            using var ms = new MemoryStream(44 + dataSize);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(riffChunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(fmtChunkSize);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            bw.Write(new byte[dataSize]);

            bw.Flush();
            return ms.ToArray();
        }
    }
}
