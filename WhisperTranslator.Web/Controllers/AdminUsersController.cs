using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WhisperTranslator.Web.Models;

namespace WhisperTranslator.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly RoleManager<IdentityRole> _roles;

        public AdminUsersController(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
        {
            _users = users; _roles = roles;
        }

        // GET: /AdminUsers
        public async Task<IActionResult> Index(string? q = null)
        {
            var all = _users.Users.ToList();

            if (!string.IsNullOrWhiteSpace(q))
                all = all.Where(u => (u.Email ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            var list = new List<AdminUserListItem>();
            foreach (var u in all.OrderBy(x => x.Email))
            {
                var roles = await _users.GetRolesAsync(u);
                list.Add(new AdminUserListItem
                {
                    Id = u.Id,
                    Email = u.Email ?? "",
                    IsAdmin = roles.Contains("Admin"),
                    IsUser = roles.Contains("User")
                });
            }

            return View(list);
        }

        // GET: /AdminUsers/Create
        public IActionResult Create() => View(new AdminUserEditVm { RoleUser = true });

        // POST: /AdminUsers/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminUserEditVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var exists = await _users.FindByEmailAsync(vm.Email);
            if (exists != null)
            {
                ModelState.AddModelError(nameof(vm.Email), "A user with this email already exists.");
                return View(vm);
            }

            var user = new ApplicationUser { UserName = vm.Email, Email = vm.Email, EmailConfirmed = true };
            var pwd = string.IsNullOrWhiteSpace(vm.Password) ? Guid.NewGuid().ToString("N") + "!" : vm.Password!;
            var res = await _users.CreateAsync(user, pwd);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            // Ensure roles exist (defensive)
            if (!await _roles.RoleExistsAsync("Admin")) await _roles.CreateAsync(new IdentityRole("Admin"));
            if (!await _roles.RoleExistsAsync("User")) await _roles.CreateAsync(new IdentityRole("User"));

            var rolesToAdd = new List<string>();
            if (vm.RoleAdmin) rolesToAdd.Add("Admin");
            if (vm.RoleUser) rolesToAdd.Add("User");
            if (rolesToAdd.Count > 0) await _users.AddToRolesAsync(user, rolesToAdd);

            TempData["Message"] = "User created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /AdminUsers/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _users.GetRolesAsync(user);
            var vm = new AdminUserEditVm
            {
                Id = user.Id,
                Email = user.Email ?? "",
                RoleAdmin = roles.Contains("Admin"),
                RoleUser = roles.Contains("User")
            };
            return View(vm);
        }

        // POST: /AdminUsers/Edit/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminUserEditVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = vm.Id is null ? null : await _users.FindByIdAsync(vm.Id);
            if (user == null) return NotFound();

            // Update email/username
            user.Email = vm.Email;
            user.UserName = vm.Email;
            var updateRes = await _users.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                foreach (var e in updateRes.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            // Optional password reset if provided
            if (!string.IsNullOrWhiteSpace(vm.Password))
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var pwdRes = await _users.ResetPasswordAsync(user, token, vm.Password);
                if (!pwdRes.Succeeded)
                {
                    foreach (var e in pwdRes.Errors) ModelState.AddModelError("", e.Description);
                    return View(vm);
                }
            }

            // Ensure roles present
            if (!await _roles.RoleExistsAsync("Admin")) await _roles.CreateAsync(new IdentityRole("Admin"));
            if (!await _roles.RoleExistsAsync("User")) await _roles.CreateAsync(new IdentityRole("User"));

            // Synchronize roles
            var current = await _users.GetRolesAsync(user);
            var want = new List<string>();
            if (vm.RoleAdmin) want.Add("Admin");
            if (vm.RoleUser) want.Add("User");

            var toAdd = want.Except(current).ToArray();
            var toRemove = current.Except(want).ToArray();

            if (toAdd.Length > 0) await _users.AddToRolesAsync(user, toAdd);
            if (toRemove.Length > 0) await _users.RemoveFromRolesAsync(user, toRemove);

            TempData["Message"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminUsers/Delete/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _users.FindByIdAsync(id);
            if (user == null) return NotFound();
            var res = await _users.DeleteAsync(user);
            if (!res.Succeeded)
            {
                TempData["Error"] = string.Join("; ", res.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["Message"] = "User deleted.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
