using System;
using System.Linq;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class ReconciliationService
    {
        private readonly ApplicationDbContext _context;

        public ReconciliationService(ApplicationDbContext context) => _context = context;

        public TillReconciliation PerformBlindReconciliation(int userId, decimal countedAmount, string notes)
        {
            // 1. Fetch expected cash from sales since last reconciliation
            var expected = _context.Sales
                .Where(s => s.PaymentMethod == "Cash" && s.OrderStatus == "Completed")
                .Sum(s => s.TotalAmount);

            var reconciliation = new TillReconciliation
            {
                UserId = userId,
                OpenTime = DateTime.Now.AddHours(-8), // Simplification: 8-hour shift
                CloseTime = DateTime.Now,
                ExpectedCash = expected,
                CountedCash = countedAmount,
                Notes = notes
            };

            _context.TillReconciliations.Add(reconciliation);
            _context.SaveChanges();

            return reconciliation; // UI shows the discrepancy only AFTER submission
        }
    }
}