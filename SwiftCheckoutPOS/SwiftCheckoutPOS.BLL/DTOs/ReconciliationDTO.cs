// Project: SwiftCheckoutPOS.BLL
// File: DTOs/ReconciliationDTO.cs
using System;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class ReconciliationDTO
    {
        public DateTime ShiftStartTime { get; set; }
        public decimal OpeningFloat { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public string? Notes { get; set; }
    }
}