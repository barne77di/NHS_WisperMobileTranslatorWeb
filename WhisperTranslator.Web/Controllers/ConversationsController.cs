using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhisperTranslator.Web.Data;
using WhisperTranslator.Web.Models;

namespace WhisperTranslator.Web.Controllers
{
    [Authorize]
    public class ConversationsController : Controller
    {
        private readonly AppDbContext _db;
        public ConversationsController(AppDbContext db) => _db = db;

        // POST /Conversations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string uniqueRef)
        {
            uniqueRef = (uniqueRef ?? "").Trim();
            if (string.IsNullOrWhiteSpace(uniqueRef))
            {
                TempData["Error"] = "Unique Ref is required.";
                return RedirectToAction("Start", "Home");
            }

            // Prevent dup
            var exists = await _db.Conversations.FirstOrDefaultAsync(c => c.UniqueRef == uniqueRef);
            if (exists != null)
                return RedirectToAction("Index", "Home", new { conversationId = exists.Id });

            var c = new Conversation { UniqueRef = uniqueRef, Title = $"Translation {uniqueRef}" };
            _db.Conversations.Add(c);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index", "Home", new { conversationId = c.Id });
        }

        // GET /Conversations/List
        [HttpGet]
        public async Task<IActionResult> List(DateTime? from = null, DateTime? to = null, string? uniqueRef = null)
        {
            var q = _db.Conversations.AsNoTracking().OrderByDescending(c => c.CreatedUtc).AsQueryable();

            if (!string.IsNullOrWhiteSpace(uniqueRef))
                q = q.Where(c => c.UniqueRef != null && c.UniqueRef.Contains(uniqueRef));

            if (from.HasValue) q = q.Where(c => c.CreatedUtc >= from.Value);
            if (to.HasValue) q = q.Where(c => c.CreatedUtc < to.Value.AddDays(1));

            var items = await q.Take(200).ToListAsync(); // cap results
            return View(items);
        }

        // GET /Conversations/Open/{id}
        [HttpGet]
        public async Task<IActionResult> Open(Guid id)
        {
            var c = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            return RedirectToAction("Index", "Home", new { conversationId = id });
        }
    }
}
