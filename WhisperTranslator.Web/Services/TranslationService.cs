using Azure;
using Azure.AI.Translation.Text;

namespace WhisperTranslator.Web.Services
{
    public class TranslationService
    {
        private readonly TextTranslationClient _client;

        public TranslationService(IConfiguration cfg)
        {
            var key = new AzureKeyCredential(cfg["Translator:Key"]!);
            var region = cfg["Translator:Region"]!;
            // ctor: (AzureKeyCredential key, string region)
            _client = new TextTranslationClient(key, region);
        }

        public async Task<(string detectedLang, string translated)> DetectAndTranslateAsync(
            string text, string to = "en", CancellationToken ct = default)
        {
            // overload: TranslateAsync(string to, IEnumerable<string> input, ...)
            var res = await _client.TranslateAsync(
                to,
                new[] { text },
                cancellationToken: ct);

            var first = res.Value.First();
            var det = first.DetectedLanguage?.Language ?? "auto";
            var tr = first.Translations.FirstOrDefault()?.Text ?? text;
            return (det, tr);
        }

        public async Task<string> TranslateAsync(
            string text, string _fromIgnored, string to, CancellationToken ct = default)
        {
            // Auto-detect source (ignore 'from' to stay compatible across SDK versions)
            var res = await _client.TranslateAsync(
                to,
                new[] { text },
                cancellationToken: ct);

            return res.Value.First().Translations.First().Text;
        }
    }
}
