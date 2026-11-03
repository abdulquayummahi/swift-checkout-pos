namespace SwiftCheckoutPOS.BLL.DTOs;

public sealed class RefundLineDTO
{
    public int SaleItemId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public int Refunded { get; set; }
    public int Remaining => Quantity - Refunded;
    public decimal UnitPrice { get; set; }
    public decimal LineNet { get; set; }
}

public sealed class RefundLookupDTO
{
    public string ReceiptNumber { get; set; } = "";
    public DateTime SaleDate { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string OrderStatus { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? CustomerName { get; set; }
    public List<RefundLineDTO> Lines { get; set; } = new();
    public bool CanRefund { get; set; }
    public string? RefundBlockedReason { get; set; }
    public bool CanVoid { get; set; }
}

public sealed class RefundRequestLineDTO
{
    public int SaleItemId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Restock or Damaged (PR-Q29).</summary>
    public string Disposition { get; set; } = "Restock";
}

public sealed class RefundResultDTO
{
    public decimal Amount { get; set; }
    public decimal DueCancelled { get; set; }
    public decimal PayoutRounding { get; set; }
    public List<ResolvedTenderDTO> Payouts { get; set; } = new();
    public string Summary => string.Join(", ", Payouts.Select(p => $"{p.Tender} {p.Amount:N2}"));
}
