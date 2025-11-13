using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhisperTranslator.Web.Data;
using WhisperTranslator.Web.Models;
using WhisperTranslator.Web.Services;
using static System.Net.Mime.MediaTypeNames;

namespace WhisperTranslator.Web.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly TranscriptionService _stt;
        private readonly TranslationService _tr;
        private readonly TtsService _tts;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ApiController(TranscriptionService stt, TranslationService tr, TtsService tts, AppDbContext db, IWebHostEnvironment env)
        {
            _stt = stt; _tr = tr; _tts = tts; _db = db; _env = env;
        }

        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] Guid conversationId)
        {
            var items = await _db.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.TimestampUtc)
                .Select(m => new { m.Role, m.Text, m.Translation, m.SourceLang, m.TargetLang, m.TimestampUtc })
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost("transcribe")]
        public async Task<IActionResult> Transcribe([FromForm] Guid conversationId, [FromForm] IFormFile audio)
        {
            if (audio == null || audio.Length == 0) return BadRequest("no audio");
            using var s = audio.OpenReadStream();
            var (text, lang) = await _stt.TranscribeAsync(s);

            var (det, en) = await _tr.DetectAndTranslateAsync(text, "en");

            var convo = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (convo == null) { convo = new Conversation(); _db.Conversations.Add(convo); }

            var msg = new Message
            {
                Conversation = convo,
                Role = "user",
                SourceLang = det,
                TargetLang = "en",
                Text = text,
                Translation = en,
                TimestampUtc = DateTime.UtcNow
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            return Ok(new { conversationId = convo.Id, text, detectedLanguage = det, translated = en });
        }

        [HttpPost("reply-voice")]
        public async Task<IActionResult> ReplyVoice([FromForm] Guid conversationId, [FromForm] IFormFile audio)
        {
            if (audio == null || audio.Length == 0) return BadRequest("No audio provided for reply.");

            try
            {
                var convo = await _db.Conversations.Include(c => c.Messages)
                                                   .FirstOrDefaultAsync(c => c.Id == conversationId);
                if (convo == null) return BadRequest("Conversation not found. Start by speaking first.");

                var lastUser = convo.Messages.LastOrDefault(m => m.Role == "user");
                var target = lastUser?.SourceLang;
                if (string.IsNullOrWhiteSpace(target) || target.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    target = convo.Messages.Where(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.SourceLang) && m.SourceLang != "auto")
                                           .Select(m => m.SourceLang).LastOrDefault();
                if (string.IsNullOrWhiteSpace(target) || target.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    target = "en";

                using var s = audio.OpenReadStream();
                var (replyTextEn, _) = await _stt.TranscribeAsync(s);

                if (string.IsNullOrWhiteSpace(replyTextEn))
                {
                    return Ok(new
                    {
                        text = "",
                        translated = "",
                        target = (string?)null,
                        audioBase64 = "",
                        audioLength = 0,
                        audioContentType = "audio/mpeg",
                        note = "no_speech",
                        sttLen = 0,
                        transLen = 0
                    });
                }

                var msg = new Message
                {
                    ConversationId = conversationId,
                    Role = "assistant",
                    SourceLang = "en",
                    TargetLang = target,
                    Text = replyTextEn,
                    TimestampUtc = DateTime.UtcNow
                };

                // TTS: pass the target lang (e.g., "fr", "ar", "zh-Hans") so we pick a matching voice
                var translated = await _tr.TranslateAsync(replyTextEn, "en", target);
                msg.Translation = translated;

                var (bytes, contentType, ttsSource, ttsErr) = await _tts.SpeakAsync(translated, target);
                var audioBase64 = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                var audioLength = bytes?.Length ?? 0;

                _db.Messages.Add(msg);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    text = replyTextEn,
                    translated,
                    target,
                    audioBase64,
                    audioLength,
                    audioContentType = contentType,
                    ttsSource,
                    ttsErr,
                    sttLen = replyTextEn?.Length ?? 0,
                    transLen = translated?.Length ?? 0
                });
            }
            catch (Exception ex)
            {
                return Problem(title: "Voice reply failed", detail: ex.Message, statusCode: 500);
            }
        }

        [HttpPost("reply")]
        public async Task<IActionResult> Reply([FromForm] Guid conversationId, [FromForm] string text)
        {
            try
            {
                var convo = await _db.Conversations.Include(c => c.Messages)
                                   .FirstOrDefaultAsync(c => c.Id == conversationId);
                if (convo == null)
                {
                    convo = new Conversation { Title = "Conversation" };
                    _db.Conversations.Add(convo);
                    await _db.SaveChangesAsync();
                    conversationId = convo.Id;
                }

                var lastUser = convo.Messages.LastOrDefault(m => m.Role == "user");
                var target = lastUser?.SourceLang;
                if (string.IsNullOrWhiteSpace(target) || target.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    target = convo.Messages.Where(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.SourceLang) && m.SourceLang != "auto")
                                           .Select(m => m.SourceLang).LastOrDefault();
                if (string.IsNullOrWhiteSpace(target) || target.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    target = "en";

                var msg = new Message
                {
                    ConversationId = conversationId,
                    Role = "assistant",
                    SourceLang = "en",
                    TargetLang = target,
                    Text = text,
                    TimestampUtc = DateTime.UtcNow
                };

                var translated = await _tr.TranslateAsync(text, "en", target);
                msg.Translation = translated;

                // TTS: pass the target lang (e.g., "fr", "ar", "zh-Hans") so we pick a matching voice
                var (bytes, contentType, ttsSource, ttsErr) = await _tts.SpeakAsync(translated, target);
                var audioBase64 = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                var audioLength = bytes?.Length ?? 0;

                _db.Messages.Add(msg);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    translated,
                    target,
                    audioBase64,
                    audioLength,
                    audioContentType = contentType,
                    ttsSource,
                    ttsErr
                });
            }
            catch (Exception ex)
            {
                return Problem(title: "Reply failed", detail: ex.Message, statusCode: 500);
            }
        }
    }
}
