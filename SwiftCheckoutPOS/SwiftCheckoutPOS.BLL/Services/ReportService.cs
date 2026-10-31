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

        public ReportService(SalesRepository salesRepo)
        {
            _salesRepo = salesRepo;
        }

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

            var completedSales = allSales.Where(s => s.OrderStatus == "Completed").ToList();
            var heldSales = allSales.Where(s => s.OrderStatus == "Held").ToList();
            var refundedSales = allSales.Where(s => s.OrderStatus == "Refunded").ToList();

            decimal totalDiscount = completedSales.Sum(s => s.SubTotal + s.TaxAmount - s.TotalAmount);
            decimal totalCost = completedSales.SelectMany(s => s.SaleItems).Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);

            var salesDtos = allSales.Select(s => {
                decimal discount = s.SubTotal + s.TaxAmount - s.TotalAmount;
                decimal cost = s.SaleItems.Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);

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
                    EstimatedProfit = s.SubTotal - cost - discount,
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

            return new ReportDashboardDTO
            {
                AppliedStartDate = appliedStart,
                AppliedEndDate = appliedEnd,
                GrossRevenue = completedSales.Sum(s => s.TotalAmount),
                EstimatedProfit = completedSales.Sum(s => s.SubTotal) - totalCost - totalDiscount,
                TotalDiscounts = totalDiscount,
                CompletedOrders = completedSales.Count,
                ItemsSold = completedSales.SelectMany(s => s.SaleItems).Sum(si => si.Quantity),
                TaxLiabilities = completedSales.Sum(s => s.TaxAmount),
                HeldTransactions = heldSales.Count,
                HeldOrdersAmount = heldSales.Sum(s => s.TotalAmount),
                RefundedTransactions = refundedSales.Count,
                RefundedOrdersAmount = refundedSales.Sum(s => s.TotalAmount),
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
            DateTime queryStart = startDate.Date;
            DateTime queryEnd = endDate.Date.AddDays(1).AddTicks(-1);

            var sales = _salesRepo.GetSalesByDateRange(queryStart, queryEnd)
                                  .Where(s => s.OrderStatus == "Completed")
                                  .ToList();

            return new ZReportDTO
            {
                ReportDate = endDate.Date,
                StartDate = queryStart,
                EndDate = queryEnd,
                TotalTransactions = sales.Count,
                NetRevenue = sales.Sum(s => s.SubTotal),
                TotalDiscounts = sales.Sum(s => s.SubTotal + s.TaxAmount - s.TotalAmount),
                TotalTaxCollected = sales.Sum(s => s.TaxAmount),
                GrossRevenue = sales.Sum(s => s.TotalAmount),
                CashTotal = sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount),
                CardTotal = sales.Where(s => s.PaymentMethod == "Card").Sum(s => s.TotalAmount),
                MobileWalletTotal = sales.Where(s => s.PaymentMethod == "Mobile Wallet").Sum(s => s.TotalAmount)
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
            sb.AppendLine($"Subtotal (Gross): BDT {report.NetRevenue:N2}");
            sb.AppendLine($"Discounts Appld: -BDT {report.TotalDiscounts:N2}");
            sb.AppendLine($"Tax Collected:   +BDT {report.TotalTaxCollected:N2}");
            sb.AppendLine(new string('-', paperWidth));
            sb.AppendLine($"Net Collected:    BDT {report.GrossRevenue:N2}");
            sb.AppendLine(new string('=', paperWidth));

            sb.AppendLine("TENDER BREAKDOWN:");
            sb.AppendLine($"Cash:             BDT {report.CashTotal:N2}");
            sb.AppendLine($"Card:             BDT {report.CardTotal:N2}");
            sb.AppendLine($"Mobile Wallet:    BDT {report.MobileWalletTotal:N2}");
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