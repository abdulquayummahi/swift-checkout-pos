using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities;

/// <summary>BP-17/PR-Q86: money received against a customer's due balance.</summary>
public class CustomerDuePayment
{
    [Key] public int CustomerDuePaymentId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime ReceivedAt { get; set; }
    public int? ShiftId { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal Amount { get; set; }
    [MaxLength(20)] public string Tender { get; set; } = null!;
    [MaxLength(50)] public string? Reference { get; set; }
    [MaxLength(100)] public string Actor { get; set; } = null!;
}
