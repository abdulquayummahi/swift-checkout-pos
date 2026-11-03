using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Pricing;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services;

public sealed class ShiftSummaryDTO
{
    public int ShiftId { get; set; }
    public string Status { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public string OpenedBy { get; set; } = "";
    public decimal OpeningFloat { get; set; }
    public decimal CashSales { get; set; }
    public decimal CashDueCollections { get; set; }
    public decimal CashRefunds { get; set; }
    public decimal PaidIn { get; set; }
    public decimal PaidOut { get; set; }
    public decimal ExpectedCash { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Difference { get; set; }
    public string? DifferenceApprovedBy { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// BP-19 — explicit till shifts (PR-Q41–44, PR-Q47, PR-Q88, CQ-08/12).
/// Expected cash = opening float + cash sales + cash due collections − cash refunds + paid in − paid out, for this shift.
/// Counting is blind: the cashier enters the count before seeing expected cash; only Manager/Admin see the figures.
/// </summary>
public sealed class ShiftService(ShiftRepository shifts, AtomicUnit unit, IApprover? approver = null)
{
    public const decimal DifferenceApprovalLimit = 1000m; // PR-Q88
    public static bool CanSeeFigures(string role) => role is "Manager" or "Admin";

    public ShiftSummaryDTO? Current() { var s = shifts.OpenShift(); return s is null ? null : Summarize(s); }
    public IReadOnlyList<ShiftSummaryDTO> Recent(int take = 20) => shifts.Recent(take).Select(s => Summarize(shifts.Find(s.ShiftId)!)).ToList();

    public int Open(decimal openingFloat, string actor)
    {
        if (openingFloat < 0 || Money.R2(openingFloat) != openingFloat) throw new ArgumentException("Enter the opening float in taka (0 or more, max 2 decimals).");
        try
        {
            var opened = unit.Run(() =>
            {
                if (shifts.OpenShift() is not null) throw new InvalidOperationException("A shift is already open. Close it first.");
                var shift = new Shift { Status = "Open", OpenedAt = DateTime.Now, OpenedBy = actor, OpeningFloat = openingFloat };
                shifts.Add(shift);
                unit.Audit(actor, "ShiftOpened", "Shift", "new", details: $"float {openingFloat:0.00}");
                return shift;
            });
            return opened.ShiftId; // assigned by the database on commit
        }
        catch (DbUpdateException) { throw new InvalidOperationException("A shift is already open. Close it first."); }
    }

    /// <summary>PR-Q43: cash paid into or out of the drawer, with a reason.</summary>
    public void RecordCash(string type, decimal amount, string reason, string actor)
    {
        if (type is not ("PaidIn" or "PaidOut")) throw new ArgumentException("Choose Paid in or Paid out.");
        if (amount <= 0 || Money.R2(amount) != amount) throw new ArgumentException("Enter a valid amount.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200) throw new ArgumentException("Enter a reason (max 200 characters).");
        unit.Run(() =>
        {
            var shift = shifts.OpenShift() ?? throw new InvalidOperationException("Open a shift first.");
            shifts.AddMovement(new CashMovement { ShiftId = shift.ShiftId, At = DateTime.Now, Type = type, Amount = amount, Reason = reason.Trim(), Actor = actor });
            unit.Audit(actor, type, "Shift", shift.ShiftId.ToString(), reason.Trim(), $"{amount:0.00}");
        });
    }

    /// <summary>PR-Q47: Manager/Admin may open the drawer without a sale, with a reason; it is logged.</summary>
    public void NoSale(string reason, string actor, string role)
    {
        if (!CanSeeFigures(role)) throw new InvalidOperationException("Only a Manager or Admin can open the drawer without a sale.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200) throw new ArgumentException("Enter a reason (max 200 characters).");
        unit.Run(() =>
        {
            var shift = shifts.OpenShift() ?? throw new InvalidOperationException("Open a shift first.");
            shifts.AddMovement(new CashMovement { ShiftId = shift.ShiftId, At = DateTime.Now, Type = "NoSale", Amount = 0, Reason = reason.Trim(), Actor = actor });
            unit.Audit(actor, "DrawerOpened", "Shift", shift.ShiftId.ToString(), reason.Trim(), "No sale");
        });
    }

    /// <summary>
    /// Blind close. A difference larger than BDT 1,000 (either way, CQ-08) needs Manager/Admin approval
    /// (the signed-in Manager/Admin, or typed approver credentials). Returns the closed shift summary.
    /// </summary>
    public ShiftSummaryDTO Close(decimal countedCash, string? notes, string actor, string role, string? approverUsername = null, string? approverPassword = null)
    {
        if (countedCash < 0 || Money.R2(countedCash) != countedCash) throw new ArgumentException("Enter the counted cash (0 or more, max 2 decimals).");
        if (notes?.Length > 200) throw new ArgumentException("Keep the note under 200 characters.");
        var typed = !string.IsNullOrWhiteSpace(approverUsername) && approver is not null ? approver.Verify(approverUsername!, approverPassword ?? "") : null;
        if (!string.IsNullOrWhiteSpace(approverUsername) && typed is null) throw new InvalidOperationException("Approval failed: wrong approver username or password.");
        var id = unit.Run(() =>
        {
            var shift = shifts.OpenShift() ?? throw new InvalidOperationException("No shift is open.");
            var summary = Summarize(shift);
            var difference = countedCash - summary.ExpectedCash;
            string? approvedBy = null;
            if (Math.Abs(difference) > DifferenceApprovalLimit)
            {
                if (CanSeeFigures(role)) approvedBy = actor;
                else if (typed is { } t && CanSeeFigures(t.Role)) approvedBy = t.Actor;
                else throw new InvalidOperationException("APPROVAL REQUIRED: the count differs from the expected cash by more than BDT 1,000. A Manager or Admin must approve the close.");
            }
            shift.Status = "Closed"; shift.ClosedAt = DateTime.Now; shift.ClosedBy = actor;
            shift.CountedCash = countedCash; shift.ExpectedCash = summary.ExpectedCash; shift.Difference = difference;
            shift.DifferenceApprovedBy = approvedBy; shift.Notes = notes?.Trim();
            unit.Audit(actor, "ShiftClosed", "Shift", shift.ShiftId.ToString(), notes?.Trim(),
                $"expected {summary.ExpectedCash:0.00}; counted {countedCash:0.00}; difference {difference:0.00}" + (approvedBy is null ? "" : $"; approved by {approvedBy}"));
            return shift.ShiftId;
        });
        return Summarize(shifts.Find(id)!);
    }

    private ShiftSummaryDTO Summarize(Shift s)
    {
        var dto = new ShiftSummaryDTO
        {
            ShiftId = s.ShiftId, Status = s.Status, OpenedAt = s.OpenedAt, OpenedBy = s.OpenedBy, OpeningFloat = s.OpeningFloat,
            CashSales = shifts.CashSales(s.ShiftId), CashDueCollections = shifts.CashDueCollections(s.ShiftId),
            CashRefunds = shifts.CashRefundPayouts(s.ShiftId),
            PaidIn = s.CashMovements.Where(m => m.Type == "PaidIn").Sum(m => m.Amount),
            PaidOut = s.CashMovements.Where(m => m.Type == "PaidOut").Sum(m => m.Amount),
            ClosedAt = s.ClosedAt, ClosedBy = s.ClosedBy, CountedCash = s.CountedCash, Difference = s.Difference,
            DifferenceApprovedBy = s.DifferenceApprovedBy, Notes = s.Notes
        };
        dto.ExpectedCash = s.ExpectedCash ?? dto.OpeningFloat + dto.CashSales + dto.CashDueCollections - dto.CashRefunds + dto.PaidIn - dto.PaidOut;
        return dto;
    }
}
