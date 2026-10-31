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

        public DashboardService(SalesRepository salesRepo, CustomerRepository customerRepo, InventoryRepository inventoryRepo)
        {
            _salesRepo = salesRepo;
            _customerRepo = customerRepo;
            _inventoryRepo = inventoryRepo;
        }

        public DashboardMetricsDTO GetMetrics(string filter)
        {
            DateTime startDate;
            DateTime endDate = DateTime.Now;

            switch (filter?.ToLower())
            {
                case "7days": startDate = DateTime.Today.AddDays(-7); break;
                case "30days": startDate = DateTime.Today.AddDays(-30); break;
                case "6months": startDate = DateTime.Today.AddMonths(-6); break;
                case "1year": startDate = DateTime.Today.AddYears(-1); break;
                case "lifetime": startDate = new DateTime(1753, 1, 1); break;
                case "today": default: startDate = DateTime.Today; break;
            }

            var allSales = _salesRepo.GetSalesByDateRange(startDate, endDate).ToList();

            // ARCHITECT FIX: Only completed sales map to global financial health
            var completedSales = allSales.Where(s => s.OrderStatus == "Completed").ToList();

            var newCustomerCount = _customerRepo.GetNewCustomerCount(startDate, endDate);

            decimal totalDiscount = completedSales.Sum(s => s.SubTotal + s.TaxAmount - s.TotalAmount);
            decimal totalCost = completedSales.SelectMany(s => s.SaleItems).Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);

            var dto = new DashboardMetricsDTO
            {
                TotalRevenue = completedSales.Sum(s => s.TotalAmount),
                EstimatedProfit = completedSales.Sum(s => s.SubTotal) - totalCost - totalDiscount,
                TotalDiscounts = totalDiscount,
                TotalOrders = completedSales.Count,
                ProductsSold = completedSales.SelectMany(s => s.SaleItems).Sum(si => si.Quantity),
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
                decimal discount = s.SubTotal + s.TaxAmount - s.TotalAmount;
                decimal cost = s.SaleItems.Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);
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
                    EstimatedProfit = s.SubTotal - cost - discount,
                    SaleItems = s.SaleItems.Select(si => new SaleItemDTO { ProductId = si.ProductId, ProductName = si.Product?.ProductName ?? "Unknown Item", Quantity = si.Quantity, UnitPrice = si.UnitPrice, LineTotal = si.LineTotal }).ToList()
                };
            }).ToList();

            return dto;
        }
    }
}