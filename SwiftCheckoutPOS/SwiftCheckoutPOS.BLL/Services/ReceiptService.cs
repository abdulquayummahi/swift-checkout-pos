using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>BP-23: receipts always come from the saved sale (PR-Q48); reprints are marked and logged (PR-Q83).</summary>
public sealed class ReceiptService(SalesRepository sales, AtomicUnit unit)
{
    public CheckoutSessionDTO FromSale(string receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber)) throw new ArgumentException("Enter a receipt number.");
        var sale = sales.LoadForRefund(receiptNumber.Trim()) ?? throw new InvalidOperationException("Receipt not found.");
        if (sale.OrderStatus is "Held" or "Resumed" or "Cancelled") throw new InvalidOperationException("Only completed sales have a receipt.");
        var cart = new CheckoutSessionDTO
        {
            InvoiceNumber = sale.OperationKey ?? sale.ReceiptNumber, ReceiptNumber = sale.ReceiptNumber,
            ReceiptSaleDate = sale.SaleDate, ReceiptCashierName = sale.CashierName, ReceiptPaymentMethod = sale.PaymentMethod,
            ReceiptRounding = sale.RoundingAmount, CustomerId = sale.CustomerId,
            CustomerType = sale.CustomerId.HasValue ? "Loyalty" : "Walk-in", CustomerName = sale.Customer?.FullName ?? "",
            Items = sale.SaleItems.Select(i => new CartItemDTO
            {
                ProductId = i.ProductId, ProductName = i.Product?.ProductName ?? "Item", Barcode = i.Product?.Barcode ?? "",
                Quantity = i.Quantity, UnitPrice = i.UnitPrice,
                ItemDiscount = i.Quantity > 0 ? (i.UnitPrice * i.Quantity - i.LineTotal) / i.Quantity : 0, TaxRate = 0
            }).ToList(),
            ReceiptPayments = sale.Payments.Select(p => new ResolvedTenderDTO
            {
                Tender = p.Tender, Amount = p.Amount, Tendered = p.Tendered, Reference = p.Reference,
                Change = p.Tendered is decimal t ? t - p.Amount : 0
            }).ToList()
        };
        // Saved totals win over recomputation: the bill discount is whatever was saved.
        cart.BillDiscountAmount = Math.Max(0, sale.SubTotal - sale.TotalAmount);
        return cart;
    }

    public CheckoutSessionDTO Reprint(string receiptNumber, string actor)
    {
        var cart = FromSale(receiptNumber);
        var name = actor.Contains(": ") ? actor[(actor.IndexOf(": ", StringComparison.Ordinal) + 2)..] : actor;
        cart.ReprintNote = $"Printed {DateTime.Now:dd-MM-yyyy HH:mm} by {name}";
        unit.Run(() => unit.Audit(actor, "Reprint", "Sale", cart.ReceiptNumber!));
        return cart;
    }
}
