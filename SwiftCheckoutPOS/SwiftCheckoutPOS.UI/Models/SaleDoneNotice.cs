using System.Text.Json;

namespace SwiftCheckoutPOS.UI.Models;

/// <summary>
/// What the till shows after a sale is saved (the "Sale complete" screen): the change to hand back, what was paid
/// and the receipt number. Carried in TempData across the redirect as JSON.
/// </summary>
public sealed record SaleDoneNotice(string Receipt, decimal Total, decimal Tendered, decimal Change, string Payment)
{
    public string ToJson() => JsonSerializer.Serialize(this);

    public static SaleDoneNotice? From(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SaleDoneNotice>(json); }
        catch (JsonException) { return null; }
    }
}
