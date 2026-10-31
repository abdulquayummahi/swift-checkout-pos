// Project: SwiftCheckoutPOS.BLL
// File: Services/FinanceService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class FinanceService
    {
        private readonly FinanceRepository _financeRepo;
        private readonly SalesRepository _salesRepo;

        public FinanceService(FinanceRepository financeRepo, SalesRepository salesRepo)
        {
            _financeRepo = financeRepo;
            _salesRepo = salesRepo;
        }

        public FinanceDashboardDTO GetFinanceDashboard(DateTime? startDate, DateTime? endDate, string searchQuery)
        {
            // --- 1. PERIODIC FILTERED DATA (For the Data Grid and KPI Cards) ---
            DateTime start = startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime end = endDate ?? DateTime.Today;

            if (start > end) { var temp = start; start = end; end = temp; }
            start = start.Date; end = end.Date.AddDays(1).AddTicks(-1);

            var rawEntries = _financeRepo.GetLedgerByDateRange(start, end).ToList();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                rawEntries = rawEntries.Where(e =>
                    e.Category.ToLower().Contains(q) ||
                    (e.Description != null && e.Description.ToLower().Contains(q))
                ).ToList();
            }

            var dtos = rawEntries.Select(e => new LedgerEntryDTO
            {
                EntryId = e.EntryId,
                TransactionDate = e.TransactionDate,
                Category = e.Category,
                TransactionType = e.TransactionType,
                Amount = Math.Round(e.Amount, 2),
                Description = e.Description,
                RecordedBy = e.RecordedBy
            }).ToList();

            decimal capital = rawEntries.Where(e => e.TransactionType == "Inflow" && e.Category == "Capital").Sum(e => e.Amount);
            decimal inventoryCash = rawEntries.Where(e => e.TransactionType == "Outflow" && (e.Category == "Inventory" || e.Category == "Damaged")).Sum(e => e.Amount);
            decimal opex = rawEntries.Where(e => e.TransactionType == "Outflow" && e.Category != "Inventory" && e.Category != "Damaged" && e.Category != "COGS").Sum(e => e.Amount);

            var dto = new FinanceDashboardDTO
            {
                TotalCapitalInflow = Math.Round(capital, 2),
                InventoryCosts = Math.Round(inventoryCash, 2),
                OperatingExpenses = Math.Round(opex, 2),
                NetLedgerBalance = Math.Round(capital - (inventoryCash + opex), 2),
                Entries = dtos
            };

            // --- 2. LIFETIME BREAK-EVEN POINT (BEP) CALCULATION ENGINE ---

            // ARCHITECT FIX: Establish SQL-safe epoch dates to prevent DATETIME boundary overflow
            DateTime sqlSafeMinDate = new DateTime(1753, 1, 1);
            DateTime sqlSafeMaxDate = new DateTime(9999, 12, 31);

            var lifetimeLedger = _financeRepo.GetLedgerByDateRange(sqlSafeMinDate, sqlSafeMaxDate).ToList();
            var lifetimeSales = _salesRepo.GetSalesByDateRange(sqlSafeMinDate, sqlSafeMaxDate).Where(s => s.OrderStatus == "Completed").ToList();

            dto.LifetimeCapital = lifetimeLedger.Where(e => e.TransactionType == "Inflow" && e.Category == "Capital").Sum(e => e.Amount);

            decimal lifetimeRevenue = lifetimeSales.Sum(s => s.TotalAmount);
            decimal lifetimeOpex = lifetimeLedger.Where(e => e.TransactionType == "Outflow" && e.Category != "Inventory" && e.Category != "Damaged" && e.Category != "Capital" && e.Category != "COGS").Sum(e => e.Amount);
            decimal lifetimeCOGS = lifetimeLedger.Where(e => e.TransactionType == "Outflow" && e.Category == "COGS").Sum(e => e.Amount);

            dto.LifetimeNetProfit = lifetimeRevenue - lifetimeCOGS - lifetimeOpex;

            // Determine business lifespan to find precise monthly averages
            var firstCapitalDate = lifetimeLedger.Where(e => e.Category == "Capital").OrderBy(e => e.TransactionDate).FirstOrDefault()?.TransactionDate;
            var firstSaleDate = lifetimeSales.OrderBy(s => s.SaleDate).FirstOrDefault()?.SaleDate;

            DateTime businessStartDate = firstCapitalDate ?? (firstSaleDate ?? DateTime.Today);
            int monthsActive = ((DateTime.Today.Year - businessStartDate.Year) * 12) + DateTime.Today.Month - businessStartDate.Month;
            if (monthsActive <= 0) monthsActive = 1; // Safeguard against divide-by-zero for brand new businesses

            dto.AverageMonthlyProfit = dto.LifetimeNetProfit > 0 ? (dto.LifetimeNetProfit / monthsActive) : 0;

            if (dto.LifetimeCapital > 0)
            {
                dto.PaybackPercentage = (dto.LifetimeNetProfit / dto.LifetimeCapital) * 100;
                if (dto.PaybackPercentage < 0) dto.PaybackPercentage = 0;
                if (dto.PaybackPercentage > 100) dto.PaybackPercentage = 100;

                decimal remainingCapital = dto.LifetimeCapital - dto.LifetimeNetProfit;
                if (remainingCapital > 0 && dto.AverageMonthlyProfit > 0)
                {
                    dto.EstimatedMonthsToBreakEven = (int)Math.Ceiling(remainingCapital / dto.AverageMonthlyProfit);
                }
                else
                {
                    dto.EstimatedMonthsToBreakEven = 0; // Target Reached or Infinite (if unprofitable)
                }
            }

            return dto;
        }

        public void RecordManualEntry(string category, string type, decimal amount, string description, string user)
        {
            if (amount <= 0) throw new ArgumentException("Amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.");

            if (category.Equals("Inventory", StringComparison.OrdinalIgnoreCase) ||
                category.Equals("COGS", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("System Security: Inventory and COGS records are strictly automated through the POS and Master Inventory. Manual overrides are disabled to preserve structural integrity.");
            }

            var entry = new LedgerEntry
            {
                TransactionDate = DateTime.Now,
                Category = category.Trim(),
                TransactionType = type == "Inflow" ? "Inflow" : "Outflow",
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                RecordedBy = user
            };

            _financeRepo.AddEntry(entry);
        }

        public void RecordSystemInventoryCost(decimal totalCost, string referenceId)
        {
            var entry = new LedgerEntry
            {
                TransactionDate = DateTime.Now,
                Category = "Inventory",
                TransactionType = "Outflow",
                Amount = totalCost,
                Description = $"System Auto-Record: Inventory Restock. Ref: {referenceId}",
                RecordedBy = "SYSTEM"
            };
            _financeRepo.AddEntry(entry);
        }

        public void RecordSystemVendorRefund(decimal refundAmount, string referenceId)
        {
            var entry = new LedgerEntry
            {
                TransactionDate = DateTime.Now,
                Category = "Inventory",
                TransactionType = "Inflow",
                Amount = refundAmount,
                Description = $"System Auto-Record: Vendor RTV Refund. Ref: {referenceId}",
                RecordedBy = "SYSTEM"
            };
            _financeRepo.AddEntry(entry);
        }

        public void RecordSystemCOGS(decimal cogsAmount, string receiptNumber)
        {
            if (cogsAmount <= 0) return;

            var entry = new LedgerEntry
            {
                TransactionDate = DateTime.Now,
                Category = "COGS",
                TransactionType = "Outflow",
                Amount = cogsAmount,
                Description = $"System Auto-Record: Cost of Goods Sold. Ref: {receiptNumber}",
                RecordedBy = "SYSTEM"
            };
            _financeRepo.AddEntry(entry);
        }
    }
}