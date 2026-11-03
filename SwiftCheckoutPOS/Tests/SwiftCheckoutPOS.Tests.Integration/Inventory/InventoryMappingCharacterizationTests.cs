using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Inventory;

[Collection("SqlServer")]
public sealed class InventoryMappingCharacterizationTests
{
    private readonly SqlServerFixture fixture;
    public InventoryMappingCharacterizationTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
        fixture.Reset();
    }

    [Theory]
    [InlineData("10.50", "11.50", "5.50")]
    [InlineData("11.55", "12.95", "7.50")]
    [Trait("Fixes", "DR-02")]
    public void Approved_ListAndEditKeepPaisaAndRates_PRQ02_PRQ04(string inputCost, string inputPrice, string inputTax)
    {
        var rawCost = decimal.Parse(inputCost, System.Globalization.CultureInfo.InvariantCulture);
        var rawPrice = decimal.Parse(inputPrice, System.Globalization.CultureInfo.InvariantCulture);
        var rawTax = decimal.Parse(inputTax, System.Globalization.CultureInfo.InvariantCulture);
        using var context = fixture.Context();
        var product = FixtureData.Product(context, rawPrice, rawCost, rawTax);
        var service = FixtureData.Inventory(context);
        foreach (var dto in new[] { service.GetAllSystemProducts().Single(), service.GetProductForEdit(product.ProductId) })
        {
            Assert.Equal(rawCost, dto.CostPrice);
            Assert.Equal(rawPrice, dto.SellingPrice);
            Assert.Equal(rawTax, dto.TaxRate);
        }
    }

    [Fact]
    [Trait("Fixes", "DR-02")]
    public void Approved_MetadataEditKeepsPaisa_PRQ02()
    {
        using var context = fixture.Context();
        var p = FixtureData.Product(context, price: 12.50m, cost: 10.50m, tax: 5.50m);
        var service = FixtureData.Inventory(context);
        var dto = service.GetProductForEdit(p.ProductId);
        dto.ProductName = "BP10 renamed only";
        service.UpdateProduct(dto, p.CategoryId);
        using var verify = fixture.Context();
        var stored = verify.Products.Single();
        Assert.Equal(10.50m, stored.CostPrice);
        Assert.Equal(12.50m, stored.SellingPrice);
        Assert.Equal(5.50m, stored.TaxRate);
    }

    [Fact]
    [Trait("Fixes", "DR-02")]
    public void Approved_CreateKeepsPaisaAndRecordsExactInitialCost_PRQ02()
    {
        using var context = fixture.Context();
        var seed = FixtureData.Product(context, stock: 0);
        FixtureData.Inventory(context).AddNewProduct(NewProduct(seed.CategoryId, seed.VendorId), seed.CategoryId);
        using var verify = fixture.Context();
        var stored = verify.Products.Single(p => p.Barcode == "BP10-NEW");
        Assert.Equal(10.50m, stored.CostPrice);
        Assert.Equal(12.50m, stored.SellingPrice);
        Assert.Equal(5.50m, stored.TaxRate);
        Assert.Equal(21m, verify.LedgerEntries.Single().Amount); // 2 x 10.50
    }

    [Fact]
    [Trait("Fixes", "DR-03")]
    public void Approved_RestockWacRoundsHalfAwayFromZero_PRQ06()
    {
        using var context = fixture.Context();
        FixtureData.Product(context, price: 5m, cost: 1m, stock: 1);
        FixtureData.Inventory(context).RestockProduct("BP10-ITEM", 1, 1.01m, null);
        using var verify = fixture.Context();
        Assert.Equal(1.01m, verify.Products.Single().CostPrice); // (1+1.01)/2 = 1.005 -> 1.01
        Assert.Equal(2, verify.Products.Single().StockQuantity);
        Assert.Equal(1.01m, verify.LedgerEntries.Single().Amount);
    }

    [Fact]
    [Trait("Fixes", "DR-09")]
    public void Approved_HeldStockStaysInWac_PRQ16()
    {
        int id;
        using (var seed = fixture.Context())
            id = FixtureData.Product(seed, price: 20m, cost: 10m, stock: 20).ProductId;
        var checkout = FixtureData.Checkout(fixture);
        checkout.HoldOrder(FixtureData.Cart(checkout, id, 8), FixtureData.Actor);
        using (var context = fixture.Context())
            FixtureData.Inventory(context).RestockProduct("BP10-ITEM", 10, 160m, null);
        using var verify = fixture.Context();
        Assert.Equal(12.00m, verify.Products.Single().CostPrice); // (20*10+160)/30 = 12: held units are still owned (old basis 12.73)
        Assert.Equal(30, verify.Products.Single().StockQuantity);
        Assert.Equal(8, verify.Products.Single().ReservedQuantity);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Current_CreateRejectsDuplicateBarcodeOrBelowCostPrice(bool duplicate)
    {
        using var context = fixture.Context();
        var seed = FixtureData.Product(context);
        var dto = NewProduct(seed.CategoryId, seed.VendorId);
        if (duplicate) dto.Barcode = seed.Barcode;
        else dto.SellingPrice = 1m;
        var service = FixtureData.Inventory(context);
        if (duplicate) Assert.Throws<InvalidOperationException>(() => service.AddNewProduct(dto, seed.CategoryId));
        else Assert.Throws<ArgumentException>(() => service.AddNewProduct(dto, seed.CategoryId));
        using var verify = fixture.Context();
        Assert.Single(verify.Products);
        Assert.Empty(verify.LedgerEntries);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Current_RestockRejectsInvalidQuantityOrCost(int quantity, bool negativeCost)
    {
        using var context = fixture.Context();
        FixtureData.Product(context);
        Assert.Throws<ArgumentException>(() =>
            FixtureData.Inventory(context).RestockProduct("BP10-ITEM", quantity, negativeCost ? -1m : 1m, null));
        using var verify = fixture.Context();
        Assert.Equal(10, verify.Products.Single().StockQuantity);
        Assert.Empty(verify.LedgerEntries);
    }

    [Fact]
    [Trait("Fixes", "PB-03")]
    public void Approved_LedgerFailureRollsBackProductToo_T06()
    {
        int categoryId, vendorId;
        using (var seed = fixture.Context())
        {
            var p = FixtureData.Product(seed, stock: 0);
            categoryId = p.CategoryId;
            vendorId = p.VendorId;
        }
        using (var context = fixture.Context(new FailLedgerSave()))
            Assert.Throws<InjectedTestFailure>(() =>
                FixtureData.Inventory(context).AddNewProduct(NewProduct(categoryId, vendorId), categoryId));
        using var verify = fixture.Context();
        Assert.DoesNotContain(verify.Products, p => p.Barcode == "BP10-NEW");
        Assert.Empty(verify.LedgerEntries);
        Assert.Empty(verify.AuditEvents);
    }

    [Fact]
    [Trait("Fixes", "IMM-06")]
    public void Approved_StaleEditFormIsRejectedAndEditNeverChangesStock()
    {
        int id;
        using (var seed = fixture.Context()) id = FixtureData.Product(seed, stock: 10).ProductId;
        ProductDTO form;
        using (var context = fixture.Context()) form = FixtureData.Inventory(context).GetProductForEdit(id);
        // A sale happens after the form was opened.
        var checkout = FixtureData.Checkout(fixture);
        checkout.CompleteSale(FixtureData.Cart(checkout, id, 3), "Card", FixtureData.Actor);
        form.ProductName = "Renamed from a stale form";
        form.StockQuantity = 10; // stale value must never overwrite the sale's deduction
        using (var context = fixture.Context())
            Assert.Throws<InvalidOperationException>(() => FixtureData.Inventory(context).UpdateProduct(form, form.CategoryId, FixtureData.Actor));
        using (var context = fixture.Context())
        {
            var fresh = FixtureData.Inventory(context).GetProductForEdit(id);
            fresh.ProductName = "Renamed";
            fresh.StockQuantity = 999;
            fresh.SellingPrice = 11m;
            FixtureData.Inventory(context).UpdateProduct(fresh, fresh.CategoryId, FixtureData.Actor);
        }
        using var verify = fixture.Context();
        var stored = verify.Products.Single();
        Assert.Equal("Renamed", stored.ProductName);
        Assert.Equal(7, stored.StockQuantity);
        Assert.Equal("PriceChange", verify.AuditEvents.Single().Action);
    }

    [Theory]
    [InlineData(4, "Damaged", "Damaged", "Outflow", 30)]
    [InlineData(4, "Theft/Loss", "Inventory Adjustment", "Outflow", 30)]
    [InlineData(12, "Found", "Inventory Adjustment", "Inflow", 10)]
    public void Approved_StockCorrectionPostsValueAndAudit_PRQ24_PRQ82(int counted, string reason, string category, string type, int value)
    {
        using (var seed = fixture.Context()) FixtureData.Product(seed, cost: 5m, stock: 10);
        using (var context = fixture.Context())
            FixtureData.Inventory(context).AdjustStock("BP10-ITEM", counted, reason, null, FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(counted, verify.Products.Single().StockQuantity);
        var entry = verify.LedgerEntries.Single();
        Assert.Equal(category, entry.Category);
        Assert.Equal(type, entry.TransactionType);
        Assert.Equal((decimal)value, entry.Amount);
        var audit = verify.AuditEvents.Single();
        Assert.Equal("StockAdjustment", audit.Action);
        Assert.Equal(reason, audit.Reason);
        Assert.Equal(FixtureData.Actor, audit.Actor);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Other", null)]
    [InlineData("Bogus", "note")]
    public void Approved_StockCorrectionNeedsValidReason(string reason, string? note)
    {
        using (var seed = fixture.Context()) FixtureData.Product(seed, stock: 10);
        using (var context = fixture.Context())
            Assert.Throws<ArgumentException>(() => FixtureData.Inventory(context).AdjustStock("BP10-ITEM", 5, reason, note, FixtureData.Actor));
        using var verify = fixture.Context();
        Assert.Equal(10, verify.Products.Single().StockQuantity);
        Assert.Empty(verify.AuditEvents);
    }

    [Fact]
    public void Approved_ZeroCostRestockNeedsReasonAndPostsNoCash_PRQ20()
    {
        using (var seed = fixture.Context()) FixtureData.Product(seed, cost: 10m, stock: 10);
        using (var context = fixture.Context())
            Assert.Throws<ArgumentException>(() => FixtureData.Inventory(context).RestockProduct("BP10-ITEM", 10, 0m, null, FixtureData.Actor));
        using (var context = fixture.Context())
            FixtureData.Inventory(context).RestockProduct("BP10-ITEM", 10, 0m, null, FixtureData.Actor, "Supplier bonus");
        using var verify = fixture.Context();
        Assert.Equal(20, verify.Products.Single().StockQuantity);
        Assert.Equal(5m, verify.Products.Single().CostPrice); // average cost goes down
        Assert.Empty(verify.LedgerEntries);
        Assert.Equal("Supplier bonus", verify.AuditEvents.Single().Reason);
    }

    [Fact]
    public void Approved_ReturnWithoutCashNotesVendorCredit_PRQ25()
    {
        using (var seed = fixture.Context()) FixtureData.Product(seed, stock: 10);
        using (var context = fixture.Context())
            FixtureData.Inventory(context).ReturnToVendor("BP10-ITEM", 3, 0m, FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(7, verify.Products.Single().StockQuantity);
        Assert.Empty(verify.LedgerEntries);
        Assert.Equal("Vendor credit pending", verify.AuditEvents.Single().Reason);
    }

    private static ProductDTO NewProduct(int categoryId, int vendorId) => new()
    {
        Barcode = "BP10-NEW", ProductName = "BP10 new synthetic item", CategoryId = categoryId,
        VendorId = vendorId, CostPrice = 10.50m, SellingPrice = 12.50m,
        TaxRate = 5.50m, StockQuantity = 2, UnitOfMeasure = "pcs"
    };
}
