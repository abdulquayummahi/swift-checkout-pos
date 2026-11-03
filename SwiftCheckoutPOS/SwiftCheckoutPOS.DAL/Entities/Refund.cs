using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities;

/// <summary>
/// BP-04/IMM-07: a refund or void EVENT (PR-Q31: reported on RefundedAt; the original sale is never rewritten).
/// Amount = exact refund value; payouts = money actually paid back per tender; DueCancelled = unpaid due removed.
/// </summary>
public class Refund
{
    [Key] public int RefundId { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    [MaxLength(10)] public string Kind { get; set; } = "Refund"; // Refund | Void
    public DateTime RefundedAt { get; set; }
    public int? ShiftId { get; set; }
    [MaxLength(100)] public string Actor { get; set; } = null!;
    [MaxLength(200)] public string? Reason { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal Amount { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal VatReversed { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal DueCancelled { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal CostReversed { get; set; }
    /// <summary>Whole-taka rounding on the cash payout (CQ-16).</summary>
    [Column(TypeName = "decimal(18, 2)")] public decimal PayoutRounding { get; set; }
    public ICollection<RefundItem> Items { get; set; } = new List<RefundItem>();
    public ICollection<RefundPayout> Payouts { get; set; } = new List<RefundPayout>();
}

public class RefundItem
{
    [Key] public int RefundItemId { get; set; }
    public int RefundId { get; set; }
    public int SaleItemId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal Amount { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal CostAtSale { get; set; }
    /// <summary>PR-Q29: Restock (default) or Damaged.</summary>
    [MaxLength(10)] public string Disposition { get; set; } = "Restock";
}

public class RefundPayout
{
    [Key] public int RefundPayoutId { get; set; }
    public int RefundId { get; set; }
    [MaxLength(20)] public string Tender { get; set; } = null!;
    [Column(TypeName = "decimal(18, 2)")] public decimal Amount { get; set; }
}
