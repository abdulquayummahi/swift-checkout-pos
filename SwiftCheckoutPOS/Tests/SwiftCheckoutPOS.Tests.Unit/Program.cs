using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.UI.Controllers;

namespace SwiftCheckoutPOS.Tests.Unit;

// Dependency-free executable regression suite; no app host, SQL or device is started.
internal static class Program
{
    private const string Actor = "User 1: Cashier";
    private static int Main()
    {
        (string Name, Action Test)[] cases =
        [
            ("Barcode lookup preserves fractional prices and tax", () =>
            {
                var (repo, service, cart) = Setup();
                service.AddToCartByBarcode(cart, "  ABC-1  ");
                Equal(11.25m, cart.Items.Single().UnitPrice);
                Equal(5.5m, cart.Items.Single().TaxRate);
                Equal(11.25m, service.GetAvailableProducts().Single().SellingPrice);
                Equal(10, repo.Products[1].StockQuantity);
            }),
            ("Unknown barcode and inactive product are rejected", () =>
            {
                var (repo, service, cart) = Setup();
                Reject(() => service.AddToCartByBarcode(cart, "unknown"));
                repo.Products[1].IsActive = false;
                Reject(() => service.AddToCart(cart, 1));
                Equal(0, cart.Items.Count);
            }),
            ("Adding beyond stock cannot mutate cart", () =>
            {
                var (_, service, cart) = Setup();
                service.AddToCart(cart, 1, 8);
                Reject(() => service.AddToCart(cart, 1, 3));
                Equal(8, cart.Items.Single().Quantity);
                Reject(() => service.AddToCart(cart, 1, -1));
            }),
            ("Quantity decrease safely handles int minimum", () =>
            {
                var (_, service, cart) = Ready();
                service.UpdateQuantity(cart, 1, int.MinValue);
                Equal(0, cart.Items.Count);
            }),
            ("Existing flat cap and percent calculations are retained", () =>
            {
                var (_, service, cart) = Ready();
                service.ApplyBillDiscount(cart, 1000, "Flat");
                Equal(0m, cart.TotalAmount);
                service.ApplyBillDiscount(cart, 10, "Percent");
                Equal(0m, cart.BillDiscountAmount);
                Equal(2.25m, cart.CalculatedDiscount);
                Reject(() => service.ApplyBillDiscount(cart, 101, "Percent"));
                Reject(() => service.ApplyBillDiscount(cart, 1, "Unknown"));
            }),
            ("Completion stages sale, stock, captured cost and COGS together", () =>
            {
                var (repo, service, cart) = Ready();
                service.ApplyBillDiscount(cart, 1, "Flat");
                var result = service.CompleteSale(cart, "Cash", Actor);
                Equal(22.68m, repo.Sales.Single().TotalAmount);
                Equal(1.18m, repo.Sales.Single().TaxAmount);
                Equal(8, repo.Products[1].StockQuantity);
                Equal(7.25m, repo.Sales.Single().SaleItems.Single().CostAtSale);
                Equal(14.50m, repo.Ledger.Single().Amount);
                Equal("COGS", repo.Ledger.Single().Category);
                Equal(Actor, repo.Ledger.Single().RecordedBy);
                Equal(1, repo.Commits);
                Equal(false, result.AlreadyCompleted);
                Equal(Actor, result.Receipt.ReceiptCashierName);
            }),
            ("Duplicate completion has one sale and one stock/ledger effect", () =>
            {
                var (repo, service, cart) = Ready();
                var first = service.CompleteSale(cart, "Cash", Actor);
                var second = service.CompleteSale(cart, "Cash", Actor);
                Equal(first.SaleId, second.SaleId);
                Equal(true, second.AlreadyCompleted);
                Equal(1, repo.Sales.Count);
                Equal(1, repo.Ledger.Count);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Different payload or tender cannot reuse receipt", () =>
            {
                var (repo, service, cart) = Ready();
                service.CompleteSale(cart, "Cash", Actor);
                Reject(() => service.CompleteSale(cart, "Card", Actor));
                cart.Items[0].Quantity = 1;
                Reject(() => service.CompleteSale(cart, "Cash", Actor));
                Equal(1, repo.Sales.Count);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Lost acknowledgement is reconciled on same-receipt replay", () =>
            {
                var (repo, service, cart) = Ready();
                repo.LoseAcknowledgement = true;
                Throws<IOException>(() => service.CompleteSale(cart, "Cash", Actor));
                Equal(true, service.CompleteSale(cart, "Cash", Actor).AlreadyCompleted);
                Equal(1, repo.Sales.Count);
                Equal(1, repo.Ledger.Count);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Commit failure leaves no fake business effects", () =>
            {
                var (repo, service, cart) = Ready();
                repo.FailCommit = true;
                Throws<IOException>(() => service.CompleteSale(cart, "Cash", Actor));
                Equal(10, repo.Products[1].StockQuantity);
                Equal(0, repo.Sales.Count);
                Equal(0, repo.Ledger.Count);
            }),
            ("Commit-time shortage, price and tax changes reject safely", () =>
            {
                foreach (var change in new Action<Product>[]
                {
                    p => p.StockQuantity = 1, p => p.SellingPrice = 12m,
                    p => p.TaxRate = 6m, p => p.IsActive = false
                })
                {
                    var (repo, service, cart) = Ready();
                    change(repo.Products[1]);
                    Reject(() => service.CompleteSale(cart, "Cash", Actor));
                    Equal(0, repo.Sales.Count);
                    Equal(0, repo.Ledger.Count);
                }
            }),
            ("Missing product and duplicate lines cannot commit", () =>
            {
                var (repo, service, cart) = Ready();
                repo.Products.Clear();
                Reject(() => service.CompleteSale(cart, "Cash", Actor));
                Equal(0, repo.Sales.Count);
                var (_, another, malformed) = Ready();
                malformed.Items.Add(malformed.Items[0]);
                Reject(() => another.CompleteSale(malformed, "Cash", Actor));
            }),
            ("Empty cart, unsupported tender and invalid money reject", () =>
            {
                var (_, service, cart) = Setup();
                Reject(() => service.CompleteSale(cart, "Cash", Actor));
                service.AddToCart(cart, 1);
                Reject(() => service.CompleteSale(cart, "Crypto", Actor));
                cart.Items[0].UnitPrice = 11.251m;
                Reject(() => service.CompleteSale(cart, "Cash", Actor));
            }),
            ("Loyalty association leaves points unchanged", () =>
            {
                var (repo, service, cart) = Ready();
                repo.Customers.Add(1, new Customer { CustomerId = 1, FullName = "Fixture", PhoneNumber = "TEST-1", LoyaltyPoints = 37 });
                service.LinkCustomer(cart, "TEST-1");
                service.CompleteSale(cart, "Cash", Actor);
                Equal(1, repo.Sales.Single().CustomerId);
                Equal(37, repo.Customers[1].LoyaltyPoints);
            }),
            ("Unlinked and missing loyalty customers cannot complete", () =>
            {
                var (_, service, cart) = Ready();
                service.SetCustomerType(cart, "Loyalty");
                Reject(() => service.CompleteSale(cart, "Cash", Actor));
                cart.CustomerId = 99;
                Reject(() => service.CompleteSale(cart, "Cash", Actor));
            }),
            ("Repeated hold reserves once without COGS", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                service.HoldOrder(cart, Actor);
                Equal(8, repo.Products[1].StockQuantity);
                Equal(1, repo.Sales.Count);
                Equal(0, repo.Ledger.Count);
                Equal("Held", repo.Sales.Single().OrderStatus);
            }),
            ("Repeated resume releases once and retains recoverable SQL copy", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var id = repo.Sales.Single().SaleId;
                var resumed = service.ResumeOrder(id);
                var again = service.ResumeOrder(id);
                Equal(10, repo.Products[1].StockQuantity);
                Equal(1, repo.Sales.Count);
                Equal("Resumed", repo.Sales.Single().OrderStatus);
                Equal(resumed.TotalAmount, again.TotalAmount);
                Equal(id, service.GetHeldOrders().Single().SaleId);
            }),
            ("Resumed order completes under original ID and captures completion cost", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var id = repo.Sales.Single().SaleId;
                var resumed = service.ResumeOrder(id);
                repo.Products[1].CostPrice = 8m;
                service.CompleteSale(resumed, "Card", Actor);
                Equal(id, repo.Sales.Single().SaleId);
                Equal(8m, repo.Sales.Single().SaleItems.Single().CostAtSale);
                Equal(16m, repo.Ledger.Single().Amount);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Rehold and stale resumed tab cannot reserve or charge twice", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var resumed = service.ResumeOrder(repo.Sales.Single().SaleId);
                var stale = Copy(resumed);
                service.HoldOrder(resumed, Actor);
                Reject(() => service.CompleteSale(stale, "Cash", Actor));
                Equal(8, repo.Products[1].StockQuantity);
                Equal(1, repo.Sales.Count);
            }),
            ("Clearing resumed work preserves a cancelled row without extra stock", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var resumed = service.ResumeOrder(repo.Sales.Single().SaleId);
                service.CancelCart(resumed);
                service.CancelCart(resumed);
                Equal("Cancelled", repo.Sales.Single().OrderStatus);
                Equal(10, repo.Products[1].StockQuantity);
                Equal(0, service.GetHeldOrders().Count);
            }),
            ("Legacy tax mismatch is refused before releasing reservation", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                repo.Products[1].TaxRate = 20;
                Reject(() => service.ResumeOrder(repo.Sales.Single().SaleId));
                Equal("Held", repo.Sales.Single().OrderStatus);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Saved held discount amount survives resume", () =>
            {
                var (repo, service, cart) = Ready();
                service.ApplyBillDiscount(cart, 10, "Percent");
                var expected = cart.TotalAmount;
                service.HoldOrder(cart, Actor);
                var resumed = service.ResumeOrder(repo.Sales.Single().SaleId);
                Equal(expected, resumed.TotalAmount);
            }),
            ("Printer factory failure happens after committed sale", () =>
            {
                var (repo, service, cart) = Ready();
                var controller = Controller(service, cart, () =>
                {
                    Equal("Completed", repo.Sales.Single().OrderStatus);
                    throw new IOException("fixture printer construction failure");
                });
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Equal(1, repo.Sales.Count);
                Check(controller.TempData.ContainsKey("Success"));
                Check(controller.TempData.ContainsKey("Warning"));
                Check(!controller.TempData.ContainsKey("Error"));
            }),
            ("Printer failure never retries sale or repeats on stale form", () =>
            {
                var (repo, service, cart) = Ready();
                var printer = new FixturePrinter { Fail = true };
                var controller = Controller(service, cart, () => printer);
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Equal(1, repo.Sales.Count);
                Equal(1, repo.Ledger.Count);
                Equal(1, printer.Prints);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Session clear failure after commit does not become failed checkout", () =>
            {
                var (repo, service, cart) = Ready();
                var controller = Controller(service, cart, () => new FixturePrinter());
                ((FixtureSession)controller.HttpContext.Session).FailRemove = true;
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Check(controller.TempData.ContainsKey("Success"));
                Check(!controller.TempData.ContainsKey("Error"));
                Equal(1, repo.Sales.Count);
            }),
            ("Model binding errors and stale forms cannot charge another cart", () =>
            {
                var (repo, service, cart) = Ready();
                var controller = Controller(service, cart, () => new FixturePrinter());
                controller.ModelState.AddModelError("paymentMethod", "fixture");
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Equal(0, repo.Sales.Count);
                controller.ModelState.Clear();
                controller.Finalize("Cash", "INV-DIFFERENT", cart.Revision);
                Equal(0, repo.Sales.Count);
            }),
            ("Uncertain outcome locks changes and reconciles original tender", () =>
            {
                var (repo, service, cart) = Ready();
                var controller = Controller(service, cart, () => new FixturePrinter());
                repo.LoseAcknowledgement = true;
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                var pending = JsonConvert.DeserializeObject<CheckoutSessionDTO>(controller.HttpContext.Session.GetString("ActiveCart")!)!;
                Equal(true, pending.AwaitingConfirmation);
                controller.ClearCart();
                controller.AddById(1);
                controller.Finalize("Card", cart.InvoiceNumber, cart.Revision);
                Equal(1, repo.Sales.Count);
                Equal(8, repo.Products[1].StockQuantity);
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Equal(1, repo.Sales.Count);
                Equal(1, repo.Ledger.Count);
                Check(controller.HttpContext.Session.GetString("ActiveCart") is null);
            }),
            ("Old tab revision cannot confirm a changed cart", () =>
            {
                var (repo, service, cart) = Ready();
                var controller = Controller(service, cart, () => new FixturePrinter());
                controller.UpdateQuantity(1, 1);
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Equal(0, repo.Sales.Count);
                Equal(10, repo.Products[1].StockQuantity);
            }),
            ("Session write failure prevents a new untracked payment attempt", () =>
            {
                var (repo, service, cart) = Ready();
                var controller = Controller(service, cart, () => new FixturePrinter());
                ((FixtureSession)controller.HttpContext.Session).FailSet = true;
                controller.Finalize("Cash", cart.InvoiceNumber, cart.Revision);
                Equal(0, repo.Sales.Count);
                Equal(10, repo.Products[1].StockQuantity);
            }),
            ("Resume session failure leaves recoverable order in SQL", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var id = repo.Sales.Single().SaleId;
                var empty = new CheckoutSessionDTO();
                var controller = Controller(service, empty, () => new FixturePrinter());
                ((FixtureSession)controller.HttpContext.Session).FailSet = true;
                controller.ResumeOrder(id);
                Equal("Resumed", repo.Sales.Single().OrderStatus);
                Equal(10, repo.Products[1].StockQuantity);
                Equal(id, service.GetHeldOrders().Single().SaleId);
            }),
            ("Adding an oversized amount leaves previous cart intact", () =>
            {
                var (repo, service, cart) = Setup();
                repo.Products[1].SellingPrice = 9999999999999999.99m;
                repo.Products[1].TaxRate = 0;
                service.AddToCart(cart, 1);
                Reject(() => service.AddToCart(cart, 1));
                Equal(1, cart.Items.Single().Quantity);
            }),
            ("Virtual receipt preview never truncates large amounts", () =>
            {
                var (_, _, cart) = Ready();
                cart.Items[0].UnitPrice = 1000000000000000.25m;
                cart.Items[0].TaxRate = 0;
                var preview = new VirtualPrinterService().GenerateReceiptPreview(cart);
                Check(preview.Contains(cart.TotalAmount.ToString("N2"), StringComparison.Ordinal));
                Check(preview.Contains(cart.Items[0].UnitPrice.ToString("N2"), StringComparison.Ordinal));
            }),
            ("Re-selecting active order preserves unsaved cart edits", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var resumed = service.ResumeOrder(repo.Sales.Single().SaleId);
                var controller = Controller(service, resumed, () => new FixturePrinter());
                controller.UpdateQuantity(1, 1);
                controller.ResumeOrder(resumed.PersistedSaleId!.Value);
                var active = JsonConvert.DeserializeObject<CheckoutSessionDTO>(controller.HttpContext.Session.GetString("ActiveCart")!)!;
                Equal(3, active.Items.Single().Quantity);
            }),
            ("Clearing stale resumed copy preserves a newer held reservation", () =>
            {
                var (repo, service, cart) = Ready();
                service.HoldOrder(cart, Actor);
                var resumed = service.ResumeOrder(repo.Sales.Single().SaleId);
                var stale = Copy(resumed);
                service.HoldOrder(resumed, Actor);
                service.CancelCart(stale);
                Equal("Held", repo.Sales.Single().OrderStatus);
                Equal(8, repo.Products[1].StockQuantity);
            }),
            ("Legacy direct print endpoint cannot print an unpaid cart", () =>
            {
                var (_, service, cart) = Ready();
                var controller = Controller(service, cart, () => throw new Exception("Must not construct printer"));
                Check(controller.TriggerHardwarePrint() is JsonResult);
            })
        ];
        var failures = 0;
        foreach (var (name, test) in cases)
        {
            try { test(); Console.WriteLine($"PASS {name}"); }
            catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
        }
        Console.WriteLine($"{cases.Length - failures}/{cases.Length} passed. No SQL connections, app host or physical devices used.");
        return failures == 0 ? 0 : 1;
    }

    private static (FixtureRepository, CheckoutService, CheckoutSessionDTO) Setup()
    {
        var repo = new FixtureRepository();
        repo.Products.Add(1, new Product
        {
            ProductId = 1, Barcode = "ABC-1", ProductName = "Fixture item", SellingPrice = 11.25m,
            CostPrice = 7.25m, TaxRate = 5.5m, StockQuantity = 10, IsActive = true
        });
        return (repo, new CheckoutService(repo), new CheckoutSessionDTO());
    }

    private static (FixtureRepository Repo, CheckoutService Service, CheckoutSessionDTO Cart) Ready()
    {
        var (repo, service, cart) = Setup();
        service.AddToCart(cart, 1, 2);
        return (repo, service, cart);
    }

    private static CheckoutController Controller(CheckoutService service, CheckoutSessionDTO cart, Func<IReceiptPrinter> printer)
    {
        var context = new DefaultHttpContext { Session = new FixtureSession() };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Name, "Cashier")], "fixture"));
        context.Session.SetString("ActiveCart", JsonConvert.SerializeObject(cart));
        return new CheckoutController(service, printer, NullLogger<CheckoutController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new FixtureTempDataProvider())
        };
    }

    private static CheckoutSessionDTO Copy(CheckoutSessionDTO cart) =>
        JsonConvert.DeserializeObject<CheckoutSessionDTO>(JsonConvert.SerializeObject(cart))!;
    private static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
    private static void Reject(Action action) => Throws<CheckoutValidationException>(action);
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}

internal sealed class FixtureRepository : ICheckoutRepository, ICheckoutDataSession
{
    public Dictionary<int, Product> Products { get; private set; } = [];
    public Dictionary<int, Customer> Customers { get; private set; } = [];
    public List<Sale> Sales { get; private set; } = [];
    public List<LedgerEntry> Ledger { get; private set; } = [];
    public int Commits { get; private set; }
    public bool FailCommit { get; set; }
    public bool LoseAcknowledgement { get; set; }

    public IReadOnlyList<Product> GetAvailableProducts() => Products.Values.Where(p => p.IsActive && p.StockQuantity > 0).ToList();
    public Product? FindProduct(int id) => Products.GetValueOrDefault(id);
    public Product? FindProductByBarcode(string barcode) => Products.Values.SingleOrDefault(p => p.Barcode == barcode);
    public Customer? FindCustomerByPhone(string phone) => Customers.Values.SingleOrDefault(c => c.PhoneNumber == phone);
    public Customer? FindCustomer(int id) => Customers.GetValueOrDefault(id);
    public Sale? FindSale(string receipt) => Sales.SingleOrDefault(s => s.ReceiptNumber == receipt);
    public Sale? FindSale(int id) => Sales.SingleOrDefault(s => s.SaleId == id);
    public IReadOnlyList<Sale> GetRecoverableOrders() => Sales.Where(s => s.OrderStatus is "Held" or "Resumed").ToList();
    public void AddSale(Sale sale) { sale.SaleId = Sales.Select(s => s.SaleId).DefaultIfEmpty().Max() + 1; Sales.Add(sale); }
    public void ReplaceItems(Sale sale, IReadOnlyList<SaleItem> items) => sale.SaleItems = items.ToList();
    public void AddLedgerEntry(LedgerEntry entry) => Ledger.Add(entry);

    public T Execute<T>(Func<ICheckoutDataSession, T> operation)
    {
        var beforeProducts = Products;
        var beforeCustomers = Customers;
        var beforeSales = Sales;
        var beforeLedger = Ledger;
        Products = Products.ToDictionary(x => x.Key, x => Clone(x.Value));
        Customers = Customers.ToDictionary(x => x.Key, x => Clone(x.Value));
        Sales = Sales.Select(Clone).ToList();
        Ledger = Ledger.Select(Clone).ToList();
        T result;
        try
        {
            result = operation(this);
            if (FailCommit) { FailCommit = false; throw new IOException("fixture commit failure"); }
            foreach (var sale in Sales)
            {
                var original = beforeSales.SingleOrDefault(s => s.SaleId == sale.SaleId);
                if (original is null || Signature(original) != Signature(sale))
                    sale.RowVersion = BitConverter.GetBytes(Commits + 1);
            }
            Commits++;
        }
        catch
        {
            Products = beforeProducts; Customers = beforeCustomers; Sales = beforeSales; Ledger = beforeLedger;
            throw;
        }
        if (LoseAcknowledgement) { LoseAcknowledgement = false; throw new IOException("fixture acknowledgement lost after commit"); }
        return result;
    }

    private static string Signature(Sale sale) => $"{sale.OrderStatus}|{sale.TotalAmount}|{sale.PaymentMethod}|{sale.CustomerId}|{sale.CashierName}|{string.Join(';', sale.SaleItems.Select(i => $"{i.ProductId}:{i.Quantity}:{i.UnitPrice}:{i.LineTotal}"))}";
    private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value,
        new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore }))!;
}

internal sealed class FixtureSession : ISession
{
    private readonly Dictionary<string, byte[]> values = [];
    public bool FailRemove { get; set; }
    public bool FailSet { get; set; }
    public string Id => "fixture";
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => values.Keys;
    public void Clear() => values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) { if (FailRemove) throw new IOException("fixture session failure"); values.Remove(key); }
    public void Set(string key, byte[] value) { if (FailSet) throw new IOException("fixture session write failure"); values[key] = value.ToArray(); }
    public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
}

internal sealed class FixtureTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

internal sealed class FixturePrinter : IReceiptPrinter
{
    public bool Fail { get; set; }
    public int Prints { get; private set; }
    public bool IsPrinterOnline() => true;
    public void PrintReceipt(CheckoutSessionDTO session) { Prints++; if (Fail) throw new IOException("fixture printer failure"); }
    public void OpenCashDrawer() { }
    public string GenerateReceiptPreview(CheckoutSessionDTO session) => "fixture preview";
    public void PrintZReport(ZReportDTO report) { }
}
