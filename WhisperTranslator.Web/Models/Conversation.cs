
namespace WhisperTranslator.Web.Models
{
    public class Conversation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "Conversation";
        public DateTime CreatedUtc { get; set; }
        public string? UniqueRef { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
