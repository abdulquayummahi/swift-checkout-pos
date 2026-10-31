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
    [InlineData("10.50", "11.50", "5.50", 10, 12, 6)]
    [InlineData("11.50", "12.50", "4.50", 12, 12, 4)]
    [Trait("Difference", "DR-02")]
    public void Current_ListAndEditWholeRoundMoneyAndRates_DiffersFromPRQ02_PRQ06(
        string inputCost, string inputPrice, string inputTax, int cost, int price, int tax)
    {
        var rawCost = decimal.Parse(inputCost, System.Globalization.CultureInfo.InvariantCulture);
        var rawPrice = decimal.Parse(inputPrice, System.Globalization.CultureInfo.InvariantCulture);
        var rawTax = decimal.Parse(inputTax, System.Globalization.CultureInfo.InvariantCulture);
        using var context = fixture.Context();
        var product = FixtureData.Product(context, rawPrice, rawCost, rawTax);
        var service = FixtureData.Inventory(context);
        var list = service.GetAllSystemProducts().Single();
        var edit = service.GetProductForEdit(product.ProductId);
        foreach (var dto in new[] { list, edit })
        {
            Assert.Equal((decimal)cost, dto.CostPrice);
            Assert.Equal((decimal)price, dto.SellingPrice);
            Assert.Equal((decimal)tax, dto.TaxRate);
        }
        using var verify = fixture.Context();
        Assert.Equal(rawCost, verify.Products.Single().CostPrice); // projection alone does not update SQL
        Assert.Equal(rawPrice, verify.Products.Single().SellingPrice);
    }

    [Fact]
    [Trait("Difference", "DR-02")]
    public void Current_MetadataEditRoundTripLosesPaisa_DiffersFromPRQ02()
    {
        using var context = fixture.Context();
        var p = FixtureData.Product(context, price: 12.50m, cost: 10.50m, tax: 5.50m);
        var service = FixtureData.Inventory(context);
        var dto = service.GetProductForEdit(p.ProductId);
        dto.ProductName = "BP10 renamed only";
        service.UpdateProduct(dto, p.CategoryId);
        using var verify = fixture.Context();
        var stored = verify.Products.Single();
        Assert.Equal(10m, stored.CostPrice);
        Assert.Equal(12m, stored.SellingPrice);
        Assert.Equal(6m, stored.TaxRate);
    }

    [Fact]
    [Trait("Difference", "DR-02")]
    public void Current_CreateRoundsMoneyBeforeRecordingInitialCost_DiffersFromPRQ02()
    {
        using var context = fixture.Context();
        var seed = FixtureData.Product(context, stock: 0);
        var dto = NewProduct(seed.CategoryId, seed.VendorId);
        FixtureData.Inventory(context).AddNewProduct(dto, seed.CategoryId);
        using var verify = fixture.Context();
        var stored = verify.Products.Single(p => p.Barcode == "BP10-NEW");
        Assert.Equal(10m, stored.CostPrice);
        Assert.Equal(12m, stored.SellingPrice);
        Assert.Equal(6m, stored.TaxRate);
        Assert.Equal(20m, verify.LedgerEntries.Single().Amount);
    }

    [Fact]
    [Trait("Difference", "DR-03")]
    public void Current_RestockWacUsesToEven_DiffersFromPRQ06()
    {
        using var context = fixture.Context();
        FixtureData.Product(context, price: 5m, cost: 1m, stock: 1);
        FixtureData.Inventory(context).RestockProduct("BP10-ITEM", 1, 1.01m, null);
        using var verify = fixture.Context();
        Assert.Equal(1.00m, verify.Products.Single().CostPrice); // (1+1.01)/2=1.005
        Assert.Equal(2, verify.Products.Single().StockQuantity);
        Assert.Equal(1.01m, verify.LedgerEntries.Single().Amount);
    }

    [Fact]
    [Trait("Difference", "DR-09")]
    public void Current_HeldStockIsExcludedFromWac_DiffersFromPRQ16()
    {
        int id;
        using (var seed = fixture.Context())
            id = FixtureData.Product(seed, price: 20m, cost: 10m, stock: 20).ProductId;
        var checkout = FixtureData.Checkout(fixture);
        checkout.HoldOrder(FixtureData.Cart(checkout, id, 8), FixtureData.Actor);
        using (var context = fixture.Context())
            FixtureData.Inventory(context).RestockProduct("BP10-ITEM", 10, 160m, null);
        using var verify = fixture.Context();
        Assert.Equal(12.73m, verify.Products.Single().CostPrice); // (12*10+160)/22, not owned-stock basis
        Assert.Equal(22, verify.Products.Single().StockQuantity);
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
    [Trait("Difference", "DR-02")]
    [Trait("Audit", "PB-03")]
    public void Current_LedgerFailureLeavesRoundedProductCommitted_DiffersFromAtomicInventory()
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
        var stored = verify.Products.Single(p => p.Barcode == "BP10-NEW");
        Assert.Equal(2, stored.StockQuantity);
        Assert.Equal(10m, stored.CostPrice);
        Assert.Empty(verify.LedgerEntries); // product save committed before failed ledger save
    }

    private static ProductDTO NewProduct(int categoryId, int vendorId) => new()
    {
        Barcode = "BP10-NEW", ProductName = "BP10 new synthetic item", CategoryId = categoryId,
        VendorId = vendorId, CostPrice = 10.50m, SellingPrice = 12.50m,
        TaxRate = 5.50m, StockQuantity = 2, UnitOfMeasure = "pcs"
    };
}
