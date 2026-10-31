// Project: SwiftCheckoutPOS.UI
// File: Controllers/AccountController.cs
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Models;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly AuthService _authService;

        public AccountController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in, bypass the login screen
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return User.IsInRole("Admin") ? RedirectToAction("Index", "AdminDashboard") : RedirectToAction("Index", "Checkout");
            }
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // BLL handles the secure validation
            if (_authService.ValidateCredentials(model.Username, model.Password, out var userId, out string? role, out string? fullName))
            {
                // Construct offline Claims Identity
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new Claim(ClaimTypes.Name, fullName ?? model.Username),
                    new Claim(ClaimTypes.Role, role!)
                };

                var claimsIdentity = new ClaimsIdentity(claims, "POSAuthCookie");
                var authProperties = new AuthenticationProperties { IsPersistent = true };

                await HttpContext.SignInAsync("POSAuthCookie", new ClaimsPrincipal(claimsIdentity), authProperties);

                // Route based on RBAC
                return role == "Admin" ? RedirectToAction("Index", "AdminDashboard") : RedirectToAction("Index", "Checkout");
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password, or account is deactivated.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("POSAuthCookie");
            // Clear session data (e.g., active carts) on logout to protect data integrity
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}
