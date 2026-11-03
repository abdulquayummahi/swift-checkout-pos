namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// BP-07: browser hardening headers for every response. Everything is served from this PC, so the policy allows
/// only 'self' (inline script/style stay allowed because the existing views use them). Signed-in pages are never
/// cached, so the Back button after logout or screen lock does not show sales or customer data.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            var type = context.Response.ContentType ?? "";
            if (type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) || type.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                headers.CacheControl = "no-store, no-cache, max-age=0";
                headers.Pragma = "no-cache";
            }
            return Task.CompletedTask;
        });
        return next(context);
    }
}
