using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Pricing;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>
/// BP-04/IMM-07/BP-18 — refunds and voids as dated events (OWNER_DECISIONS E, M; CQ-05/06/11/13/16/18/23).
/// Refund: Completed sale, within 7 x 24 h, by item and quantity, never more than sold; returned items go back
/// to stock or are recorded as Damaged; unpaid Due of the sale is cancelled first, the rest is paid back to the
/// tenders actually received (never more than received); cash payout rounded to whole taka.
/// Void: whole sale within 5 minutes, with a reason; everything reversed.
/// </summary>
public sealed class RefundService(SalesRepository salesRepo, AtomicUnit unit)
{
    public const int RefundWindowHours = 168; // CQ-13
    public const int VoidWindowMinutes = 5;   // PR-Q87

    public RefundLookupDTO LookupTransaction(string receiptNumber, DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber)) throw new ArgumentException("Receipt barcode cannot be empty.");
        var sale = salesRepo.LoadForRefund(receiptNumber.Trim()) ?? throw new InvalidOperationException($"Receipt [{receiptNumber.Trim()}] was not found.");
        var at = now ?? DateTime.Now;
        var dto = new RefundLookupDTO
        {
            ReceiptNumber = sale.ReceiptNumber, SaleDate = sale.SaleDate, PaymentMethod = sale.PaymentMethod,
            OrderStatus = sale.OrderStatus, TotalAmount = sale.TotalAmount, RefundedAmount = sale.RefundedAmount,
            CustomerName = sale.Customer?.FullName,
            Lines = sale.SaleItems.Select(i => new RefundLineDTO
            {
                SaleItemId = i.SaleItemId, ProductName = i.Product?.ProductName ?? "Unknown product", Quantity = i.Quantity,
                Refunded = sale.Refunds.SelectMany(r => r.Items).Where(r => r.SaleItemId == i.SaleItemId).Sum(r => r.Quantity),
                UnitPrice = i.UnitPrice, LineNet = Net(i)
            }).ToList()
        };
        dto.RefundBlockedReason = Eligibility(sale, at);
        dto.CanRefund = dto.RefundBlockedReason is null && dto.Lines.Any(l => l.Remaining > 0);
        dto.CanVoid = sale.OrderStatus == "Completed" && sale.Refunds.Count == 0 && at - sale.SaleDate <= TimeSpan.FromMinutes(VoidWindowMinutes);
        return dto;
    }

    /// <summary>Refund every remaining unit back to stock.</summary>
    public RefundResultDTO ExecuteRefund(string receiptNumber, string actor = "SYSTEM", DateTime? now = null)
    {
        var lookup = LookupTransaction(receiptNumber, now);
        if (lookup.RefundBlockedReason is not null) throw new InvalidOperationException(lookup.RefundBlockedReason);
        var lines = lookup.Lines.Where(l => l.Remaining > 0)
            .Select(l => new RefundRequestLineDTO { SaleItemId = l.SaleItemId, Quantity = l.Remaining }).ToList();
        return ExecuteRefund(receiptNumber, lines, actor, null, now);
    }

    public RefundResultDTO ExecuteRefund(string receiptNumber, IReadOnlyList<RefundRequestLineDTO> lines, string actor, string? reason = null, DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber)) throw new ArgumentException("Receipt barcode cannot be empty.");
        if (lines is null || lines.Count == 0 || lines.All(l => l.Quantity == 0)) throw new ArgumentException("Choose at least one item quantity to refund.");
        if (lines.Any(l => l.Quantity < 0)) throw new ArgumentException("Refund quantities cannot be negative.");
        if (lines.Any(l => l.Disposition is not ("Restock" or "Damaged"))) throw new ArgumentException("Choose Return to stock or Damaged for each item.");
        if (reason?.Length > 200) throw new ArgumentException("Keep the reason under 200 characters.");
        return Guard(receiptNumber, () => unit.Run(() =>
        {
            var at = now ?? DateTime.Now;
            var sale = salesRepo.LoadForRefund(receiptNumber.Trim()) ?? throw new InvalidOperationException("Transaction not found.");
            var blocked = Eligibility(sale, at);
            if (blocked is not null) throw new InvalidOperationException(blocked);
            var refund = new Refund { Kind = "Refund", RefundedAt = at, Actor = Clip(actor), Reason = reason?.Trim() };
            foreach (var request in lines.Where(l => l.Quantity > 0))
            {
                var item = sale.SaleItems.SingleOrDefault(i => i.SaleItemId == request.SaleItemId)
                    ?? throw new InvalidOperationException("An item is not part of this sale.");
                var refundedBefore = sale.Refunds.SelectMany(r => r.Items).Where(r => r.SaleItemId == item.SaleItemId).ToList();
                var remaining = item.Quantity - refundedBefore.Sum(r => r.Quantity);
                if (request.Quantity > remaining) throw new InvalidOperationException($"Cannot refund more than was sold ({remaining} left of {item.Product?.ProductName}).");
                // The last units take whatever is left of the line, so partial refunds always add up exactly.
                var amount = request.Quantity == remaining
                    ? Net(item) - refundedBefore.Sum(r => r.Amount)
                    : Money.R2(Net(item) * request.Quantity / item.Quantity);
                refund.Items.Add(new RefundItem
                {
                    SaleItemId = item.SaleItemId, ProductId = item.ProductId, Quantity = request.Quantity,
                    Amount = amount, CostAtSale = item.CostAtSale, Disposition = request.Disposition
                });
            }
            Settle(sale, refund, at);
            // Settle added this refund to sale.Refunds.
            var fully = sale.SaleItems.All(i => i.Quantity == sale.Refunds.SelectMany(r => r.Items).Where(r => r.SaleItemId == i.SaleItemId).Sum(r => r.Quantity));
            sale.OrderStatus = fully ? "Refunded" : "PartiallyRefunded";
            return Finish(sale, refund, "Refund");
        }));
    }

    /// <summary>PR-Q87/CQ-11: Manager/Admin void of a whole completed sale within 5 minutes, with a reason.</summary>
    public RefundResultDTO VoidSale(string receiptNumber, string reason, string actor, DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Enter the reason for the void.");
        if (reason.Length > 200) throw new ArgumentException("Keep the reason under 200 characters.");
        return Guard(receiptNumber, () => unit.Run(() =>
        {
            var at = now ?? DateTime.Now;
            var sale = salesRepo.LoadForRefund(receiptNumber.Trim()) ?? throw new InvalidOperationException("Transaction not found.");
            if (sale.OrderStatus != "Completed" || sale.Refunds.Count > 0) throw new InvalidOperationException("Only an untouched completed sale can be voided. Use a refund.");
            if (at - sale.SaleDate > TimeSpan.FromMinutes(VoidWindowMinutes)) throw new InvalidOperationException($"The {VoidWindowMinutes}-minute void window has passed. Use a refund.");
            var refund = new Refund { Kind = "Void", RefundedAt = at, Actor = Clip(actor), Reason = reason.Trim() };
            foreach (var item in sale.SaleItems)
                refund.Items.Add(new RefundItem
                {
                    SaleItemId = item.SaleItemId, ProductId = item.ProductId, Quantity = item.Quantity,
                    Amount = Net(item), CostAtSale = item.CostAtSale, Disposition = "Restock"
                });
            Settle(sale, refund, at, isVoid: true);
            sale.OrderStatus = "Voided";
            return Finish(sale, refund, "Void");
        }));
    }

    // --- rules -----------------------------------------------------------------------------------------------

    private static string? Eligibility(Sale sale, DateTime at)
    {
        if (sale.OrderStatus == "Refunded") return "This transaction has already been refunded.";
        if (sale.OrderStatus == "Voided") return "This sale was voided.";
        if (sale.OrderStatus is not ("Completed" or "PartiallyRefunded")) return "Only completed sales can be refunded.";
        if (at - sale.SaleDate > TimeSpan.FromHours(RefundWindowHours)) return "The 7-day refund window has passed (PR-Q27).";
        return null;
    }

    /// <summary>Money actually paid for the line: shelf price x qty minus its saved discount (falls back to LineTotal).</summary>
    private static decimal Net(SaleItem item) =>
        item.DiscountAmount > 0 ? item.UnitPrice * item.Quantity - item.DiscountAmount : item.LineTotal;

    private void Settle(Sale sale, Refund refund, DateTime at, bool isVoid = false)
    {
        refund.Amount = refund.Items.Sum(i => i.Amount);
        refund.CostReversed = Money.R2(refund.Items.Sum(i => i.CostAtSale * i.Quantity));
        refund.VatReversed = sale.TotalAmount > 0 ? Money.R2(sale.TaxAmount * refund.Amount / sale.TotalAmount) : 0; // PR-Q30

        // CQ-05/18: cancel this sale's still-unpaid Due first.
        var due = sale.Payments.Where(p => p.Tender == "Due").Sum(p => p.Amount);
        var dueOpen = due - sale.Refunds.Sum(r => r.DueCancelled);
        var unpaid = Math.Max(0, Math.Min(dueOpen, sale.Customer?.DueBalance ?? 0));
        refund.DueCancelled = Math.Min(refund.Amount, unpaid);
        if (refund.DueCancelled > 0) sale.Customer!.DueBalance -= refund.DueCancelled;

        // Money actually received for this sale, per tender (collected Due counted as Cash).
        var received = sale.Payments.Where(p => p.Tender != "Due").GroupBy(p => p.Tender)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        if (sale.Payments.Count == 0 && sale.PaymentMethod != "Due") received[sale.PaymentMethod] = sale.TotalAmount + sale.RoundingAmount; // legacy rows
        var collectedDue = Math.Max(0, dueOpen - unpaid);
        if (collectedDue > 0) received["Cash"] = received.GetValueOrDefault("Cash") + collectedDue;
        foreach (var prior in sale.Refunds.SelectMany(r => r.Payouts))
            if (received.ContainsKey(prior.Tender)) received[prior.Tender] = Math.Max(0, received[prior.Tender] - prior.Amount);

        // CQ-23: cap at money received first, then split in proportion; residual to Cash, else first tender.
        var available = received.Values.Sum();
        var payout = isVoid ? available : Math.Min(refund.Amount - refund.DueCancelled, available);
        if (payout > 0)
        {
            var tenders = received.Where(r => r.Value > 0).ToList();
            var parts = tenders.Select(t => Money.R2(payout * t.Value / available)).ToArray();
            var residual = payout - parts.Sum();
            if (residual != 0)
            {
                var k = tenders.FindIndex(t => t.Key == "Cash");
                parts[k >= 0 ? k : 0] += residual;
            }
            for (var i = 0; i < tenders.Count; i++)
            {
                var amount = parts[i];
                if (tenders[i].Key == "Cash" && !isVoid)
                {
                    var rounded = Money.WholeTaka(amount);
                    if (rounded > tenders[i].Value) rounded = Math.Floor(amount); // never pay out more cash than received
                    refund.PayoutRounding = rounded - amount;
                    amount = rounded;
                }
                if (amount > 0) refund.Payouts.Add(new RefundPayout { Tender = tenders[i].Key, Amount = amount });
            }
        }

        // Stock and ledger (PR-Q29/65/66, CQ-06): reverse COGS for every returned unit; damaged units are then
        // expensed as Damaged at their original cost. Money paid back is one "Refund" outflow (counted once, CQ-07).
        foreach (var item in refund.Items.Where(i => i.Disposition == "Restock"))
        {
            var product = salesRepo.FindProduct(item.ProductId) ?? throw new InvalidOperationException("A refunded product no longer exists.");
            product.StockQuantity += item.Quantity;
        }
        var reference = $"{refund.Kind} of {sale.ReceiptNumber}";
        var totalPaidBack = refund.Payouts.Sum(p => p.Amount);
        if (totalPaidBack > 0) salesRepo.AddLedger(Entry("Refund", "Outflow", totalPaidBack, $"{reference}: paid back {string.Join(", ", refund.Payouts.Select(p => $"{p.Tender} {p.Amount:0.00}"))}", refund.Actor, at));
        if (refund.CostReversed > 0) salesRepo.AddLedger(Entry("COGS", "Inflow", refund.CostReversed, $"{reference}: COGS reversal", refund.Actor, at));
        var damaged = Money.R2(refund.Items.Where(i => i.Disposition == "Damaged").Sum(i => i.CostAtSale * i.Quantity));
        if (damaged > 0) salesRepo.AddLedger(Entry("Damaged", "Outflow", damaged, $"{reference}: returned damaged goods", refund.Actor, at));

        sale.RefundedAmount += refund.Amount; // always changes the row, so its RowVersion blocks a concurrent repeat
        sale.Refunds.Add(refund);
    }

    private RefundResultDTO Finish(Sale sale, Refund refund, string action)
    {
        unit.Audit(refund.Actor, action, "Sale", sale.ReceiptNumber, refund.Reason,
            $"amount {refund.Amount:0.00}; due cancelled {refund.DueCancelled:0.00}; paid back {refund.Payouts.Sum(p => p.Amount):0.00}; items {string.Join(", ", refund.Items.Select(i => $"{i.ProductId}x{i.Quantity} {i.Disposition}"))}");
        return new RefundResultDTO
        {
            Amount = refund.Amount, DueCancelled = refund.DueCancelled, PayoutRounding = refund.PayoutRounding,
            Payouts = refund.Payouts.Select(p => new ResolvedTenderDTO { Tender = p.Tender, Amount = p.Amount }).ToList()
        };
    }

    private RefundResultDTO Guard(string receiptNumber, Func<RefundResultDTO> action)
    {
        try { return action(); }
        catch (DbUpdateConcurrencyException)
        {
            // Another refund/void of this sale committed first; nothing of this attempt was saved.
            var current = salesRepo.LoadForRefund(receiptNumber.Trim());
            throw new InvalidOperationException(current?.OrderStatus == "Refunded"
                ? "This transaction has already been refunded."
                : "The sale was changed by another action. Look it up again and retry.");
        }
    }

    private static LedgerEntry Entry(string category, string type, decimal amount, string description, string actor, DateTime at) => new()
    {
        TransactionDate = at, Category = category, TransactionType = type, Amount = amount,
        Description = description.Length <= 500 ? description : description[..500], RecordedBy = actor
    };

    private static string Clip(string actor) => actor.Length <= 100 ? actor : actor[..100];
}
