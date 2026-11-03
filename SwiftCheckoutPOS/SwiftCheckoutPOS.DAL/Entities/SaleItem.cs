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

    /// <summary>DR-36: total discount on this line (item discount x qty + its share of the bill discount).</summary>
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Sale Sale { get; set; } = null!;
}
