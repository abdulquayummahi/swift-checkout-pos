using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>Small view helpers for the design system (icons from /img/icons.svg, taka amounts).</summary>
public static class UiHelpers
{
    /// <summary>An icon from the local sprite (Lucide icons, ISC licence). Decorative: hidden from screen readers.</summary>
    public static IHtmlContent Icon(this IHtmlHelper html, string name, string? css = null) =>
        new HtmlString($"<svg class=\"icon{(css is null ? "" : " " + HtmlEncoder.Default.Encode(css))}\" aria-hidden=\"true\" focusable=\"false\"><use href=\"/img/icons.svg#i-{HtmlEncoder.Default.Encode(name)}\"></use></svg>");

    /// <summary>A taka amount: ৳ sign de-emphasised, figures tabular (PR-Q01: always BDT, two decimals).</summary>
    public static IHtmlContent Money(this IHtmlHelper html, decimal amount, string? css = null)
    {
        var sign = amount < 0 ? "−" : "";
        var text = Math.Abs(amount).ToString("N2", SwiftCheckoutPOS.BLL.Pricing.Money.Culture);
        return new HtmlString($"<span class=\"money{(css is null ? "" : " " + HtmlEncoder.Default.Encode(css))}\">{sign}<span class=\"cur\">৳</span>{text}</span>");
    }

    /// <summary>Staff name for display: records store "User 12: Rahim Uddin" for exact attribution; people read "Rahim Uddin".</summary>
    public static string Person(string? actor) =>
        string.IsNullOrWhiteSpace(actor) ? "–" : System.Text.RegularExpressions.Regex.Replace(actor, @"^User \d+: ", "");

    /// <summary>An internal name such as "ShiftOpened" or "VATChanged" as words people read: "Shift opened", "VAT changed".</summary>
    public static string Words(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "–";
        var words = System.Text.RegularExpressions.Regex.Split(name.Trim(), "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");
        return string.Join(' ', words.Select((w, i) => i == 0 || w.All(char.IsUpper) ? w : w.ToLowerInvariant()));
    }

    /// <summary>Two-letter initials for the avatar circle.</summary>
    public static string Initials(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var letters = parts.Length switch { 0 => "?", 1 => parts[0][..Math.Min(2, parts[0].Length)], _ => $"{parts[0][0]}{parts[^1][0]}" };
        return letters.ToUpper(CultureInfo.InvariantCulture);
    }
}
