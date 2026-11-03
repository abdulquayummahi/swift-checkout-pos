using System.Globalization;

namespace SwiftCheckoutPOS.BLL.Pricing;

/// <summary>
/// The ONE place for approved money rules (OWNER_DECISIONS.md A–B, CQ-01/16/17/24).
/// decimal only; 2 decimals; half away from zero; prices are VAT-inclusive; cash-only whole-taka rounding.
/// </summary>
public static class Money
{
    /// <summary>PR-Q05/06: round to paisa, 0.005 → 0.01.</summary>
    public static decimal R2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>PR-Q07/Q91: nearest whole taka, half away from zero.</summary>
    public static decimal WholeTaka(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>PR-Q08: VAT contained in a VAT-inclusive amount at the given rate (unrounded).</summary>
    public static decimal VatIncluded(decimal inclusiveAmount, decimal ratePercent) =>
        ratePercent <= 0 ? 0m : inclusiveAmount * ratePercent / (100m + ratePercent);

    /// <summary>PR-Q07/Q91/CQ-01: rounding added to the cash part only. Card, bKash, Nagad and Due stay exact.</summary>
    public static decimal CashRounding(decimal cashPart) => WholeTaka(cashPart) - cashPart;

    public static bool IsCash(string? tender) => string.Equals(tender, "Cash", StringComparison.Ordinal);

    /// <summary>
    /// Splits <paramref name="total"/> across <paramref name="weights"/> in proportion, rounded to paisa.
    /// CQ-17/21/24: any residual (positive or negative) goes to the largest weight (first one if equal),
    /// so the parts always add up exactly to the total.
    /// </summary>
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        var parts = new decimal[weights.Count];
        var sum = weights.Sum();
        if (weights.Count == 0 || sum <= 0) return parts;
        for (var i = 0; i < parts.Length; i++) parts[i] = R2(total * weights[i] / sum);
        var residual = total - parts.Sum();
        if (residual != 0)
        {
            var largest = 0;
            for (var i = 1; i < weights.Count; i++) if (weights[i] > weights[largest]) largest = i;
            parts[largest] += residual;
        }
        return parts;
    }

    /// <summary>PR-Q01: always BDT with 2 decimals, never the Windows regional currency.</summary>
    public static string Bdt(decimal value) => "BDT " + value.ToString("N2", Culture);

    /// <summary>Fixed formatting culture for the whole app (PR-Q01).</summary>
    public static readonly CultureInfo Culture = CreateCulture();

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.CurrencySymbol = "BDT ";
        culture.NumberFormat.CurrencyDecimalDigits = 2;
        culture.NumberFormat.CurrencyPositivePattern = 0; // "BDT 1,234.50"
        culture.NumberFormat.CurrencyNegativePattern = 1; // "-BDT 1,234.50"
        culture.DateTimeFormat.ShortDatePattern = "dd-MM-yyyy";
        return CultureInfo.ReadOnly(culture);
    }
}
