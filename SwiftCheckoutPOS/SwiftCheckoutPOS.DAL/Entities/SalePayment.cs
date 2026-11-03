using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities;

/// <summary>BP-17/PR-Q85: one tender of a sale (Cash, Card, bKash, Nagad or Due). Cash amount = money received incl. rounding.</summary>
public class SalePayment
{
    [Key] public int SalePaymentId { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    [MaxLength(20)] public string Tender { get; set; } = null!;
    [Column(TypeName = "decimal(18, 2)")] public decimal Amount { get; set; }
    /// <summary>Cash handed over by the customer (for change), if entered.</summary>
    [Column(TypeName = "decimal(18, 2)")] public decimal? Tendered { get; set; }
    /// <summary>PR-Q46: last 4 card digits or bKash/Nagad TrxID, optional.</summary>
    [MaxLength(50)] public string? Reference { get; set; }
}
