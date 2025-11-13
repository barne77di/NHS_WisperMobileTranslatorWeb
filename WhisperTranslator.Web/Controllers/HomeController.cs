
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhisperTranslator.Web.Data;

namespace WhisperTranslator.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        public HomeController(AppDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Start() => View();
        
        [HttpGet]
        public IActionResult Index(Guid? conversationId)        // chat page
        {
            ViewBag.ConversationId = conversationId;
            return View();
        }
    }
}
