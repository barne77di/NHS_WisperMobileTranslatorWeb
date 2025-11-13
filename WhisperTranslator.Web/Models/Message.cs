
namespace WhisperTranslator.Web.Models
{
    public class Message
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ConversationId { get; set; }
        public Conversation? Conversation { get; set; }

        // "user" (original speaker) or "assistant" (your English reply)
        public string Role { get; set; } = "user";

        public string? SourceLang { get; set; } // e.g., "fr"
        public string? TargetLang { get; set; } // e.g., "en"

        public string Text { get; set; } = "";
        public string? Translation { get; set; }

        public DateTime TimestampUtc { get; set; }
        public string? AudioUrl { get; set; } // path to synthesized audio stored in wwwroot/audio
    }
}
