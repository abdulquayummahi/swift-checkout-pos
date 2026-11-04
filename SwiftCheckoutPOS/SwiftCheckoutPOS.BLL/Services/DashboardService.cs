// Project: SwiftCheckoutPOS.BLL
// File: Services/DashboardService.cs
using System;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class DashboardService
    {
        private readonly SalesRepository _salesRepo;
        private readonly CustomerRepository _customerRepo;
        private readonly InventoryRepository _inventoryRepo;

        public DashboardService(SalesRepository salesRepo, CustomerRepository customerRepo, InventoryRepository inventoryRepo, MetricsService metrics)
        {
            _metrics = metrics;
            _salesRepo = salesRepo;
            _customerRepo = customerRepo;
            _inventoryRepo = inventoryRepo;
        }

        private readonly MetricsService _metrics;

        public DashboardMetricsDTO GetMetrics(string filter)
        {
            DateTime startDate;
            DateTime endDate = DateTime.Now;

            switch (filter?.ToLower())
            {
                case "7days": startDate = DateTime.Today.AddDays(-6); break;   // today and the six days before
                case "30days": startDate = DateTime.Today.AddDays(-29); break;
                case "6months": startDate = DateTime.Today.AddMonths(-6); break;
                case "1year": startDate = DateTime.Today.AddYears(-1); break;
                case "lifetime": startDate = new DateTime(1753, 1, 1); break;
                case "today": default: startDate = DateTime.Today; break;
            }

            var allSales = _salesRepo.GetSalesByDateRange(startDate, endDate).ToList();

            // ARCHITECT FIX: Only completed sales map to global financial health
            var completedSales = allSales.Where(s => (s.OrderStatus == "Completed" || s.OrderStatus == "PartiallyRefunded")).ToList();

            var newCustomerCount = _customerRepo.GetNewCustomerCount(startDate, endDate);

            decimal totalDiscount = completedSales.Sum(s => s.SubTotal - s.TotalAmount);
            decimal totalCost = completedSales.SelectMany(s => s.SaleItems).Sum(si => si.CostAtSale * si.Quantity);

            var m = _metrics.Period(startDate, DateTime.Today.AddDays(1)); // approved definitions
            var dto = new DashboardMetricsDTO
            {
                TotalRevenue = m.NetSales,
                EstimatedProfit = m.GrossProfit,
                TotalDiscounts = m.Discounts,
                TotalOrders = m.SalesCount,
                ProductsSold = m.ItemsSold,
                NewCustomers = newCustomerCount
            };

            var productGroups = completedSales.SelectMany(s => s.SaleItems)
                .GroupBy(si => si.ProductId)
                .Select(g => new TopProductDTO
                {
                    ProductName = g.First().Product?.ProductName ?? "Unknown",
                    QuantitySold = g.Sum(si => si.Quantity)
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(5)
                .ToList();

            int maxSold = productGroups.Any() ? productGroups.Max(p => p.QuantitySold) : 1;
            foreach (var p in productGroups) { p.Percentage = ((decimal)p.QuantitySold / maxSold) * 100; }
            dto.TopProducts = productGroups;

            // Render all sales (including refunded) for the transaction list to maintain an accurate audit log
            dto.RecentTransactions = allSales.OrderByDescending(s => s.SaleDate).Take(6).Select(s => {
                decimal discount = s.SubTotal - s.TotalAmount;
                decimal cost = s.SaleItems.Sum(si => si.CostAtSale * si.Quantity);
                return new SaleDTO
                {
                    SaleId = s.SaleId,
                    ReceiptNumber = s.ReceiptNumber,
                    SaleDate = s.SaleDate,
                    TotalAmount = s.TotalAmount,
                    PaymentMethod = s.PaymentMethod,
                    OrderStatus = s.OrderStatus,
                    CustomerName = s.Customer?.FullName ?? "Walk-in",
                    CustomerPhone = s.Customer?.PhoneNumber,
                    DiscountAmount = discount,
                    EstimatedProfit = s.TotalAmount - s.TaxAmount - cost,
                    SaleItems = s.SaleItems.Select(si => new SaleItemDTO { ProductId = si.ProductId, ProductName = si.Product?.ProductName ?? "Unknown Item", Quantity = si.Quantity, UnitPrice = si.UnitPrice, LineTotal = si.LineTotal }).ToList()
                };
            }).ToList();

            // Overview chart: bill totals over the period, and the same total for the period before it.
            var f = filter?.ToLower() ?? "today";
            var unit = f switch { "7days" or "30days" => "day", "6months" => "week", "1year" or "lifetime" => "month", _ => "hour" };
            dto.Trend = SalesTrend.Build(_salesRepo.GetSaleTotals(startDate, DateTime.Today.AddDays(1)), startDate, DateTime.Now, unit);
            (DateTime From, DateTime To, string Label)? previous = f switch
            {
                "7days" => (startDate.AddDays(-7), startDate, "the previous 7 days"),
                "30days" => (startDate.AddDays(-30), startDate, "the previous 30 days"),
                "6months" => (startDate.AddMonths(-6), startDate, "the previous 6 months"),
                "1year" => (startDate.AddYears(-1), startDate, "the year before"),
                "lifetime" => null,
                _ => (DateTime.Today.AddDays(-1), DateTime.Now.AddDays(-1), "the same time yesterday")
            };
            if (previous is { } prev)
            {
                dto.Trend.PreviousTotal = _salesRepo.GetSaleTotals(prev.From, prev.To).Sum(t => t.TotalAmount);
                dto.Trend.PreviousLabel = prev.Label;
            }

            return dto;
        }
    }
}