using SwiftCheckoutPOS.BLL.Hardware;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Receipts;

[Collection("SqlServer")]
public sealed class ReceiptTests
{
    private readonly SqlServerFixture fixture;
    public ReceiptTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    [Fact]
    [Trait("Fixes", "DR-31")]
    public void Approved_ReprintComesFromSavedSaleMarkedAndLogged_PRQ48_PRQ83()
    {
        int id;
        using (var c = fixture.Context()) id = FixtureData.Product(c, price: 10.40m).ProductId;
        var checkout = FixtureData.Checkout(fixture);
        var sold = checkout.CompleteSale(FixtureData.Cart(checkout, id, 2), "Cash", FixtureData.Actor);
        using (var c = fixture.Context()) { c.Products.Single().SellingPrice = 99m; c.SaveChanges(); } // later price change
        using var context = fixture.Context();
        var copy = new ReceiptService(new SalesRepository(context), new AtomicUnit(context)).Reprint(sold.ReceiptNumber, FixtureData.Actor);
        var text = ReceiptFormatter.Receipt(copy, new ReceiptHeaderDTO("My Shop", "Dhaka", "0170", "", "Thanks"));
        Assert.Contains("REPRINT / COPY", text);
        Assert.Contains("by Fixture Cashier", text);
        Assert.Contains(sold.ReceiptNumber, text);
        Assert.Contains("2x10.40", text);          // saved price, not today's 99
        Assert.Contains("ROUNDING:", text);        // 20.80 cash -> 21.00
        Assert.DoesNotContain("BIN:", text);       // not VAT-registered
        Assert.DoesNotContain("VAT", text);
        using var v = fixture.Context();
        Assert.Equal("Reprint", v.AuditEvents.Single().Action);
    }

    private sealed class FakeTransport : IRawTransport
    {
        public List<byte[]> Sent { get; } = new();
        public bool IsAvailable() => true;
        public void Send(byte[] data) => Sent.Add(data);
    }

    [Fact]
    public void Approved_EscPosPrinterSendsInitTextCutAndDrawerPulse_BP08()
    {
        var t = new FakeTransport();
        var printer = new EscPosPrinter(t, () => new ReceiptHeaderDTO("Shop", "", "", "BIN-1", ""));
        var cart = new SwiftCheckoutPOS.BLL.DTOs.CheckoutSessionDTO { ReceiptNumber = "SCP-20261104-0001" };
        cart.Items.Add(new() { ProductId = 1, ProductName = "Tea", UnitPrice = 5m, Quantity = 1 });
        printer.PrintReceipt(cart);
        printer.OpenCashDrawer();
        Assert.Equal(new byte[] { 0x1B, 0x40 }, t.Sent[0][..2]);                // ESC @
        Assert.Equal(new byte[] { 0x1D, 0x56, 0x41, 0x10 }, t.Sent[0][^4..]);   // cut
        Assert.Contains("BIN: BIN-1", System.Text.Encoding.ASCII.GetString(t.Sent[0]));
        Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, t.Sent[1]);   // drawer kick
    }
}
