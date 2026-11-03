// Project: SwiftCheckoutPOS.UI
// File: Controllers/AccountController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Security;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;
using SwiftCheckoutPOS.UI.Models;

namespace SwiftCheckoutPOS.UI.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuthService _authService;
        private readonly LoginThrottle _throttle;
        private readonly AuditService _audit;

        public AccountController(AuthService authService, LoginThrottle throttle, AuditService audit)
        {
            _authService = authService;
            _throttle = throttle;
            _audit = audit;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (_authService.NeedsFirstAdmin()) return RedirectToAction(nameof(Setup));
            if (User.Identity is { IsAuthenticated: true }) return Home(User.FindFirstValue(ClaimTypes.Role));
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var username = model.Username?.Trim() ?? "";
            var wait = _throttle.WaitRemaining(username, DateTime.Now);
            if (wait > TimeSpan.Zero)
            {
                ModelState.AddModelError(string.Empty, $"Too many failed attempts. Wait {Math.Ceiling(wait.TotalSeconds)} seconds and try again.");
                return View(model);
            }

            if (_authService.ValidateCredentials(username, model.Password, out var userId, out string? role, out string? fullName, out var stamp))
            {
                _throttle.Reset(username);
                await SignIn(userId, fullName ?? username, role!, stamp!);
                return Home(role);
            }

            // PR-Q78: failed attempts are logged (never the password).
            var delay = _throttle.RegisterFailure(username, DateTime.Now);
            try { _audit.Record($"login:{(username.Length > 50 ? username[..50] : username)}", "LoginFailed", "User", username.Length > 100 ? username[..100] : username); }
            catch { /* logging must not block the login page */ }
            ModelState.AddModelError(string.Empty, delay > TimeSpan.Zero
                ? $"Invalid username or password. Wait {Math.Ceiling(delay.TotalSeconds)} seconds before the next try."
                : "Invalid username or password, or account is deactivated.");
            return View(model);
        }

        /// <summary>BP-13/PR-Q52: one-time first-Admin setup, only while no user exists, only from this PC.</summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Setup()
        {
            if (!_authService.NeedsFirstAdmin()) return RedirectToAction(nameof(Login));
            if (!IsLocal()) return Forbid();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult Setup(string username, string fullName, string password, string confirmPassword)
        {
            if (!_authService.NeedsFirstAdmin()) return RedirectToAction(nameof(Login));
            if (!IsLocal()) return Forbid();
            try
            {
                if (password != confirmPassword) throw new ArgumentException("The two passwords differ.");
                _authService.CreateFirstAdmin(username, fullName, password);
                TempData["Success"] = "Admin created. Sign in.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        /// <summary>BP-21: the screen lock. The cart and work stay in the session.</summary>
        [HttpGet]
        [Authorize]
        public IActionResult Locked(string? returnUrl)
        {
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult Unlock(string? username, string password, string? returnUrl)
        {
            var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var who = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
            var throttleKey = who ?? $"id:{me}";
            if (_throttle.WaitRemaining(throttleKey, DateTime.Now) > TimeSpan.Zero)
            {
                TempData["Error"] = "Too many failed attempts. Wait and try again.";
                return RedirectToAction(nameof(Locked), new { returnUrl });
            }
            bool ok;
            if (who is null)
            {
                // Same user: verify the signed-in user's own password.
                var state = int.TryParse(me, out var id) ? _authService.SecurityState(id) : null;
                var myName = _authService.UsernameOf(id);
                ok = myName is not null && _authService.ValidateCredentials(myName, password, out var uid, out _, out _) && uid == id;
            }
            else
            {
                // A Manager/Admin may unlock with their own password.
                ok = _authService.ValidateCredentials(who, password, out _, out var role, out _) && role is "Manager" or "Admin";
            }
            if (!ok)
            {
                _throttle.RegisterFailure(throttleKey, DateTime.Now);
                TempData["Error"] = "Wrong password.";
                return RedirectToAction(nameof(Locked), new { returnUrl });
            }
            _throttle.Reset(throttleKey);
            HttpContext.Session.Remove(IdleLockMiddleware.LockedKey);
            HttpContext.Session.SetString(IdleLockMiddleware.LastActivityKey, DateTime.UtcNow.Ticks.ToString());
            if (who is not null) _audit.Record(who, "ScreenUnlocked", "User", me ?? "", details: $"Unlocked session of {User.Identity?.Name}");
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("POSAuthCookie");
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        private async Task SignIn(int userId, string name, string role, string stamp)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, name),
                new(ClaimTypes.Role, role),
                new("stamp", stamp)
            };
            var identity = new ClaimsIdentity(claims, "POSAuthCookie");
            // Not persistent: closing the browser ends the login; at most 12 hours either way (PR-Q64).
            await HttpContext.SignInAsync("POSAuthCookie", new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
            HttpContext.Session.SetString(IdleLockMiddleware.LastActivityKey, DateTime.UtcNow.Ticks.ToString());
            HttpContext.Session.Remove(IdleLockMiddleware.LockedKey);
        }

        private IActionResult Home(string? role) =>
            role == "Admin" ? RedirectToAction("Index", "AdminDashboard")
            : role == "Manager" ? RedirectToAction("Index", "ManagerDashboard")
            : RedirectToAction("Index", "Checkout");

        private bool IsLocal()
        {
            var ip = HttpContext.Connection.RemoteIpAddress;
            return ip is null || System.Net.IPAddress.IsLoopback(ip);
        }
    }
}
