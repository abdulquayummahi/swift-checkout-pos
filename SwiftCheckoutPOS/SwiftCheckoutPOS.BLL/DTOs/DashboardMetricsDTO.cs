// Project: SwiftCheckoutPOS.BLL
// File: DTOs/DashboardMetricsDTO.cs
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class DashboardMetricsDTO
    {
        public decimal TotalRevenue { get; set; }
        public decimal EstimatedProfit { get; set; }
        public decimal TotalDiscounts { get; set; }
        public int TotalOrders { get; set; }
        public int ProductsSold { get; set; }
        public int NewCustomers { get; set; }

        public List<TopProductDTO> TopProducts { get; set; } = new List<TopProductDTO>();
        public List<SaleDTO> RecentTransactions { get; set; } = new List<SaleDTO>();
    }
}