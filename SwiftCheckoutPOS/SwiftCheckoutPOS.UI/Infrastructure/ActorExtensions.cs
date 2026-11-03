using System.Security.Claims;

namespace SwiftCheckoutPOS.UI.Infrastructure;

public static class ActorExtensions
{
    /// <summary>Stable "User {id}: {name}" label for ledger/audit attribution (max 100 chars).</summary>
    public static string Actor(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(id, out var userId) || userId <= 0)
            throw new InvalidOperationException("Sign in again before doing this.");
        var actor = $"User {userId}: {user.Identity?.Name}";
        return actor.Length <= 100 ? actor : actor[..100];
    }
}
