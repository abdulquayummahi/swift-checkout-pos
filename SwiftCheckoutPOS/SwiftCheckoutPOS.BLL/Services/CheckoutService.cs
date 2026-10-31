using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;
using System.Diagnostics.CodeAnalysis;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>
/// Restores the existing checkout use cases without changing the DTO's tax,
/// discount or rounding formulas. Money/stock persistence is a single DAL unit.
/// </summary>
public sealed class CheckoutService(ICheckoutRepository repository)
{
    private const decimal MaxMoney = 9999999999999999.99m; // Existing decimal(18,2).
    private const string Held = "Held";
    private const string Resumed = "Resumed";
    private const string Completed = "Completed";
    private const string Cancelled = "Cancelled";

    public List<ProductDTO> GetAvailableProducts() => repository.GetAvailableProducts().Select(p => new ProductDTO
    {
        ProductId = p.ProductId, Barcode = p.Barcode, ProductName = p.ProductName,
        CategoryId = p.CategoryId, VendorId = p.VendorId, SellingPrice = p.SellingPrice,
        CostPrice = p.CostPrice, TaxRate = p.TaxRate, StockQuantity = p.StockQuantity,
        IsActive = p.IsActive, ImagePath = p.ImagePath, UnitOfMeasure = p.UnitOfMeasure
    }).ToList();

    public List<HeldOrderDTO> GetHeldOrders() => repository.GetRecoverableOrders().Select(s => new HeldOrderDTO
    {
        SaleId = s.SaleId, ReceiptNumber = s.ReceiptNumber, SaleDate = s.SaleDate,
        TotalAmount = s.TotalAmount, CustomerName = s.Customer?.FullName,
        TotalQuantity = s.SaleItems.Sum(i => (long)i.Quantity), Status = s.OrderStatus
    }).ToList();

    public void AddToCartByBarcode(CheckoutSessionDTO cart, string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Trim().Length > 50)
            Fail("Enter a valid product barcode.");
        var product = repository.FindProductByBarcode(barcode.Trim());
        if (product is null) Fail("No product matches this barcode.");
        AddProduct(cart, product!, 1);
    }

    public void AddToCart(CheckoutSessionDTO cart, int productId, int quantity = 1)
    {
        var product = repository.FindProduct(productId);
        if (product is null) Fail("Product not found.");
        AddProduct(cart, product!, quantity);
    }

    private static void AddProduct(CheckoutSessionDTO cart, Product product, int quantity)
    {
        ValidateCart(cart, requireItems: false);
        ValidateProduct(product);
        if (quantity <= 0) Fail("Quantity to add must be greater than zero.");
        var existing = cart.Items.SingleOrDefault(i => i.ProductId == product.ProductId);
        var requested = (long)(existing?.Quantity ?? 0) + quantity;
        if (requested > int.MaxValue || requested > product.StockQuantity)
            Fail("Not enough stock. Reduce the requested quantity.");
        var previousQuantity = existing?.Quantity ?? 0;
        if (existing is not null)
        {
            CheckCurrentPrice(existing, product);
            existing.Quantity = (int)requested;
        }
        else
        {
            cart.Items.Add(new CartItemDTO
            {
                ProductId = product.ProductId, Barcode = product.Barcode, ProductName = product.ProductName,
                UnitPrice = product.SellingPrice, TaxRate = product.TaxRate, Quantity = quantity
            });
        }
        try { ValidateCart(cart, requireItems: false); }
        catch
        {
            if (existing is not null) existing.Quantity = previousQuantity;
            else cart.Items.RemoveAll(i => i.ProductId == product.ProductId);
            throw;
        }
    }

    public void UpdateQuantity(CheckoutSessionDTO cart, int productId, int change)
    {
        ValidateCart(cart, requireItems: false);
        if (change > 0) { AddToCart(cart, productId, change); return; }
        var item = cart.Items.SingleOrDefault(i => i.ProductId == productId);
        if (item is null) Fail("This product is not in the cart.");
        var quantity = (long)item!.Quantity + change;
        if (quantity <= 0) cart.Items.Remove(item);
        else item.Quantity = (int)quantity;
    }

    public void RemoveItem(CheckoutSessionDTO cart, int productId)
    {
        ValidateCart(cart, requireItems: false);
        cart.Items.RemoveAll(i => i.ProductId == productId);
    }

    public void ApplyBillDiscount(CheckoutSessionDTO cart, decimal value, string type)
    {
        ValidateCart(cart, requireItems: false);
        if (value < 0 || value > MaxMoney) Fail("Enter a nonnegative discount within the supported amount.");
        if (type == "Percent")
        {
            if (value > 100) Fail("Percentage discount cannot exceed 100.");
            cart.BillDiscountPercentage = value;
            cart.BillDiscountAmount = 0;
        }
        else if (type == "Flat")
        {
            cart.BillDiscountAmount = value;
            cart.BillDiscountPercentage = 0;
        }
        else Fail("Choose Flat or Percent discount.");
    }

    public void SetCustomerType(CheckoutSessionDTO cart, string type)
    {
        if (type != "Walk-in" && type != "Loyalty") Fail("Choose Walk-in or Loyalty customer.");
        cart.CustomerType = type;
        if (type == "Walk-in") { cart.CustomerId = null; cart.CustomerName = ""; }
    }

    public CustomerDTO LinkCustomer(CheckoutSessionDTO cart, string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 30) Fail("Enter a valid loyalty phone number.");
        var customer = repository.FindCustomerByPhone(phone.Trim());
        if (customer is null) Fail("Customer not found.");
        cart.CustomerType = "Loyalty";
        cart.CustomerId = customer.CustomerId;
        cart.CustomerName = customer.FullName;
        return new CustomerDTO
        {
            CustomerId = customer.CustomerId, FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber, LoyaltyPoints = customer.LoyaltyPoints
        };
    }

    public void HoldOrder(CheckoutSessionDTO cart, string cashierName)
    {
        var command = Copy(cart);
        ValidateCart(command);
        ValidateCashier(cashierName);
        var saved = repository.Execute(data =>
        {
            var existing = data.FindSale(command.InvoiceNumber);
            if (existing?.OrderStatus == Held)
            {
                RequireSameSale(existing, command, "Pending", cashierName);
                return existing; // Lost acknowledgement/double submission: no second reservation.
            }
            CheckEditableOrder(existing, command);
            var items = BuildItems(data, command);
            CheckCustomer(data, command);
            var sale = existing ?? new Sale { ReceiptNumber = command.InvoiceNumber };
            SetSale(sale, command, "Pending", cashierName, Held);
            foreach (var item in items) data.FindProduct(item.ProductId)!.StockQuantity -= item.Quantity;
            if (existing is null)
            {
                sale.SaleItems = items;
                data.AddSale(sale);
            }
            else data.ReplaceItems(sale, items);
            return sale;
        });
        cart.PersistedSaleId = saved.SaleId;
        cart.PersistedRowVersion = Version(saved);
    }

    public CheckoutSessionDTO ResumeOrder(int saleId)
    {
        if (saleId <= 0) Fail("Choose a valid held order.");
        var saved = repository.Execute(data =>
        {
            var sale = data.FindSale(saleId);
            if (sale is null || (sale.OrderStatus != Held && sale.OrderStatus != Resumed))
                Fail("This order is no longer available to resume.");
            // Reconstruct/validate BEFORE releasing stock. Never delete the SQL copy.
            var cart = RestoreCart(data, sale!);
            if (sale!.OrderStatus == Held)
            {
                foreach (var item in sale.SaleItems)
                {
                    var product = data.FindProduct(item.ProductId);
                    if (product is null) Fail("An ordered product is missing. The held order has been preserved.");
                    var restored = (long)product!.StockQuantity + item.Quantity;
                    if (restored > int.MaxValue) Fail("Restoring this order would exceed the stock limit.");
                    product.StockQuantity = (int)restored;
                }
                sale.OrderStatus = Resumed;
            }
            return (Sale: sale, Cart: cart);
        });
        // Saved rowversion is populated by SaveChanges before Execute returns.
        saved.Cart.PersistedRowVersion = Version(saved.Sale);
        return saved.Cart;
    }

    public void CancelCart(CheckoutSessionDTO cart)
    {
        if (!cart.PersistedSaleId.HasValue) return; // Unsaved cart owns no stock reservation.
        repository.Execute(data =>
        {
            var sale = data.FindSale(cart.InvoiceNumber);
            // Clearing a stale local copy must not cancel somebody else's newer
            // reservation or financial record. Leave its durable copy untouched.
            if (sale?.SaleId != cart.PersistedSaleId || sale.OrderStatus != Resumed ||
                Version(sale) != cart.PersistedRowVersion) return true;
            sale.OrderStatus = Cancelled; // Resuming already released its reservation once.
            return true;
        });
    }

    public string FinalizeTransaction(CheckoutSessionDTO cart, string paymentMethod, string cashierName) =>
        CompleteSale(cart, paymentMethod, cashierName).ReceiptNumber;

    public CompletedCheckoutDTO CompleteSale(CheckoutSessionDTO cart, string paymentMethod, string cashierName)
    {
        var command = Copy(cart);
        ValidateCart(command);
        ValidateCashier(cashierName);
        ValidatePayment(paymentMethod);
        var result = repository.Execute(data =>
        {
            var existing = data.FindSale(command.InvoiceNumber);
            if (existing?.OrderStatus == Completed)
            {
                RequireSameSale(existing, command, paymentMethod, cashierName);
                return (Sale: existing, Replay: true);
            }
            CheckEditableOrder(existing, command);
            var items = BuildItems(data, command);
            CheckCustomer(data, command);
            var sale = existing ?? new Sale { ReceiptNumber = command.InvoiceNumber };
            SetSale(sale, command, paymentMethod, cashierName, Completed);
            foreach (var item in items) data.FindProduct(item.ProductId)!.StockQuantity -= item.Quantity;
            if (existing is null)
            {
                sale.SaleItems = items;
                data.AddSale(sale);
            }
            else data.ReplaceItems(sale, items);

            decimal cost = 0;
            foreach (var item in items)
            {
                var lineCost = item.CostAtSale * item.Quantity;
                if (lineCost > MaxMoney - cost) Fail("Cost of goods sold exceeds the supported amount.");
                cost += lineCost;
            }
            CheckMoney(cost, "Cost of goods sold");
            if (cost > 0)
                data.AddLedgerEntry(new LedgerEntry
                {
                    TransactionDate = sale.SaleDate, Category = "COGS", TransactionType = "Outflow",
                    Amount = cost, Description = $"System Auto-Record: Cost of Goods Sold. Ref: {sale.ReceiptNumber}",
                    RecordedBy = cashierName
                });
            // Owner decision: associate customer; do not invent point earning/redemption.
            return (Sale: sale, Replay: false);
        });
        command.ReceiptCashierName = result.Sale.CashierName;
        command.ReceiptSaleDate = result.Sale.SaleDate;
        command.ReceiptPaymentMethod = result.Sale.PaymentMethod;
        return new CompletedCheckoutDTO
        {
            SaleId = result.Sale.SaleId, ReceiptNumber = result.Sale.ReceiptNumber,
            AlreadyCompleted = result.Replay, Receipt = command
        };
    }

    public bool IsCompleted(string receiptNumber, string paymentMethod, string cashierName)
    {
        ValidatePayment(paymentMethod);
        var sale = repository.FindSale(receiptNumber);
        return sale?.OrderStatus == Completed && sale.PaymentMethod == paymentMethod && sale.CashierName == cashierName;
    }

    private static List<SaleItem> BuildItems(ICheckoutDataSession data, CheckoutSessionDTO command)
    {
        var items = new List<SaleItem>();
        foreach (var line in command.Items.OrderBy(i => i.ProductId))
        {
            var product = data.FindProduct(line.ProductId);
            if (product is null) Fail("An ordered product no longer exists. Review the cart.");
            ValidateProduct(product!);
            CheckCurrentPrice(line, product!);
            if (product!.StockQuantity < line.Quantity)
                Fail("Not enough stock. Reduce the requested quantity.");
            items.Add(new SaleItem
            {
                ProductId = line.ProductId, Quantity = line.Quantity, UnitPrice = product.SellingPrice,
                LineTotal = line.LineTotal, CostAtSale = product.CostPrice
            });
        }
        return items;
    }

    private static void SetSale(Sale sale, CheckoutSessionDTO cart, string payment, string actor, string status)
    {
        sale.SaleDate = DateTime.Now; // Preserve current local-time convention, not a historical date migration.
        sale.SubTotal = cart.GrossSubTotal;
        sale.TaxAmount = cart.TaxAmount;
        sale.TotalAmount = cart.TotalAmount;
        sale.CustomerId = cart.CustomerId;
        sale.PaymentMethod = payment;
        sale.CashierName = actor;
        sale.OrderStatus = status;
    }

    private static void CheckEditableOrder(Sale? sale, CheckoutSessionDTO cart)
    {
        if (sale is null)
        {
            if (cart.PersistedSaleId.HasValue) Fail("The saved order is missing. Reload held orders.");
            return;
        }
        if (sale.OrderStatus != Resumed || sale.SaleId != cart.PersistedSaleId ||
            !string.Equals(Version(sale), cart.PersistedRowVersion, StringComparison.Ordinal))
            Fail("This order changed or was already processed. Reload it before trying again.");
    }

    private static void RequireSameSale(Sale sale, CheckoutSessionDTO cart, string payment, string actor)
    {
        var lines = cart.Items.ToDictionary(i => i.ProductId);
        if (sale.SubTotal != cart.GrossSubTotal || sale.TaxAmount != cart.TaxAmount ||
            sale.TotalAmount != cart.TotalAmount || sale.CustomerId != cart.CustomerId ||
            sale.PaymentMethod != payment || sale.CashierName != actor ||
            sale.SaleItems.Count != lines.Count ||
            sale.SaleItems.Any(i => !lines.TryGetValue(i.ProductId, out var line) ||
                line.Quantity != i.Quantity || line.UnitPrice != i.UnitPrice || line.LineTotal != i.LineTotal))
            Fail("This receipt already belongs to a different request. Reload the checkout.");
    }

    private static CheckoutSessionDTO RestoreCart(ICheckoutDataSession data, Sale sale)
    {
        if (sale.OrderStatus != Held && sale.OrderStatus != Resumed)
            Fail("This order is no longer available to resume.");
        var cart = new CheckoutSessionDTO
        {
            InvoiceNumber = sale.ReceiptNumber, PersistedSaleId = sale.SaleId,
            PersistedRowVersion = Version(sale), CustomerId = sale.CustomerId,
            CustomerType = sale.CustomerId.HasValue ? "Loyalty" : "Walk-in",
            CustomerName = sale.Customer?.FullName ?? ""
        };
        foreach (var line in sale.SaleItems)
        {
            var product = data.FindProduct(line.ProductId);
            if (product is null || line.Quantity <= 0) Fail("The held order needs review. Its database copy is preserved.");
            var discount = line.UnitPrice - line.LineTotal / line.Quantity;
            if (discount < 0) Fail("The stored line cannot be reconstructed safely.");
            cart.Items.Add(new CartItemDTO
            {
                ProductId = line.ProductId, ProductName = product!.ProductName, Barcode = product.Barcode,
                Quantity = line.Quantity, UnitPrice = line.UnitPrice, ItemDiscount = discount,
                TaxRate = product.TaxRate
            });
        }
        cart.BillDiscountAmount = sale.SubTotal + sale.TaxAmount - sale.TotalAmount;
        ValidateCart(cart);
        // Legacy schema has no per-line tax/discount intent snapshot. Refuse any
        // reconstruction that changes persisted amounts instead of guessing policy.
        if (cart.GrossSubTotal != sale.SubTotal || cart.TaxAmount != sale.TaxAmount ||
            cart.TotalAmount != sale.TotalAmount ||
            cart.Items.Any(i => i.LineTotal != sale.SaleItems.Single(s => s.ProductId == i.ProductId).LineTotal))
            Fail("Held totals cannot be reproduced with current tax data. The order is preserved for review.");
        return cart;
    }

    private static void CheckCustomer(ICheckoutDataSession data, CheckoutSessionDTO cart)
    {
        if (cart.CustomerId is int id && data.FindCustomer(id) is null)
            Fail("The selected loyalty customer no longer exists. Select a valid customer.");
    }

    private static void ValidateProduct(Product product)
    {
        if (!product.IsActive) Fail("This product is inactive and cannot be sold.");
        CheckMoney(product.SellingPrice, "Selling price");
        CheckMoney(product.CostPrice, "Cost price");
        if (product.TaxRate < 0 || product.TaxRate > 100) Fail("Product tax rate needs correction before checkout.");
    }

    private static void CheckCurrentPrice(CartItemDTO line, Product product)
    {
        if (line.UnitPrice != product.SellingPrice || line.TaxRate != product.TaxRate)
            Fail("A price or tax rate changed. Remove and re-add the affected product before checkout.");
    }

    private static void ValidateCashier(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 100) Fail("A valid authenticated cashier is required.");
    }

    private static void ValidatePayment(string payment)
    {
        if (payment != "Cash" && payment != "Card" && payment != "Mobile Wallet")
            Fail("Choose Cash, Card or Mobile Wallet.");
    }

    private static void CheckMoney(decimal value, string field)
    {
        if (value < 0 || value > MaxMoney || Math.Round(value, 2) != value)
            Fail($"{field} is outside the supported monetary precision or range.");
    }

    private static void ValidateCart(CheckoutSessionDTO cart, bool requireItems = true)
    {
        if (cart is null || cart.Items is null) Fail("The cart is unavailable. Reload checkout.");
        if (!Guid.TryParseExact(cart!.SessionId, "N", out _) || string.IsNullOrWhiteSpace(cart.InvoiceNumber) ||
            cart.InvoiceNumber.Length > 50 || cart.InvoiceNumber.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            Fail("The cart identity is invalid. Reload checkout.");
        if (cart.CustomerType != "Walk-in" && cart.CustomerType != "Loyalty")
            Fail("Choose Walk-in or Loyalty customer.");
        if (cart.CustomerType == "Walk-in" && cart.CustomerId.HasValue)
            Fail("A walk-in cart cannot contain a linked loyalty customer.");
        if (requireItems && cart.CustomerType == "Loyalty" && cart.CustomerId is not > 0)
            Fail("Link a valid loyalty customer before holding or completing this order.");
        if (cart.Items!.Any(i => i is null) || cart.Items.Select(i => i.ProductId).Distinct().Count() != cart.Items.Count)
            Fail("The cart contains duplicate or invalid product lines.");
        if (requireItems && cart.Items.Count == 0) Fail("Add at least one item before processing an order.");
        foreach (var item in cart.Items)
        {
            if (item.ProductId <= 0 || item.Quantity <= 0) Fail("Product quantities must be positive.");
            CheckMoney(item.UnitPrice, "Unit price");
            if (item.ItemDiscount < 0 || item.ItemDiscount > item.UnitPrice || item.TaxRate < 0 || item.TaxRate > 100)
                Fail("A product discount or tax rate is invalid.");
        }
        if (cart.BillDiscountAmount < 0 || cart.BillDiscountAmount > MaxMoney ||
            cart.BillDiscountPercentage < 0 || cart.BillDiscountPercentage > 100)
            Fail("The bill discount is invalid.");
        try
        {
            CheckMoney(cart.GrossSubTotal, "Subtotal");
            CheckMoney(cart.TaxAmount, "Tax");
            CheckMoney(cart.TotalAmount, "Total");
        }
        catch (OverflowException) { Fail("The cart exceeds the supported amount."); }
    }

    private static string Version(Sale sale) => Convert.ToBase64String(sale.RowVersion ?? []);
    [DoesNotReturn]
    private static void Fail(string message) => throw new CheckoutValidationException(message);

    private static CheckoutSessionDTO Copy(CheckoutSessionDTO cart)
    {
        ValidateCart(cart);
        return new CheckoutSessionDTO
        {
            SessionId = cart.SessionId, InvoiceNumber = cart.InvoiceNumber,
            PersistedSaleId = cart.PersistedSaleId, PersistedRowVersion = cart.PersistedRowVersion,
            CustomerId = cart.CustomerId, CustomerType = cart.CustomerType, CustomerName = cart.CustomerName,
            BillDiscountAmount = cart.BillDiscountAmount, BillDiscountPercentage = cart.BillDiscountPercentage,
            Items = cart.Items.Select(i => new CartItemDTO
            {
                ProductId = i.ProductId, ProductName = i.ProductName, Barcode = i.Barcode,
                UnitPrice = i.UnitPrice, TaxRate = i.TaxRate, ItemDiscount = i.ItemDiscount, Quantity = i.Quantity
            }).ToList()
        };
    }
}
