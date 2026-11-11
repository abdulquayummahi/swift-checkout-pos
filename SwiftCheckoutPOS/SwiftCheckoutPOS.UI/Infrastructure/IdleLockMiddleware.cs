using Microsoft.AspNetCore.Authentication;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// BP-21 (PR-Q51): after 15 minutes without activity the screen locks; the same user (or a Manager/Admin)
/// re-enters a password. The cart stays in the session.
/// </summary>
public sealed class IdleLockMiddleware(RequestDelegate next)
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(15);
    public const string LastActivityKey = "SwiftCheckout.LastActivity";
    public const string LockedKey = "SwiftCheckout.Locked";
    private static readonly string[] Open = ["/Account/Locked", "/Account/Unlock", "/Account/Logout", "/Account/Login", "/Account/Setup", "/Account/AccessDenied", "/Home/Error"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        if (context.User.Identity?.IsAuthenticated == true && !IsStatic(path) && !Open.Any(o => path.StartsWith(o, StringComparison.OrdinalIgnoreCase)))
        {
            var session = context.Session;
            var now = DateTime.UtcNow.Ticks;
            var last = long.TryParse(session.GetString(LastActivityKey), out var t) ? t : now;
            if (session.GetString(LockedKey) == "1" || TimeSpan.FromTicks(now - last) > IdleLimit)
            {
                session.SetString(LockedKey, "1");
                if (context.Request.Headers.Accept.ToString().Contains("application/json") || context.Request.Headers.ContainsKey("RequestVerificationToken"))
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = "Screen locked. Enter your password to continue." });
                    return;
                }
                context.Response.Redirect("/Account/Locked?returnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
                return;
            }
            session.SetString(LastActivityKey, now.ToString());
        }
        await next(context);
    }

    private static bool IsStatic(string path) =>
        path.StartsWith("/lib/") || path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/images/") || path.StartsWith("/img/") || path.StartsWith("/fonts/") || path == "/favicon.ico";
}
