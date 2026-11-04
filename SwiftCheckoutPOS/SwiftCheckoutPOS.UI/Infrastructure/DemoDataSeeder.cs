using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// Fills an EMPTY database with a realistic small Dhaka grocery shop: staff, products, customers and several weeks of
/// shifts, sales, refunds, a void, Due sales and collections, restocks, expenses and capital. Everything goes through the
/// real services (so stock, average cost, ledger, receipts, payments and audit are exactly what the till would write);
/// only the timestamps are moved back to the simulated day afterwards. For trying the system out, never for a live shop.
/// Run: SwiftCheckoutPOS.UI.exe --seed-demo   (or: dotnet run --project SwiftCheckoutPOS.UI -- --seed-demo)
/// </summary>
public sealed class DemoDataSeeder
{
    public sealed record DemoLogin(string Username, string Password, string Role, string FullName);

    /// <summary>Demo sign-ins printed at the end. The owner account is created only when the database has no Admin yet.</summary>
    public static readonly DemoLogin Owner = new("owner", "Owner-Demo-2026", "Admin", "Shop Owner");
    public static readonly DemoLogin[] Staff =
    [
        new("nasrin", "Manager-Demo-2026", "Manager", "Nasrin Akter"),
        new("rahim", "Cashier-Demo-2026", "Cashier", "Rahim Uddin"),
        new("sumaiya", "Cashier2-Demo-2026", "Cashier", "Sumaiya Islam"),
    ];

    private sealed record Item(string Barcode, string Name, string Category, string Unit, decimal Cost, decimal Price, int Stock, int Reorder, int Weight);
    private static readonly Item[] Catalogue =
    [
        new("8941000000017", "Aromatic rice 5 kg", "Rice, flour and lentils", "bag", 760, 850, 30, 8, 5),
        new("8941000000024", "Miniket rice 5 kg", "Rice, flour and lentils", "bag", 380, 420, 40, 10, 7),
        new("8941000000031", "Atta 2 kg", "Rice, flour and lentils", "pack", 120, 135, 35, 8, 5),
        new("8941000000048", "Red lentils 1 kg", "Rice, flour and lentils", "pack", 118, 135, 40, 10, 6),
        new("8941000000055", "Soybean oil 1 L", "Oil, sugar and salt", "btl", 172, 189, 60, 12, 9),
        new("8941000000062", "Soybean oil 5 L", "Oil, sugar and salt", "btl", 830, 910, 15, 4, 2),
        new("8941000000079", "Sugar 1 kg", "Oil, sugar and salt", "pack", 128, 140, 50, 12, 7),
        new("8941000000086", "Salt 1 kg", "Oil, sugar and salt", "pack", 36, 42, 60, 10, 5),
        new("8941000000093", "Tea leaves 400 g", "Tea and drinks", "pack", 196, 230, 25, 6, 4),
        new("8941000000109", "Mineral water 500 ml", "Tea and drinks", "btl", 14, 20, 120, 24, 12),
        new("8941000000116", "Mineral water 1.5 L", "Tea and drinks", "btl", 24, 35, 60, 12, 6),
        new("8941000000123", "Mango juice 1 L", "Tea and drinks", "pack", 98, 120, 24, 6, 3),
        new("8941000000130", "UHT milk 1 L", "Tea and drinks", "pack", 82, 95, 40, 10, 6),
        new("8941000000147", "Soft drink 1 L", "Tea and drinks", "btl", 60, 75, 48, 12, 6),
        new("8941000000154", "Biscuits family pack", "Snacks", "pack", 47, 60, 50, 10, 7),
        new("8941000000161", "Chanachur 350 g", "Snacks", "pack", 90, 110, 30, 8, 4),
        new("8941000000178", "Muri (মুড়ি) 500 g", "Snacks", "pack", 55, 70, 30, 8, 4),
        new("8941000000185", "Instant noodles 4-pack", "Snacks", "pack", 72, 88, 40, 10, 6),
        new("8941000000192", "Potato chips 50 g", "Snacks", "pcs", 16, 20, 80, 15, 8),
        new("8941000000208", "Chocolate bar", "Snacks", "pcs", 42, 50, 40, 10, 5),
        new("8941000000215", "Toothpaste 100 g", "Personal care", "pcs", 78, 95, 30, 8, 4),
        new("8941000000222", "Bath soap 100 g", "Personal care", "pcs", 52, 65, 50, 12, 6),
        new("8941000000239", "Shampoo 180 ml", "Personal care", "btl", 210, 250, 20, 5, 2),
        new("8941000000246", "Detergent powder 1 kg", "Household", "pack", 140, 165, 25, 6, 3),
        new("8941000000253", "Dishwashing bar", "Household", "pcs", 22, 30, 40, 10, 4),
        new("8941000000260", "Mosquito coil (10)", "Household", "box", 65, 80, 30, 8, 3),
        new("8941000000277", "Matches (10 boxes)", "Household", "pack", 12, 15, 50, 10, 3),
        new("8941000000284", "AA batteries 2-pack", "Household", "pack", 45, 60, 30, 6, 2),
        new("8941000000291", "Eggs (dozen)", "Fresh", "dozen", 125, 145, 30, 8, 6),
        new("8941000000307", "Onion 1 kg", "Fresh", "kg", 85, 100, 40, 10, 6),
        new("8941000000314", "Potato 1 kg", "Fresh", "kg", 38, 50, 60, 12, 6),
    ];
    // Left without restock so the "low stock" views have something to show.
    private static readonly HashSet<string> NeverRestock = ["8941000000123", "8941000000239", "8941000000284"];

    private static readonly (string Name, string Phone, string Address)[] People =
    [
        ("Rahima Begum", "01711000201", "Mirpur 10, Dhaka"), ("Karim Uddin", "01711000202", "Mohammadpur, Dhaka"),
        ("Fatema Khatun", "01711000203", "Dhanmondi 27, Dhaka"), ("Abdul Hakim", "01711000204", "Uttara Sector 7, Dhaka"),
        ("Nusrat Jahan", "01711000205", "Banani, Dhaka"), ("Tanvir Ahmed", "01711000206", "Badda, Dhaka"),
        ("Shirin Akter", "01711000207", "Rampura, Dhaka"), ("Mizanur Rahman", "01711000208", "Khilgaon, Dhaka"),
        ("Sadia Afrin", "01711000209", "Shyamoli, Dhaka"), ("Habibur Rahman", "01711000210", "Farmgate, Dhaka"),
        ("Rokeya Sultana", "01711000211", "Malibagh, Dhaka"), ("Jahid Hasan", "01711000212", "Lalmatia, Dhaka"),
        ("Taslima Akhter", "01711000213", "Tejgaon, Dhaka"), ("Moinul Islam", "01711000214", "Bashundhara R/A, Dhaka"),
        ("Ayesha Siddiqa", "01711000215", "Gulshan 1, Dhaka"), ("Rafiqul Islam", "01711000216", "Mirpur 2, Dhaka"),
        ("Nazma Begum", "01711000217", "Kalabagan, Dhaka"), ("Shafiqul Alam", "01711000218", "Wari, Dhaka"),
        ("Mahmuda Khanam", "01711000219", "Mohakhali, Dhaka"), ("Arif Hossain", "01711000220", ""),
    ];

    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly Random _rnd = new(20261104);
    private readonly Action<string> _log;
    private readonly List<(DateTime Day, int SaleId, bool Damaged)> _refundPlan = new();
    private string _owner = "", _manager = "", _cashier1 = "", _cashier2 = "";
    private readonly Dictionary<string, int> _products = new();
    private readonly List<(int Id, string Phone)> _customers = new();
    public int Sales { get; private set; }
    public int Refunds { get; private set; }

    public DemoDataSeeder(string connectionString, Action<string>? log = null)
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()).Options;
        _log = log ?? (_ => { });
    }

    private ApplicationDbContext Db() => new(_options);
    private IApprover Approver() => new ApprovalService(new AuthService(new UserRepository(Db())));
    private CheckoutService Checkout() => new(new CheckoutRepository(_options), Approver());
    private T With<T>(Func<ApplicationDbContext, T> work) { using var c = Db(); return work(c); }
    private void With(Action<ApplicationDbContext> work) { using var c = Db(); work(c); }
    private void Inventory(Action<InventoryService> work) => With(c => work(new InventoryService(new InventoryRepository(c), new FinanceService(new FinanceRepository(c), new SalesRepository(c)), new AtomicUnit(c))));
    private void Finance(Action<FinanceService> work) => With(c => work(new FinanceService(new FinanceRepository(c), new SalesRepository(c))));
    private T Shifts<T>(Func<ShiftService, T> work) => With(c => work(new ShiftService(new ShiftRepository(c), new AtomicUnit(c), Approver())));
    private T Refund<T>(Func<RefundService, T> work) => With(c => work(new RefundService(new SalesRepository(c), new AtomicUnit(c))));

    /// <summary>Refuses unless the database has no sales yet, so real data can never be mixed with demo data.</summary>
    public static bool IsEmpty(string connectionString)
    {
        using var c = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options);
        return !c.Sales.Any() && !c.Products.Any();
    }

    public void Run(int days = 40)
    {
        var today = DateTime.Today;
        var start = today.AddDays(-days);
        CreateStaff();
        // Shop opened the day before the first sale: capital, shelves stocked, customers start registering.
        Stamped(start.AddDays(-1).AddHours(10), () => Finance(f => f.RecordManualEntry("Capital Investment", "", 600000m, "Owner's capital to open the shop", _owner)));
        Stamped(start.AddDays(-1).AddHours(12), CreateCatalogue);
        CreateCustomers(start);
        for (var day = start; day <= today; day = day.AddDays(1)) RunDay(day, day == today);
        _log($"Done: {Sales} sales over {days + 1} days, {Refunds} refunds, {_products.Count} products, {_customers.Count} customers.");
    }

    // ------------------------------------------------------------------ set-up

    private void CreateStaff()
    {
        With(c =>
        {
            var auth = new AuthService(new UserRepository(c));
            if (auth.NeedsFirstAdmin()) auth.CreateFirstAdmin(Owner.Username, Owner.FullName, Owner.Password);
        });
        With(c =>
        {
            var users = new UserAdminService(new UserRepository(c));
            foreach (var s in Staff.Where(s => !c.Users.Any(u => u.Username == s.Username)))
                users.CreateUser(new UserActionDTO { Username = s.Username, FullName = s.FullName, Role = s.Role, PlainTextPassword = s.Password, IsActive = true });
        });
        With(c =>
        {
            string ActorOf(User u) => $"User {u.UserId}: {u.FullName}";
            var admin = c.Users.AsNoTracking().Where(u => u.Role == "Admin" && u.IsActive).OrderBy(u => u.UserId).First();
            _owner = ActorOf(admin);
            _manager = ActorOf(c.Users.AsNoTracking().Single(u => u.Username == "nasrin"));
            _cashier1 = ActorOf(c.Users.AsNoTracking().Single(u => u.Username == "rahim"));
            _cashier2 = ActorOf(c.Users.AsNoTracking().Single(u => u.Username == "sumaiya"));
        });
        _log("Staff accounts ready.");
    }

    private void CreateCatalogue()
    {
        var categories = new Dictionary<string, int>();
        int vendorWholesale = 0, vendorDistributor = 0;
        Inventory(i =>
        {
            vendorWholesale = i.AddVendor("Karwan Bazar Wholesale");
            vendorDistributor = i.AddVendor("Meghna Distributors");
            foreach (var cat in Catalogue.Select(p => p.Category).Distinct()) categories[cat] = i.AddCategory(cat);
        });
        foreach (var p in Catalogue)
        {
            Inventory(i => i.AddNewProduct(new ProductDTO
            {
                Barcode = p.Barcode, ProductName = p.Name, VendorId = p.Category is "Personal care" or "Household" ? vendorDistributor : vendorWholesale,
                CostPrice = p.Cost, SellingPrice = p.Price, StockQuantity = p.Stock, TaxRate = 0m, UnitOfMeasure = p.Unit, ReorderLevel = p.Reorder
            }, categories[p.Category], _owner));
        }
        With(c => { foreach (var p in c.Products.AsNoTracking()) _products[p.Barcode] = p.ProductId; });
        _log($"{_products.Count} products created.");
    }

    private void CreateCustomers(DateTime start)
    {
        With(c =>
        {
            var service = new CustomerService(new CustomerRepository(c), new SalesRepository(c));
            foreach (var p in People) service.AddNewCustomer(p.Name, p.Phone, p.Address);
        });
        With(c =>
        {
            var span = Math.Max(1, (DateTime.Today - start).Days);
            foreach (var cust in c.Customers.OrderBy(x => x.CustomerId).ToList())
            {
                // Half registered before opening (regulars), the rest during the period.
                var at = _rnd.Next(2) == 0 ? start.AddDays(-1).AddHours(14) : start.AddDays(_rnd.Next(span)).AddHours(11 + _rnd.Next(8));
                cust.RegisteredAt = at;
                _customers.Add((cust.CustomerId, cust.PhoneNumber));
            }
            c.SaveChanges();
        });
        _log($"{_customers.Count} customers created.");
    }

    // ------------------------------------------------------------------ one business day

    private void RunDay(DateTime day, bool isToday)
    {
        var dayStart = Mark();
        var cashier = day.Day % 2 == 0 ? _cashier1 : _cashier2;
        var close = day.AddHours(21).AddMinutes(30);
        var lastSaleAt = isToday ? DateTime.Now.AddMinutes(-15) : day.AddHours(21).AddMinutes(10);
        if (isToday && lastSaleAt < day.AddHours(9).AddMinutes(40)) lastSaleAt = day.AddHours(9).AddMinutes(40);

        MorningRestock(isToday && DateTime.Now < day.AddHours(9) ? DateTime.Now.AddMinutes(-30) : day.AddHours(8).AddMinutes(40));
        Expenses(day);

        var openAt = isToday && DateTime.Now < day.AddHours(9).AddMinutes(30) ? DateTime.Now.AddMinutes(-20) : day.AddHours(9);
        var shiftId = Shifts(s => s.Open(2000m, cashier));
        With(c => c.Shifts.Where(s => s.ShiftId == shiftId).ExecuteUpdate(u => u.SetProperty(s => s.OpenedAt, openAt)));
        StampAudit(dayStart, openAt);

        // Busier on Fridays and at the end of the month. Before 10:00 today there are no sales yet.
        var count = _rnd.Next(12, 20) + (day.DayOfWeek == DayOfWeek.Friday ? 6 : 0) + (day.Day >= 28 ? 3 : 0);
        if (isToday) count = DateTime.Now < day.AddHours(10) ? 0 : Math.Min(count, Math.Max(2, (int)(DateTime.Now - day.AddHours(9)).TotalHours * 2));
        var span = (lastSaleAt - day.AddHours(9).AddMinutes(20)).TotalMinutes;
        var times = Enumerable.Range(0, count).Select(_ => day.AddHours(9).AddMinutes(20 + _rnd.NextDouble() * Math.Max(span, 10))).OrderBy(t => t).ToList();
        var dueRefunds = _refundPlan.Where(r => r.Day == day).ToList();
        var refundAt = times.Count > 2 ? times[times.Count / 2] : day.AddHours(13);
        var voidDay = !isToday && day.Day == 15;

        foreach (var at in times)
        {
            var saleId = Sell(at, cashier, at.Hour >= 17);
            if (saleId is null) continue;
            if (_rnd.NextDouble() < 0.04) _refundPlan.Add((day.AddDays(1 + _rnd.Next(4)), saleId.Value, _rnd.NextDouble() < 0.25));
            if (voidDay) { voidDay = false; VoidSale(saleId.Value, at.AddMinutes(2)); }
            if (at >= refundAt && dueRefunds.Count > 0) { foreach (var r in dueRefunds) RefundSale(r.SaleId, r.Damaged, at.AddMinutes(1)); dueRefunds.Clear(); }
        }
        bool Past(DateTime t) => !isToday || t < DateTime.Now;
        var late = isToday ? DateTime.Now.AddMinutes(-5) : day.AddHours(18);
        foreach (var r in dueRefunds) RefundSale(r.SaleId, r.Damaged, late);

        var teaAt = day.AddHours(15).AddMinutes(_rnd.Next(50));
        if (_rnd.NextDouble() < 0.45 && Past(teaAt)) Stamped(teaAt, () => Shifts(s => { s.RecordCash("PaidOut", 50m * (1 + _rnd.Next(3)), "Tea and snacks for staff", cashier); return 0; }));
        if (day.Day % 6 == 0 && Past(day.AddHours(19).AddMinutes(10))) CollectDue(day.AddHours(19).AddMinutes(10), cashier);
        if (day.Day == 10 && Past(day.AddHours(16))) Stamped(day.AddHours(16), () => Shifts(s => { s.NoSale("Change for a 1000 taka note", _manager, "Manager"); return 0; }));
        if (day.Day == 12 && Past(day.AddHours(20))) Stamped(day.AddHours(20), () => Inventory(i => i.AdjustStock("8941000000253", Math.Max(0, Stock("8941000000253") - 2), "Damaged", "Two bars soaked in a leak", _manager)));
        if (day.Day == 20 && Past(day.AddHours(11))) Stamped(day.AddHours(11), () => Inventory(i => i.ReturnToVendor("8941000000130", 2, 164m, _manager)));

        RenumberReceipts(dayStart, day);
        if (isToday)
        {
            HoldOrders(cashier);
            return; // today's shift stays open
        }
        CloseShift(day, close, cashier);
    }

    private int? Sell(DateTime at, string cashier, bool evening)
    {
        var mark = Mark();
        var checkout = Checkout();
        var cart = new CheckoutSessionDTO();
        var lines = 1 + _rnd.Next(evening ? 6 : 4);
        for (var i = 0; i < lines; i++)
        {
            var p = Pick();
            try { checkout.AddToCart(cart, _products[p.Barcode], 1 + (_rnd.NextDouble() < 0.25 ? _rnd.Next(1, 3) : 0)); }
            catch (CheckoutValidationException) { /* sold out: the customer takes something else */ }
        }
        if (cart.Items.Count == 0) return null;

        var customer = _rnd.NextDouble() < 0.3 ? _customers[_rnd.Next(_customers.Count)] : default;
        if (customer.Id != 0) { checkout.SetCustomerType(cart, "Loyalty"); checkout.LinkCustomer(cart, customer.Phone); }
        if (_rnd.NextDouble() < 0.08) checkout.ApplyBillDiscount(cart, _rnd.Next(2) == 0 ? 5m : 10m, _rnd.Next(2) == 0 ? "Percent" : "Flat");

        var total = cart.TotalAmount;
        var roll = _rnd.NextDouble();
        PaymentPlanDTO plan;
        ApprovalRequestDTO? approval = null;
        if (customer.Id != 0 && roll < 0.06 && total >= 200 && DueOf(customer.Id) + total < 2000)
        {
            var due = Math.Round(total * 0.5m, 0);
            plan = new PaymentPlanDTO { Lines = { new TenderLineDTO { Tender = "Cash" }, new TenderLineDTO { Tender = "Due", Amount = due } } };
            approval = new ApprovalRequestDTO { Username = "nasrin", Password = Staff[0].Password };
        }
        else if (roll < 0.55) plan = PaymentPlanDTO.Single("Cash", Tendered(Math.Round(total, 0, MidpointRounding.AwayFromZero)));
        else if (roll < 0.75) plan = PaymentPlanDTO.Single("bKash", null, $"8N{_rnd.Next(10000000, 99999999)}");
        else if (roll < 0.86) plan = PaymentPlanDTO.Single("Card", null, _rnd.Next(1000, 9999).ToString());
        else if (roll < 0.96) plan = PaymentPlanDTO.Single("Nagad", null, $"7K{_rnd.Next(10000000, 99999999)}");
        else
        {
            var mobile = Math.Round(total * 0.6m, 0);
            plan = new PaymentPlanDTO { Lines = { new TenderLineDTO { Tender = "Cash" }, new TenderLineDTO { Tender = "bKash", Amount = mobile, Reference = $"8N{_rnd.Next(10000000, 99999999)}" } } };
        }
        try
        {
            var result = checkout.CompleteSale(cart, plan, cashier, "Cashier", approval);
            Stamp(mark, at);
            Sales++;
            return result.SaleId;
        }
        catch (Exception ex) when (ex is CheckoutValidationException or InvalidOperationException or ArgumentException)
        {
            return null; // nothing was saved or reserved for an unsaved cart
        }
    }

    private decimal? Tendered(decimal due)
    {
        if (_rnd.NextDouble() < 0.4) return due;
        foreach (var note in new[] { 50m, 100m, 500m, 1000m }) if (note >= due) return note;
        return Math.Ceiling(due / 1000m) * 1000m;
    }

    private Item Pick()
    {
        var total = Catalogue.Sum(p => p.Weight);
        var n = _rnd.Next(total);
        foreach (var p in Catalogue) { if (n < p.Weight) return p; n -= p.Weight; }
        return Catalogue[0];
    }

    private void MorningRestock(DateTime at)
    {
        foreach (var p in Catalogue.Where(p => !NeverRestock.Contains(p.Barcode)))
        {
            var stock = Stock(p.Barcode);
            if (stock > p.Reorder) continue;
            var qty = p.Stock;
            var unitCost = Math.Round(p.Cost * (decimal)(0.97 + _rnd.NextDouble() * 0.06), 2);
            decimal? newPrice = null;
            if (p.Barcode == "8941000000055" && at > DateTime.Today.AddDays(-15)) newPrice = 195m; // oil price went up
            Stamped(at, () => Inventory(i => i.RestockProduct(p.Barcode, qty, Math.Round(unitCost * qty, 2), newPrice, _manager)));
        }
    }

    private void Expenses(DateTime day)
    {
        void Pay(string category, decimal amount, string note, int hour)
        {
            if (day.AddHours(hour) > DateTime.Now) return; // not yet happened today
            Stamped(day.AddHours(hour), () => Finance(f => f.RecordManualEntry(category, "", amount, note, _owner)));
        }
        if (day.Day == 1) Pay("Rent", 25000m, $"Shop rent for {day:MMMM}", 10);
        if (day.Day == 5) Pay("Utilities", 4200m + _rnd.Next(0, 9) * 100m, "Electricity and water bill", 11);
        if (day.Day == 30 || (day.Day == 28 && day.Month == 2)) Pay("Salaries", 36000m, "Staff salaries", 20);
        if (day.Day % 7 == 3) Pay("Transport", 300m + _rnd.Next(0, 6) * 100m, "Van hire for wholesale pickup", 8);
        if (day.Day == 18) Pay("Maintenance", 1500m, "Freezer repair", 14);
    }

    private void RefundSale(int saleId, bool damaged, DateTime at)
    {
        var (receipt, line) = With(c =>
        {
            var sale = c.Sales.AsNoTracking().Include(s => s.SaleItems).Single(s => s.SaleId == saleId);
            return (sale.ReceiptNumber, sale.SaleItems.OrderByDescending(i => i.LineTotal).First());
        });
        var mark = Mark();
        try
        {
            Refund(r => r.ExecuteRefund(receipt, [new RefundRequestLineDTO { SaleItemId = line.SaleItemId, Quantity = 1, Disposition = damaged ? "Damaged" : "Restock" }],
                _manager, damaged ? "Packet was torn" : "Customer changed their mind", at));
            Stamp(mark, at);
            Refunds++;
        }
        catch (InvalidOperationException) { /* already refunded or outside the window */ }
    }

    private void VoidSale(int saleId, DateTime at)
    {
        var receipt = With(c => c.Sales.AsNoTracking().Single(s => s.SaleId == saleId).ReceiptNumber);
        var mark = Mark();
        Refund(r => r.VoidSale(receipt, "Rang up the wrong items", _manager, at));
        Stamp(mark, at);
    }

    private void CollectDue(DateTime at, string cashier)
    {
        var owing = With(c => c.Customers.AsNoTracking().Where(x => x.DueBalance > 0).OrderByDescending(x => x.DueBalance).Select(x => new { x.CustomerId, x.DueBalance }).FirstOrDefault());
        if (owing is null) return;
        var amount = Math.Max(1m, Math.Round(owing.DueBalance * 0.6m, 0));
        Stamped(at, () => Checkout().CollectDue(owing.CustomerId, Math.Min(amount, owing.DueBalance), "Cash", null, cashier));
    }

    private void CloseShift(DateTime day, DateTime at, string cashier)
    {
        var expected = Shifts(s => s.Current()!.ExpectedCash);
        var mark = Mark();
        ShiftSummaryDTO closed;
        if (day.Day == 22 && expected > 1200m)
        {
            // One evening the drawer was ৳1,200 short: a Manager approved the difference.
            closed = Shifts(s => s.Close(expected - 1200m, "Short: a 1000 note may have been given as change", cashier, "Cashier", "nasrin", Staff[0].Password));
        }
        else
        {
            var diff = _rnd.NextDouble() < 0.7 ? 0m : (decimal)_rnd.Next(-60, 30);
            closed = Shifts(s => s.Close(Math.Max(0, expected + diff), diff == 0 ? null : "Counted twice", cashier, "Cashier"));
        }
        With(c => c.Shifts.Where(s => s.ShiftId == closed.ShiftId).ExecuteUpdate(u => u.SetProperty(s => s.ClosedAt, at)));
        Stamp(mark, at);
    }

    private void HoldOrders(string cashier)
    {
        foreach (var (minutes, barcodes) in new[] { (25, new[] { "8941000000024", "8941000000055", "8941000000079" }), (6, new[] { "8941000000109", "8941000000192", "8941000000208" }) })
        {
            var checkout = Checkout();
            var cart = new CheckoutSessionDTO();
            foreach (var b in barcodes) { try { checkout.AddToCart(cart, _products[b], 1); } catch (CheckoutValidationException) { } }
            if (cart.Items.Count == 0) continue;
            var mark = Mark();
            checkout.HoldOrder(cart, cashier);
            Stamp(mark, DateTime.Now.AddMinutes(-minutes));
        }
    }

    // ------------------------------------------------------------------ moving timestamps to the simulated day

    private sealed record Marks(int Sale, int Ledger, long Audit, int Cash, int Due, int Refund);

    private Marks Mark() => With(c => new Marks(
        c.Sales.Max(s => (int?)s.SaleId) ?? 0, c.LedgerEntries.Max(e => (int?)e.EntryId) ?? 0, c.AuditEvents.Max(a => (long?)a.AuditEventId) ?? 0,
        c.CashMovements.Max(m => (int?)m.CashMovementId) ?? 0, c.CustomerDuePayments.Max(d => (int?)d.CustomerDuePaymentId) ?? 0, c.Refunds.Max(r => (int?)r.RefundId) ?? 0));

    private void Stamped(DateTime at, Action work) { var m = Mark(); work(); Stamp(m, at); }

    private void Stamp(Marks m, DateTime at) => With(c =>
    {
        c.Sales.Where(s => s.SaleId > m.Sale).ExecuteUpdate(u => u.SetProperty(s => s.SaleDate, at).SetProperty(s => s.HeldAt, s => s.HeldAt == null ? (DateTime?)null : at));
        c.LedgerEntries.Where(e => e.EntryId > m.Ledger).ExecuteUpdate(u => u.SetProperty(e => e.TransactionDate, at));
        c.AuditEvents.Where(a => a.AuditEventId > m.Audit).ExecuteUpdate(u => u.SetProperty(a => a.OccurredAt, at));
        c.CashMovements.Where(x => x.CashMovementId > m.Cash).ExecuteUpdate(u => u.SetProperty(x => x.At, at));
        c.CustomerDuePayments.Where(x => x.CustomerDuePaymentId > m.Due).ExecuteUpdate(u => u.SetProperty(x => x.ReceivedAt, at));
        c.Refunds.Where(x => x.RefundId > m.Refund).ExecuteUpdate(u => u.SetProperty(x => x.RefundedAt, at));
    });

    private void StampAudit(Marks m, DateTime at) => With(c => c.AuditEvents.Where(a => a.AuditEventId > m.Audit).ExecuteUpdate(u => u.SetProperty(a => a.OccurredAt, at)));

    /// <summary>Receipts were numbered with today's date by the real till code; give them the simulated day's date and sequence.</summary>
    private void RenumberReceipts(Marks dayStart, DateTime day)
    {
        var today = DateTime.Today;
        if (day.Date == today) return;
        With(c =>
        {
            var oldPrefix = $"SCP-{today:yyyyMMdd}-";
            var newPrefix = $"SCP-{day:yyyyMMdd}-";
            var sales = c.Sales.Where(s => s.SaleId > dayStart.Sale && s.ReceiptNumber.StartsWith(oldPrefix)).OrderBy(s => s.SaleId).ToList();
            var map = new List<(string Old, string New)>();
            var n = 0;
            foreach (var s in sales) { var fresh = newPrefix + (++n).ToString("D4"); map.Add((s.ReceiptNumber, fresh)); s.ReceiptNumber = fresh; }
            var seq = c.ReceiptSequences.SingleOrDefault(r => r.BusinessDate == today);
            if (seq is not null) c.ReceiptSequences.Remove(seq);
            if (n > 0) c.ReceiptSequences.Add(new ReceiptSequence { BusinessDate = day.Date, LastNumber = n });
            c.SaveChanges();
            foreach (var (o, fresh) in map)
            {
                c.LedgerEntries.Where(e => e.EntryId > dayStart.Ledger && e.Description != null && e.Description.Contains(o))
                    .ExecuteUpdate(u => u.SetProperty(e => e.Description, e => e.Description!.Replace(o, fresh)));
                c.AuditEvents.Where(a => a.AuditEventId > dayStart.Audit && (a.EntityKey == o || (a.Details != null && a.Details.Contains(o))))
                    .ExecuteUpdate(u => u.SetProperty(a => a.EntityKey, a => a.EntityKey.Replace(o, fresh)).SetProperty(a => a.Details, a => a.Details == null ? null : a.Details.Replace(o, fresh)));
            }
        });
    }

    private int Stock(string barcode) => With(c => c.Products.AsNoTracking().Where(p => p.Barcode == barcode).Select(p => p.StockQuantity - p.ReservedQuantity).Single());
    private decimal DueOf(int customerId) => With(c => c.Customers.AsNoTracking().Where(x => x.CustomerId == customerId).Select(x => x.DueBalance).Single());
}
