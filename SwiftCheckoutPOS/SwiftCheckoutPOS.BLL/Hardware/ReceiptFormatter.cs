using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;

namespace SwiftCheckoutPOS.BLL.Hardware;

/// <summary>PR-Q48: receipt header from Admin settings (no hard-coded shop details).</summary>
public sealed record ReceiptHeaderDTO(string ShopName, string Address, string Phone, string Bin, string Footer)
{
    public static ReceiptHeaderDTO Default { get; } = new("SWIFT CHECKOUT", "", "", "", "Thank you for your purchase!");
}

/// <summary>One 48-column (80 mm) layout used by the virtual and the ESC/POS printers alike.</summary>
public static class ReceiptFormatter
{
    public const int Width = 48;

    public static string Receipt(CheckoutSessionDTO session, ReceiptHeaderDTO header)
    {
        var r = new StringBuilder();
        if (session.ReprintNote is not null)
        {
            r.AppendLine(Center("*** REPRINT / COPY ***"));
            r.AppendLine(Center(session.ReprintNote));
        }
        r.AppendLine(Center(header.ShopName));
        if (!string.IsNullOrWhiteSpace(header.Address)) r.AppendLine(Center(header.Address));
        if (!string.IsNullOrWhiteSpace(header.Phone)) r.AppendLine(Center("Tel: " + header.Phone));
        // PR-Q10/Q48: BIN only when VAT-registered (a BIN is set).
        if (!string.IsNullOrWhiteSpace(header.Bin)) r.AppendLine(Center("BIN: " + header.Bin));
        r.AppendLine(new string('-', Width));
        r.AppendLine($"Date: {session.ReceiptSaleDate ?? DateTime.Now:dd-MM-yyyy HH:mm:ss}");
        r.AppendLine($"Receipt #: {session.ReceiptNumber ?? "(not yet saved)"}");
        r.AppendLine($"Cashier: {CashierName(session.ReceiptCashierName) ?? "Preview"}");
        if (session.ReceiptPaymentMethod is not null) r.AppendLine($"Payment: {session.ReceiptPaymentMethod}");
        if (session.CustomerType == "Loyalty" && !string.IsNullOrEmpty(session.CustomerName)) r.AppendLine($"Customer: {session.CustomerName}");
        r.AppendLine(new string('-', Width));
        r.AppendLine(Columns("ITEM", "QTYxPRICE", "TOTAL"));
        r.AppendLine(new string('-', Width));
        foreach (var item in session.Items)
        {
            var name = item.ProductName.Length > 22 ? item.ProductName[..22] : item.ProductName;
            r.AppendLine(Columns(name, $"{item.Quantity}x{item.UnitPrice:N2}", item.LineTotal.ToString("N2")));
            if (item.ItemDiscount > 0) r.AppendLine(Columns("  item discount", "", $"-{item.ItemDiscount * item.Quantity:N2}"));
        }
        r.AppendLine(new string('-', Width));
        r.AppendLine(Columns("SUBTOTAL:", "", $"{session.GrossSubTotal:N2}"));
        if (session.CalculatedDiscount > 0) r.AppendLine(Columns("DISCOUNT:", "", $"-{session.CalculatedDiscount:N2}"));
        if (session.TaxAmount > 0) r.AppendLine(Columns("VAT (incl.):", "", $"{session.TaxAmount:N2}"));
        r.AppendLine(Columns("TOTAL:", "", $"BDT {session.TotalAmount:N2}"));
        var rounding = session.ReceiptRounding ?? session.RoundingFor(session.ReceiptPaymentMethod);
        if (rounding != 0)
        {
            r.AppendLine(Columns("ROUNDING:", "", $"{rounding:N2}"));
            r.AppendLine(Columns("PAYABLE:", "", $"BDT {session.TotalAmount + rounding:N2}"));
        }
        if (session.ReceiptPayments is { Count: > 0 })
        {
            r.AppendLine(new string('-', Width));
            foreach (var p in session.ReceiptPayments)
            {
                r.AppendLine(Columns($"PAID {p.Tender.ToUpperInvariant()}:", p.Reference ?? "", $"{p.Amount:N2}"));
                if (p.Tendered is decimal given)
                {
                    r.AppendLine(Columns("CASH RECEIVED:", "", $"{given:N2}"));
                    r.AppendLine(Columns("CHANGE:", "", $"{p.Change:N2}"));
                }
            }
        }
        r.AppendLine(new string('=', Width));
        if (!string.IsNullOrWhiteSpace(header.Footer)) r.AppendLine(Center(header.Footer));
        r.AppendLine("\n\n");
        return r.ToString();
    }

    public static string ZReport(ZReportDTO report, ReceiptHeaderDTO header)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Center(header.ShopName));
        sb.AppendLine(Center("END OF DAY Z-REPORT"));
        sb.AppendLine(new string('=', Width));
        sb.AppendLine($"Report date: {report.ReportDate:dd-MM-yyyy}");
        sb.AppendLine($"Printed at:  {DateTime.Now:dd-MM-yyyy HH:mm:ss}");
        sb.AppendLine(new string('-', Width));
        sb.AppendLine($"Sales:              {report.TotalTransactions}");
        sb.AppendLine($"Sales incl. VAT:    BDT {report.GrossRevenue:N2}");
        sb.AppendLine($"Refunds:           -BDT {report.RefundsTotal:N2}");
        sb.AppendLine($"Net sales (ex VAT): BDT {report.NetRevenue:N2}");
        if (report.TotalTaxCollected != 0) sb.AppendLine($"VAT:                BDT {report.TotalTaxCollected:N2}");
        sb.AppendLine(new string('-', Width));
        sb.AppendLine($"Cash:  BDT {report.CashTotal:N2}   Card:  BDT {report.CardTotal:N2}");
        sb.AppendLine($"bKash: BDT {report.BkashTotal:N2}   Nagad: BDT {report.NagadTotal:N2}");
        sb.AppendLine($"Due:   BDT {report.DueTotal:N2}");
        sb.AppendLine(new string('=', Width));
        sb.AppendLine("\n\n");
        return sb.ToString();
    }

    /// <summary>"User 3: Rahim" → "Rahim".</summary>
    private static string? CashierName(string? actor) =>
        actor is null ? null : actor.Contains(": ") ? actor[(actor.IndexOf(": ", StringComparison.Ordinal) + 2)..] : actor;

    private static string Center(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length >= Width) return text[..Width];
        var padding = (Width - text.Length) / 2;
        return text.PadLeft(text.Length + padding);
    }

    private static string Columns(string left, string center, string right)
    {
        const int leftWidth = 22, centerWidth = 12, rightWidth = 14;
        if (center.Length > centerWidth || right.Length > rightWidth)
            return left + Environment.NewLine + center + Environment.NewLine + right.PadLeft(Width); // never truncate money
        var l = left.Length > leftWidth ? left[..leftWidth] : left.PadRight(leftWidth);
        return l + center.PadLeft(centerWidth) + right.PadLeft(rightWidth);
    }
}
