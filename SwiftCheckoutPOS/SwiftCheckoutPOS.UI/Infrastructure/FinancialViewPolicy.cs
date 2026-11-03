using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>BP-15: server-side "CanViewFinancials" policy (PR-Q50/Q92, CQ-03).</summary>
public sealed class FinancialViewRequirement : IAuthorizationRequirement
{
    public const string Policy = "CanViewFinancials";
}

public sealed class FinancialViewHandler(SettingsService settings) : AuthorizationHandler<FinancialViewRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, FinancialViewRequirement requirement)
    {
        if (settings.CanViewFinancials(context.User.FindFirstValue(ClaimTypes.Role))) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class FinancialViewExtensions
{
    public static bool CanViewFinancials(this ClaimsPrincipal user, SettingsService settings) =>
        settings.CanViewFinancials(user.FindFirstValue(ClaimTypes.Role));
    public static string RoleName(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "Cashier";
}
