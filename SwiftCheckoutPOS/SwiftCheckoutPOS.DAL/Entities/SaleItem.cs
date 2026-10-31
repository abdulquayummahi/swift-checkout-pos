using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.DAL.Entities;

public partial class SaleItem
{
    public int SaleItemId { get; set; }

    public int SaleId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }

    /// <summary>Unit cost captured at completion so historical profit and refunds do not change with later supplier prices.</summary>
    public decimal CostAtSale { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Sale Sale { get; set; } = null!;
}
