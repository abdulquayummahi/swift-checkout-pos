using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities;

/// <summary>BP-19 (PR-Q41–44, PR-Q88): one explicit till shift. Only one may be Open at a time.</summary>
public class Shift
{
    [Key] public int ShiftId { get; set; }
    [MaxLength(10)] public string Status { get; set; } = "Open"; // Open | Closed
    public DateTime OpenedAt { get; set; }
    [MaxLength(100)] public string OpenedBy { get; set; } = null!;
    [Column(TypeName = "decimal(18, 2)")] public decimal OpeningFloat { get; set; }
    public DateTime? ClosedAt { get; set; }
    [MaxLength(100)] public string? ClosedBy { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal? CountedCash { get; set; }
    [Column(TypeName = "decimal(18, 2)")] public decimal? ExpectedCash { get; set; }
    /// <summary>Counted minus expected: positive = over, negative = short.</summary>
    [Column(TypeName = "decimal(18, 2)")] public decimal? Difference { get; set; }
    [MaxLength(100)] public string? DifferenceApprovedBy { get; set; }
    [MaxLength(200)] public string? Notes { get; set; }
    public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
}

/// <summary>PR-Q43/47: cash paid in or out of the drawer, or a "No sale" drawer opening, during a shift.</summary>
public class CashMovement
{
    [Key] public int CashMovementId { get; set; }
    public int ShiftId { get; set; }
    public DateTime At { get; set; }
    [MaxLength(10)] public string Type { get; set; } = null!; // PaidIn | PaidOut | NoSale
    [Column(TypeName = "decimal(18, 2)")] public decimal Amount { get; set; }
    [MaxLength(200)] public string Reason { get; set; } = null!;
    [MaxLength(100)] public string Actor { get; set; } = null!;
}
