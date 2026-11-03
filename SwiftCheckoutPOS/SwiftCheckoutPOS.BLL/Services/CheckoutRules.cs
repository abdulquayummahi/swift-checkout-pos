using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Pricing;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>Approved checkout rules (OWNER_DECISIONS C, I, M). Pure functions, no database.</summary>
public static class CheckoutRules
{
    public static readonly string[] Tenders = ["Cash", "Card", "bKash", "Nagad", "Due"];
    public const decimal DueLimit = 2500m;                 // PR-Q86
    public static decimal DiscountLimitPercent(string role) => role switch
    {
        "Admin" => decimal.MaxValue,                       // PR-Q67
        "Manager" => 50m,
        _ => 25m
    };
    public static int Rank(string role) => role switch { "Admin" => 3, "Manager" => 2, "Cashier" => 1, _ => 0 };

    /// <summary>PR-Q90/CQ-02: (item discounts + bill discount) ÷ bill before any discount × 100.</summary>
    public static decimal CombinedDiscountPercent(CheckoutSessionDTO cart)
    {
        var before = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        if (before <= 0) return 0;
        var discount = before - cart.TotalAmount; // everything taken off the shelf prices
        return discount <= 0 ? 0 : discount / before * 100m;
    }

    /// <summary>DR-36/CQ-02/17: per-line discount (item x qty + share of bill discount), adds up exactly.</summary>
    public static decimal[] LineDiscounts(CheckoutSessionDTO cart)
    {
        var shares = Money.Allocate(cart.CalculatedDiscount, cart.Items.Select(i => i.LineTotal).ToList());
        return cart.Items.Select((i, k) => Money.R2(i.ItemDiscount * i.Quantity) + shares[k]).ToArray();
    }

    /// <summary>
    /// BP-17/CQ-01: resolves up to two tenders. Non-cash amounts are exact; Cash takes the remainder and is
    /// rounded to whole taka (the difference is the sale's rounding amount). Without Cash, the line without
    /// an amount takes the remainder.
    /// </summary>
    public static (List<ResolvedTenderDTO> Payments, decimal Rounding) Resolve(PaymentPlanDTO plan, decimal total)
    {
        if (plan?.Lines is null || plan.Lines.Count is < 1 or > 2) throw new CheckoutValidationException("Use one or two payment methods.");
        if (plan.Lines.Any(l => !Tenders.Contains(l.Tender))) throw new CheckoutValidationException("Choose Cash, Card, bKash, Nagad or Due.");
        if (plan.Lines.Select(l => l.Tender).Distinct().Count() != plan.Lines.Count) throw new CheckoutValidationException("Use two different payment methods.");
        foreach (var l in plan.Lines)
        {
            if (l.Amount is decimal a && (a <= 0 || Money.R2(a) != a)) throw new CheckoutValidationException("Each payment amount must be above zero with at most 2 decimals.");
            if (l.Reference?.Length > 50) throw new CheckoutValidationException("Payment reference is too long (max 50).");
        }
        var cash = plan.Lines.SingleOrDefault(l => l.Tender == "Cash");
        var remainderLine = cash ?? (plan.Lines.Count == 1 ? plan.Lines[0] : plan.Lines.SingleOrDefault(l => l.Amount is null));
        if (remainderLine is null) throw new CheckoutValidationException("Leave the amount empty on one payment method; it takes the rest of the bill.");
        var fixedSum = plan.Lines.Where(l => l != remainderLine).Sum(l => l.Amount ?? throw new CheckoutValidationException("Enter the amount for the second payment method."));
        var remainder = total - fixedSum;
        if (remainder <= 0) throw new CheckoutValidationException("The second payment covers the whole bill. Use a single payment method.");

        var rounding = remainderLine.Tender == "Cash" ? Money.CashRounding(remainder) : 0m;
        var payments = new List<ResolvedTenderDTO>();
        foreach (var l in plan.Lines)
        {
            var amount = l == remainderLine ? remainder + rounding : l.Amount!.Value;
            if (amount <= 0) throw new CheckoutValidationException("The cash part rounds to zero. Pay this bill without cash.");
            decimal? tendered = null; decimal change = 0;
            if (l.Tender == "Cash" && l.Tendered is decimal given)
            {
                if (given < amount || Money.R2(given) != given) throw new CheckoutValidationException($"Cash received must be at least {Money.Bdt(amount)}.");
                tendered = given; change = given - amount;
            }
            payments.Add(new ResolvedTenderDTO { Tender = l.Tender, Amount = amount, Tendered = tendered, Change = change, Reference = string.IsNullOrWhiteSpace(l.Reference) ? null : l.Reference.Trim() });
        }
        return (payments, rounding);
    }
}
