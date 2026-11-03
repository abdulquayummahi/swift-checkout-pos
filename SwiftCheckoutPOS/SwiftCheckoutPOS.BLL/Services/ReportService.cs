// Project: SwiftCheckoutPOS.BLL
// File: Services/ReportService.cs
using System;
using System.Linq;
using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class ReportService
    {
        private readonly SalesRepository _salesRepo;

        public ReportService(SalesRepository salesRepo, MetricsService metrics)
        {
            _salesRepo = salesRepo;
            _metrics = metrics;
        }

        private readonly MetricsService _metrics;

        public ReportDashboardDTO GetAdvancedReport(DateTime? startDate, DateTime? endDate, string searchQuery)
        {
            DateTime appliedStart = startDate ?? DateTime.Today;
            DateTime appliedEnd = endDate ?? DateTime.Today;

            if (appliedStart > appliedEnd)
            {
                var temp = appliedStart;
                appliedStart = appliedEnd;
                appliedEnd = temp;
            }

            DateTime queryStart = appliedStart.Date;
            DateTime queryEnd = appliedEnd.Date.AddDays(1).AddTicks(-1);

            var allSales = _salesRepo.GetSalesByDateRange(queryStart, queryEnd).ToList();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var query = searchQuery.Trim().ToLower();
                allSales = allSales.Where(s => s.ReceiptNumber.ToLower().Contains(query)).ToList();
            }

            var completedSales = allSales.Where(s => (s.OrderStatus == "Completed" || s.OrderStatus == "PartiallyRefunded")).ToList();
            var heldSales = allSales.Where(s => s.OrderStatus == "Held").ToList();
            var refundedSales = allSales.Where(s => s.OrderStatus == "Refunded").ToList();

            decimal totalDiscount = completedSales.Sum(s => s.SubTotal - s.TotalAmount);
            decimal totalCost = completedSales.SelectMany(s => s.SaleItems).Sum(si => si.CostAtSale * si.Quantity);

            var salesDtos = allSales.Select(s => {
                decimal discount = s.SubTotal - s.TotalAmount;
                decimal cost = s.SaleItems.Sum(si => si.CostAtSale * si.Quantity);

                return new SaleDTO
                {
                    SaleId = s.SaleId,
                    ReceiptNumber = s.ReceiptNumber,
                    SaleDate = s.SaleDate,
                    SubTotal = s.SubTotal,
                    TaxAmount = s.TaxAmount,
                    TotalAmount = s.TotalAmount,
                    PaymentMethod = s.PaymentMethod,
                    OrderStatus = s.OrderStatus,
                    CustomerName = s.Customer?.FullName,
                    CustomerPhone = s.Customer?.PhoneNumber,
                    CashierName = s.CashierName,
                    DiscountAmount = discount,
                    EstimatedProfit = s.TotalAmount - s.TaxAmount - cost,
                    SaleItems = s.SaleItems.Select(si => new SaleItemDTO
                    {
                        ProductId = si.ProductId,
                        ProductName = si.Product?.ProductName ?? "Unknown Item",
                        Quantity = si.Quantity,
                        UnitPrice = si.UnitPrice,
                        LineTotal = si.LineTotal
                    }).ToList()
                };
            }).OrderByDescending(s => s.SaleDate).ToList();

            // PR-Q59: KPI cards follow the date range only; the search filters the table only.
            var (from, to) = MetricsService.Days(appliedStart, appliedEnd);
            var m = _metrics.Period(from, to);
            return new ReportDashboardDTO
            {
                AppliedStartDate = appliedStart,
                AppliedEndDate = appliedEnd,
                GrossRevenue = m.NetSales,          // net sales excl. VAT, after refunds (PR-Q36)
                EstimatedProfit = m.GrossProfit,    // at cost of sale (PR-Q37)
                TotalDiscounts = m.Discounts,
                CompletedOrders = m.SalesCount,
                ItemsSold = m.ItemsSold,
                TaxLiabilities = m.Vat,
                HeldTransactions = _metrics.HeldCount(),
                HeldOrdersAmount = _metrics.HeldAmount(),
                RefundedTransactions = m.RefundCount,
                RefundedOrdersAmount = m.RefundsExVat,
                UnknownCostSales = m.UnknownCostSales,
                Transactions = salesDtos
            };
        }

        // Wrapper to protect legacy 1-day endpoints
        public ZReportDTO GenerateDailyZReport(DateTime targetDate)
        {
            return GenerateZReport(targetDate, targetDate);
        }

        // ARCHITECT FIX: Dynamic Range Report Engine
        public ZReportDTO GenerateZReport(DateTime startDate, DateTime endDate)
        {
            var (from, to) = MetricsService.Days(startDate, endDate);
            var m = _metrics.Period(from, to);
            return new ZReportDTO
            {
                ReportDate = endDate.Date,
                StartDate = from,
                EndDate = to.AddTicks(-1),
                TotalTransactions = m.SalesCount,
                NetRevenue = m.NetSales,
                TotalDiscounts = m.Discounts,
                TotalTaxCollected = m.Vat,
                GrossRevenue = m.GrossSalesInclVat,
                RefundsTotal = m.RefundsExVat,
                CashTotal = m.Tenders.GetValueOrDefault("Cash"),
                CardTotal = m.Tenders.GetValueOrDefault("Card"),
                BkashTotal = m.Tenders.GetValueOrDefault("bKash"),
                NagadTotal = m.Tenders.GetValueOrDefault("Nagad"),
                DueTotal = m.Tenders.GetValueOrDefault("Due"),
                MobileWalletTotal = m.Tenders.GetValueOrDefault("Mobile Wallet") + m.Tenders.GetValueOrDefault("bKash") + m.Tenders.GetValueOrDefault("Nagad")
            };
        }

        // ARCHITECT FIX: Virtual Layout Engine reflects dynamic dates and advanced metrics
        public string GenerateZReportText(DateTime startDate, DateTime endDate)
        {
            var report = GenerateZReport(startDate, endDate);
            int paperWidth = 48;
            var sb = new StringBuilder();

            sb.AppendLine(CenterText("SWIFT SUPERMARKET", paperWidth));

            if (startDate.Date == endDate.Date)
                sb.AppendLine(CenterText("END OF DAY Z-REPORT", paperWidth));
            else
                sb.AppendLine(CenterText("PERIOD FINANCIAL REPORT", paperWidth));

            sb.AppendLine(new string('=', paperWidth));

            if (startDate.Date == endDate.Date)
            {
                sb.AppendLine($"Report Date: {report.EndDate:yyyy-MM-dd}");
            }
            else
            {
                sb.AppendLine($"Start Date:  {report.StartDate:yyyy-MM-dd}");
                sb.AppendLine($"End Date:    {report.EndDate:yyyy-MM-dd}");
            }

            sb.AppendLine($"Printed At:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('-', paperWidth));

            sb.AppendLine($"Total Transactions: {report.TotalTransactions}");
            sb.AppendLine($"Sales (incl. VAT): BDT {report.GrossRevenue:N2}");
            sb.AppendLine($"Discounts given:   BDT {report.TotalDiscounts:N2}");
            if (report.TotalTaxCollected != 0) sb.AppendLine($"VAT included:      BDT {report.TotalTaxCollected:N2}");
            sb.AppendLine($"Refunds:          -BDT {report.RefundsTotal:N2}");
            sb.AppendLine(new string('-', paperWidth));
            sb.AppendLine($"Net sales (ex VAT): BDT {report.NetRevenue:N2}");
            sb.AppendLine(new string('=', paperWidth));

            sb.AppendLine("TENDER BREAKDOWN:");
            sb.AppendLine($"Cash:             BDT {report.CashTotal:N2}");
            sb.AppendLine($"Card:             BDT {report.CardTotal:N2}");
            sb.AppendLine($"bKash:            BDT {report.BkashTotal:N2}");
            sb.AppendLine($"Nagad:            BDT {report.NagadTotal:N2}");
            sb.AppendLine($"Due (credit):     BDT {report.DueTotal:N2}");
            sb.AppendLine(new string('=', paperWidth));

            sb.AppendLine(CenterText("REPORT GENERATED SECURELY", paperWidth));

            return sb.ToString();
        }

        private string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return new string(' ', width);
            if (text.Length >= width) return text.Substring(0, width);
            int padding = (width - text.Length) / 2;
            return text.PadLeft(text.Length + padding).PadRight(width);
        }
    }
}