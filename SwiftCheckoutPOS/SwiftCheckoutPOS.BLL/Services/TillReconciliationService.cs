// Project: SwiftCheckoutPOS.BLL
// File: Services/TillReconciliationService.cs
using System;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class TillReconciliationService
    {
        private readonly TillReconciliationRepository _tillRepo;
        private readonly SalesRepository _salesRepo;

        // Standard starting float for a supermarket till (e.g., 5,000 BDT in small change)
        private const decimal DefaultOpeningFloat = 5000.00m;

        public TillReconciliationService(TillReconciliationRepository tillRepo, SalesRepository salesRepo)
        {
            _tillRepo = tillRepo;
            _salesRepo = salesRepo;
        }

        public ReconciliationDTO GenerateCurrentShiftPreview()
        {
            var lastReconciliation = _tillRepo.GetLatestReconciliation();

            // ARCHITECT FIX: Map to CloseTime
            DateTime shiftStart = lastReconciliation?.CloseTime ?? DateTime.Today;

            // Calculate Expected Cash
            var recentSales = _salesRepo.GetAllSales()
                .Where(s => s.SaleDate >= shiftStart && s.PaymentMethod == "Cash")
                .ToList();

            decimal totalCashSales = recentSales.Sum(s => s.TotalAmount);
            decimal expected = DefaultOpeningFloat + totalCashSales;

            return new ReconciliationDTO
            {
                ShiftStartTime = shiftStart,
                OpeningFloat = DefaultOpeningFloat,
                TotalCashSales = totalCashSales,
                ExpectedCash = expected
            };
        }

        public void CommitReconciliation(int userId, decimal actualCashCount, string? notes)
        {
            var preview = GenerateCurrentShiftPreview();

            // ARCHITECT FIX: Property mappings explicitly aligned with your exact entity definitions.
            var reconciliation = new TillReconciliation
            {
                UserId = userId,
                OpenTime = preview.ShiftStartTime,
                CloseTime = DateTime.Now,
                ExpectedCash = preview.ExpectedCash,
                CountedCash = actualCashCount,
                // Discrepancy is computed automatically by your entity, so we do not assign it.

                // We inject the opening float into the notes for permanent auditing since it lacks a dedicated column.
                Notes = $"[Float: BDT {preview.OpeningFloat:N0}] {(notes ?? string.Empty)}"
            };

            _tillRepo.AddReconciliation(reconciliation);
        }
    }
}