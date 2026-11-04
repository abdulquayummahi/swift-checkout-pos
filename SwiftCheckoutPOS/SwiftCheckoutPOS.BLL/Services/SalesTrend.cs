// Project: SwiftCheckoutPOS.BLL
// File: Services/SalesTrend.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;

namespace SwiftCheckoutPOS.BLL.Services
{
    /// <summary>
    /// Buckets bill totals for the Overview chart: by hour for today, by day up to a month, by week (ending today) up to
    /// six months, by calendar month beyond. Pure arithmetic, so it is tested without a database.
    /// </summary>
    public static class SalesTrend
    {
        public static SalesTrendDTO Build(IReadOnlyCollection<(DateTime When, decimal Total)> sales, DateTime from, DateTime now, string unit)
        {
            var today = now.Date;
            var points = new List<SalesTrendPointDTO>();
            switch (unit)
            {
                case "hour":
                    // A usual shop day (08:00 to 22:00), stretched to include any sale outside it.
                    var hours = sales.Where(s => s.When.Date == today).Select(s => s.When.Hour).ToList();
                    var first = Math.Min(8, hours.DefaultIfEmpty(8).Min());
                    var last = Math.Max(Math.Max(21, now.Hour), hours.DefaultIfEmpty(21).Max());
                    for (var h = first; h <= last; h++) points.Add(Point(today.AddHours(h), today.AddHours(h + 1)));
                    break;
                case "day":
                    for (var d = from.Date; d <= today; d = d.AddDays(1)) points.Add(Point(d, d.AddDays(1)));
                    break;
                case "week":
                    // Seven-day blocks ending today, so the last bar is always "this past week".
                    for (var end = today.AddDays(1); end > from.Date; end = end.AddDays(-7))
                        points.Insert(0, Point(end.AddDays(-7) < from.Date ? from.Date : end.AddDays(-7), end));
                    break;
                default:
                    var start = sales.Count > 0 ? sales.Min(s => s.When) : today;
                    if (start < from) start = from;
                    for (var m = new DateTime(start.Year, start.Month, 1); m <= today; m = m.AddMonths(1))
                        points.Add(Point(m < from ? from.Date : m, m.AddMonths(1)));
                    unit = "month";
                    break;
            }
            foreach (var (when, total) in sales)
            {
                var p = points.FirstOrDefault(x => x.Start <= when && when < x.End);
                if (p is null) continue;
                p.Total += total;
                p.Sales++;
            }
            return new SalesTrendDTO { Unit = unit, Points = points, Total = points.Sum(p => p.Total), Sales = points.Sum(p => p.Sales) };
        }

        private static SalesTrendPointDTO Point(DateTime start, DateTime end) => new() { Start = start, End = end };
    }
}
