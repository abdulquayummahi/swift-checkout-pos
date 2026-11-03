using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// BP-07: what a person on the till sees when something fails. Business-rule messages written by the services
/// (validation, InvalidOperation, Argument) are shown as they are; database, file, network and unexpected errors
/// never show their raw text (it can contain SQL, table names, paths or cost values) and are written to the local log.
/// </summary>
public static class SafeErrors
{
    public const string Generic = "Something went wrong and nothing was changed. Try again; if it keeps happening, tell the Admin (details are in the log).";

    public static string UserMessage(Exception ex) => ex switch
    {
        CheckoutValidationException or ArgumentException => ex.Message,
        DbUpdateConcurrencyException => "Someone else changed this record. Reload the page and try again.",
        DbUpdateException => Generic,
        InvalidOperationException when IsFrameworkMessage(ex) => Generic,
        InvalidOperationException => ex.Message,
        UnauthorizedAccessException => "You are not allowed to do that.",
        _ => Generic
    };

    /// <summary>Returns the safe message and logs anything that was hidden.</summary>
    public static string SafeError(this ControllerBase controller, Exception ex)
    {
        var message = UserMessage(ex);
        if (message == Generic)
        {
            var factory = controller.HttpContext?.RequestServices?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            factory?.CreateLogger(controller.GetType().FullName ?? "Controller")
                .LogError(ex, "Request {Path} failed", controller.HttpContext?.Request.Path.Value);
        }
        return message;
    }

    // EF Core / SqlClient raise InvalidOperationException for internal faults; their text is not meant for staff.
    private static bool IsFrameworkMessage(Exception ex)
    {
        var source = ex.Source ?? "";
        return source.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || source.StartsWith("Microsoft.Data.SqlClient", StringComparison.Ordinal)
            || ex.InnerException is Microsoft.Data.SqlClient.SqlException;
    }
}
