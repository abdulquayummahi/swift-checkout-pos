using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories;

public sealed class ShiftRepository(ApplicationDbContext context)
{
    public Shift? OpenShift() => context.Shifts.Include(s => s.CashMovements).SingleOrDefault(s => s.Status == "Open");
    /// <summary>Light query for the page header: when the open shift started and who opened it.</summary>
    public (DateTime OpenedAt, string OpenedBy)? OpenShiftHeader() =>
        context.Shifts.AsNoTracking().Where(s => s.Status == "Open").Select(s => new { s.OpenedAt, s.OpenedBy })
            .AsEnumerable().Select(s => ((DateTime, string)?)(s.OpenedAt, s.OpenedBy)).SingleOrDefault();
    public Shift? Find(int shiftId) => context.Shifts.Include(s => s.CashMovements).SingleOrDefault(s => s.ShiftId == shiftId);
    public void Add(Shift shift) => context.Shifts.Add(shift);
    public void AddMovement(CashMovement movement) => context.CashMovements.Add(movement);
    public IReadOnlyList<Shift> Recent(int take) =>
        context.Shifts.AsNoTracking().OrderByDescending(s => s.ShiftId).Take(take).ToList();

    /// <summary>PR-Q43: cash received for sales completed in the shift (incl. rounding; legacy rows by PaymentMethod).</summary>
    public decimal CashSales(int shiftId) =>
        context.SalePayments.Where(p => p.Tender == "Cash" && p.Sale.ShiftId == shiftId).Sum(p => (decimal?)p.Amount) ?? 0m;

    public decimal CashDueCollections(int shiftId) =>
        context.CustomerDuePayments.Where(p => p.ShiftId == shiftId && p.Tender == "Cash").Sum(p => (decimal?)p.Amount) ?? 0m;

    public decimal CashRefundPayouts(int shiftId) =>
        context.RefundPayouts.Where(p => p.Tender == "Cash" &&
            context.Refunds.Any(r => r.RefundId == p.RefundId && r.ShiftId == shiftId)).Sum(p => (decimal?)p.Amount) ?? 0m;
}
