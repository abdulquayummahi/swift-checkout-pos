using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.DAL.Entities;

/// <summary>BP-24: durable record of a sensitive action. Written in the same transaction as the action.</summary>
public class AuditEvent
{
    [Key]
    public long AuditEventId { get; set; }
    public DateTime OccurredAt { get; set; }
    [MaxLength(100)] public string Actor { get; set; } = null!;
    [MaxLength(50)] public string Action { get; set; } = null!;
    [MaxLength(50)] public string EntityType { get; set; } = null!;
    [MaxLength(100)] public string EntityKey { get; set; } = null!;
    [MaxLength(200)] public string? Reason { get; set; }
    [MaxLength(2000)] public string? Details { get; set; }
}
