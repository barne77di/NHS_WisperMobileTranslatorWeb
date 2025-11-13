using Azure;
using Azure.AI.OpenAI;
using OpenAI.Audio; // <- IMPORTANT: brings in AudioTranscriptionOptions, etc.

namespace WhisperTranslator.Web.Services
{
    public class TranscriptionService
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deployment;

        public TranscriptionService(IConfiguration cfg)
        {
            var ep = cfg["AzureOpenAI:Endpoint"]!;
            var key = cfg["AzureOpenAI:Key"]!;
            _deployment = cfg["AzureOpenAI:WhisperDeployment"] ?? "whisper";
            _client = new AzureOpenAIClient(new Uri(ep), new AzureKeyCredential(key));
        }

        public async Task<(string text, string language)> TranscribeAsync(Stream audioStream, CancellationToken ct = default)
        {
            // Ensure seekable stream
            using var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms, ct);
            ms.Position = 0;

            var audioClient = _client.GetAudioClient(_deployment);

            var opts = new AudioTranscriptionOptions
            {
                // Optional: set format, prompt, temperature, etc.
                // ResponseFormat = AudioTranscriptionFormat.Verbose, // if your version supports this
                // Temperature = 0.2f
            };

            // Overload: (Stream content, string fileName, AudioTranscriptionOptions options [, CancellationToken ct])
            var result = await audioClient.TranscribeAudioAsync(ms, "speech.webm", opts, ct);

            var text = result.Value.Text ?? string.Empty;
            var language = result.Value.Language ?? "auto";
            return (text, language);
        }
    }
}
