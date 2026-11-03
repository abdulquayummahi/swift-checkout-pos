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

        public FinanceService(FinanceRepository financeRepo, SalesRepository salesRepo, MetricsService? metrics = null)
        {
            _metrics = metrics;
            _financeRepo = financeRepo;
            _salesRepo = salesRepo;
        }

        private readonly MetricsService? _metrics;

        /// <summary>
        /// T14: Finance page. KPI cards follow the date range only; search filters the ledger table only (PR-Q59).
        /// Lifetime cards ignore the range and are labelled "All time" (PR-Q80).
        /// </summary>
        public FinanceDashboardDTO GetFinanceDashboard(DateTime? startDate, DateTime? endDate, string searchQuery)
        {
            var (from, to) = MetricsService.Days(startDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), endDate ?? DateTime.Today);
            var rawEntries = _financeRepo.GetLedgerByDateRange(from, to.AddTicks(-1)).ToList();
            var periodEntries = rawEntries;
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                rawEntries = rawEntries.Where(e => e.Category.ToLower().Contains(q) || (e.Description != null && e.Description.ToLower().Contains(q))).ToList();
            }
            var metrics = _metrics ?? throw new InvalidOperationException("Metrics are not configured.");
            var period = metrics.Period(from, to);
            var life = metrics.Lifetime();
            return new FinanceDashboardDTO
            {
                PeriodFrom = from, PeriodTo = to.AddDays(-1),
                TotalCapitalInflow = periodEntries.Where(e => e.TransactionType == "Inflow" && (e.Category == "Capital Investment" || e.Category == "Capital")).Sum(e => e.Amount),
                InventoryCosts = periodEntries.Where(e => e.TransactionType == "Outflow" && e.Category == "Inventory").Sum(e => e.Amount),
                OperatingExpenses = period.OperatingExpenses,
                NetLedgerBalance = life.NetCashPosition,
                Period = period,
                Lifetime = life,
                Entries = rawEntries.Select(e => new LedgerEntryDTO
                {
                    EntryId = e.EntryId, TransactionDate = e.TransactionDate, Category = e.Category,
                    TransactionType = e.TransactionType, Amount = e.Amount, Description = e.Description, RecordedBy = e.RecordedBy
                }).ToList(),
                LifetimeCapital = life.CapitalInvested,
                LifetimeNetProfit = life.NetProfit,
                AverageMonthlyProfit = life.AverageMonthlyProfit,
                PaybackPercentage = life.PaybackPercent,
                PaybackEstimate = life.PaybackEstimate
            };
        }

        /// <summary>PR-Q92: a Manager without financial view sees only the expenses they recorded today.</summary>
        public List<LedgerEntryDTO> GetOwnEntriesToday(string actor) =>
            _financeRepo.GetLedgerByDateRange(DateTime.Today, DateTime.Today.AddDays(1).AddTicks(-1))
                .Where(e => e.RecordedBy == actor)
                .Select(e => new LedgerEntryDTO { EntryId = e.EntryId, TransactionDate = e.TransactionDate, Category = e.Category,
                    TransactionType = e.TransactionType, Amount = e.Amount, Description = e.Description, RecordedBy = e.RecordedBy }).ToList();

        /// <summary>
        /// PR-Q33: manual entries use the approved category list; the type is fixed by the category.
        /// System categories (Inventory, COGS, Inventory Adjustment, Damaged, Refund) are never manual.
        /// <paramref name="expensesOnly"/>: Manager without financial view may record operating expenses only (PR-Q92).
        /// </summary>
        public void RecordManualEntry(string category, string type, decimal amount, string description, string user, bool expensesOnly = false)
        {
            category = category?.Trim() ?? "";
            if (amount <= 0 || Pricing.Money.R2(amount) != amount) throw new ArgumentException("Amount must be greater than zero with at most 2 decimals.");
            var expectedType = MetricsService.ManualInflow.Contains(category) ? "Inflow"
                : MetricsService.ManualOutflow.Contains(category) ? "Outflow"
                : throw new InvalidOperationException("Choose one of the approved categories. Inventory, COGS, adjustments and refunds are recorded automatically.");
            if (!string.IsNullOrEmpty(type) && type != expectedType) throw new ArgumentException($"{category} is always an {expectedType}.");
            if (expensesOnly && !MetricsService.OpexCategories.Contains(category)) throw new InvalidOperationException("You can record operating expenses only.");
            if (description?.Length > 500) throw new ArgumentException("Keep the description under 500 characters.");
            _financeRepo.AddEntry(new LedgerEntry
            {
                TransactionDate = DateTime.Now, Category = category, TransactionType = expectedType, Amount = amount,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(), RecordedBy = user
            });
        }

        public void RecordSystemInventoryCost(decimal totalCost, string referenceId, string actor = "SYSTEM") =>
            RecordSystemEntry("Inventory", "Outflow", totalCost, $"System Auto-Record: Inventory Restock. Ref: {referenceId}", actor);

        public void RecordSystemVendorRefund(decimal refundAmount, string referenceId, string actor = "SYSTEM") =>
            RecordSystemEntry("Inventory", "Inflow", refundAmount, $"System Auto-Record: Vendor RTV Refund. Ref: {referenceId}", actor);

        /// <summary>System-only ledger entry (Inventory, COGS, Inventory Adjustment, Damaged). Stage inside an AtomicUnit.</summary>
        public void RecordSystemEntry(string category, string type, decimal amount, string description, string actor)
        {
            if (amount <= 0) return;
            _financeRepo.AddEntry(new LedgerEntry
            {
                TransactionDate = DateTime.Now, Category = category, TransactionType = type, Amount = amount,
                Description = description.Length <= 500 ? description : description[..500],
                RecordedBy = actor.Length <= 100 ? actor : actor[..100]
            });
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