// Project: SwiftCheckoutPOS.BLL
// File: DTOs/SalesTrendDTO.cs
using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    /// <summary>Overview chart: bill totals per hour, day, week or month, and the same total for the previous period.</summary>
    public sealed class SalesTrendDTO
    {
        /// <summary>"hour", "day", "week" or "month".</summary>
        public string Unit { get; set; } = "day";
        public List<SalesTrendPointDTO> Points { get; set; } = new();
        public decimal Total { get; set; }
        public int Sales { get; set; }
        /// <summary>Total of the previous period of the same length; null when there is nothing to compare with (all time).</summary>
        public decimal? PreviousTotal { get; set; }
        /// <summary>How the comparison reads, for example "the previous 7 days" or "the same time yesterday".</summary>
        public string PreviousLabel { get; set; } = "";
    }

    public sealed class SalesTrendPointDTO
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public decimal Total { get; set; }
        public int Sales { get; set; }
    }
}
