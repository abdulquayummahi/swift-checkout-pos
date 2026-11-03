namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>Verifies a Manager/Admin's credentials typed at the till. Returns "User id: name (Role)" or null.</summary>
public interface IApprover
{
    (string Actor, string Role)? Verify(string username, string password);
}

public sealed class ApprovalService(AuthService auth) : IApprover
{
    public (string Actor, string Role)? Verify(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;
        if (!auth.ValidateCredentials(username.Trim(), password, out var id, out var role, out var name) || role is null) return null;
        var actor = $"User {id}: {name}";
        return (actor.Length <= 100 ? actor : actor[..100], role);
    }
}
