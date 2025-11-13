using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhisperTranslator.Web.Models;

namespace WhisperTranslator.Web.Controllers
{
    [AllowAnonymous] // login must be reachable without auth
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly UserManager<ApplicationUser> _users;

        public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
        {
            _signIn = signIn; _users = users;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Home");
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            var user = await _users.FindByEmailAsync(email);
            if (user != null)
            {
                var res = await _signIn.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
                if (res.Succeeded)
                    return Redirect(returnUrl ?? Url.Action("Index", "Home")!);
            }

            ModelState.AddModelError("", "Invalid credentials.");
            ViewBag.ReturnUrl = returnUrl ?? Url.Action("Index", "Home");
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        public IActionResult Denied() => View();
    }
}
