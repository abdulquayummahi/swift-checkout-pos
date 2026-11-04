using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;
using System.Diagnostics.CodeAnalysis;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>
/// Checkout use cases. Money rules live in Pricing.Money (VAT-inclusive, half away from zero,
/// cash-only whole-taka rounding). Money/stock persistence is a single DAL unit.
/// </summary>
public sealed class CheckoutService(ICheckoutRepository repository, IApprover? approver = null)
{
    private const decimal MaxMoney = 9999999999999999.99m; // Existing decimal(18,2).
    private const string Held = "Held";
    private const string Resumed = "Resumed";
    private const string Completed = "Completed";
    private const string Cancelled = "Cancelled";

    public List<ProductDTO> GetAvailableProducts() => repository.GetAvailableProducts().Select(p => new ProductDTO
    {
        ProductId = p.ProductId, Barcode = p.Barcode, ProductName = p.ProductName,
        CategoryId = p.CategoryId, CategoryName = p.Category?.CategoryName ?? "", VendorId = p.VendorId, SellingPrice = p.SellingPrice,
        TaxRate = p.TaxRate, StockQuantity = p.StockQuantity - p.ReservedQuantity, // cost is never sent to the till
        IsActive = p.IsActive, ImagePath = p.ImagePath, UnitOfMeasure = p.UnitOfMeasure, ReorderLevel = p.ReorderLevel
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
        var available = Available(product) + cart.ReservedByOrder.GetValueOrDefault(product.ProductId);
        if (requested > int.MaxValue || requested > available)
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
            PhoneNumber = customer.PhoneNumber, LoyaltyPoints = customer.LoyaltyPoints, DueBalance = customer.DueBalance
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
                RequireSameSale(existing, command, "Pending", cashierName, 0m, null);
                return existing; // Lost acknowledgement/double submission: no second reservation.
            }
            CheckEditableOrder(existing, command);
            if (existing is not null) ReleaseReservation(data, existing); // re-hold: replace the old reservation
            var items = BuildItems(data, command);
            CheckCustomer(data, command);
            var sale = existing ?? new Sale { ReceiptNumber = command.InvoiceNumber, OperationKey = command.InvoiceNumber };
            SetSale(sale, command, "Pending", cashierName, Held, 0m);
            sale.HeldAt = DateTime.Now; // CQ-20: (re)holding starts a new expiry clock
            // PR-Q15/16: held units stay in owned stock but are reserved for this order.
            foreach (var item in items) data.FindProduct(item.ProductId)!.ReservedQuantity += item.Quantity;
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
            // CQ-04: resuming does NOT release stock; the reservation stays until completion or cancellation.
            sale!.OrderStatus = Resumed;
            cart.ReservedByOrder = sale.SaleItems.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
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
            ReleaseReservation(data, sale);
            sale.OrderStatus = Cancelled; // PR-Q17: kept in history, reservation released once.
            return true;
        });
    }

    public string FinalizeTransaction(CheckoutSessionDTO cart, string paymentMethod, string cashierName) =>
        CompleteSale(cart, paymentMethod, cashierName).ReceiptNumber;

    /// <summary>Single tender, no approval (Cashier limits apply).</summary>
    public CompletedCheckoutDTO CompleteSale(CheckoutSessionDTO cart, string paymentMethod, string cashierName) =>
        CompleteSale(cart, PaymentPlanDTO.Single(paymentMethod), cashierName, "Cashier", null);

    /// <summary>
    /// Completes a sale with up to two tenders (BP-17), the next SCP receipt number (DR-26), per-line discounts
    /// (DR-36) and role limits / Manager-Admin approval for discounts, Due and below-cost (BP-20, PR-Q67/86/93).
    /// <paramref name="belowCostMinRole"/> is Admin while Manager financial view is OFF (CQ-03).
    /// </summary>
    public CompletedCheckoutDTO CompleteSale(CheckoutSessionDTO cart, PaymentPlanDTO plan, string cashierName,
        string actorRole, ApprovalRequestDTO? approval, string belowCostMinRole = "Admin")
    {
        var command = Copy(cart);
        ValidateCart(command);
        ValidateCashier(cashierName);
        var (payments, rounding) = CheckoutRules.Resolve(plan, command.TotalAmount);
        var summary = plan.Summary;
        var due = payments.Where(p => p.Tender == "Due").Sum(p => p.Amount);
        if (due > 0 && command.CustomerId is null) Fail("Due needs a registered customer. Link the customer (Loyalty) first.");
        var lineDiscounts = CheckoutRules.LineDiscounts(command);
        var discountPercent = CheckoutRules.CombinedDiscountPercent(command);
        // Verify typed approver credentials once, outside the database transaction.
        var approverInfo = approval is { Username.Length: > 0 } && approver is not null
            ? approver.Verify(approval.Username!, approval.Password ?? "") : null;
        if (approval is { Username.Length: > 0 } && approverInfo is null) Fail("Approval failed: wrong approver username or password.");

        var result = repository.Execute(data =>
        {
            var existing = data.FindSale(command.InvoiceNumber);
            if (existing?.OrderStatus == Completed)
            {
                RequireSameSale(existing, command, summary, cashierName, rounding, payments);
                return (Sale: existing, Replay: true);
            }
            CheckEditableOrder(existing, command);
            if (existing is not null) ReleaseReservation(data, existing); // its own reservation becomes the sale
            var items = BuildItems(data, command);
            CheckCustomer(data, command);

            // --- approvals (role limits) ---
            var reasons = new List<string>(); var needRank = 0;
            if (discountPercent > CheckoutRules.DiscountLimitPercent(actorRole))
            {
                reasons.Add($"discount {discountPercent:0.##}% is above the {actorRole} limit");
                needRank = Math.Max(needRank, discountPercent <= CheckoutRules.DiscountLimitPercent("Manager") ? 2 : 3);
            }
            if (due > 0) { reasons.Add("Due sale"); needRank = Math.Max(needRank, 2); }
            var ordered = command.Items.OrderBy(i => i.ProductId).ToList(); // same order as items
            for (var k = 0; k < items.Count; k++)
            {
                var line = ordered[k];
                var netUnit = (line.UnitPrice * line.Quantity - lineDiscounts[command.Items.IndexOf(line)]) / line.Quantity;
                if (netUnit < items[k].CostAtSale) // PR-Q93/CQ-02: discount may not go below cost without approval
                {
                    reasons.Add("price below the allowed minimum");
                    needRank = Math.Max(needRank, CheckoutRules.Rank(belowCostMinRole));
                    break;
                }
            }
            string? approvedBy = null;
            if (needRank > 0 && CheckoutRules.Rank(actorRole) < needRank)
            {
                if (approverInfo is null || CheckoutRules.Rank(approverInfo.Value.Role) < needRank)
                    Fail($"APPROVAL REQUIRED: {string.Join(", ", reasons.Distinct())}. A {(needRank >= 3 ? "Admin" : "Manager or Admin")} must enter their username and password.");
                approvedBy = approverInfo.Value.Actor;
            }
            else if (needRank > 0) approvedBy = cashierName; // the signed-in Manager/Admin approves their own exception

            // --- Due (PR-Q86) ---
            if (due > 0)
            {
                var customer = data.FindCustomer(command.CustomerId!.Value)!;
                if (customer.DueBalance + due > CheckoutRules.DueLimit)
                    Fail($"Due limit exceeded: {customer.FullName} already owes {Pricing.Money.Bdt(customer.DueBalance)}; the maximum is {Pricing.Money.Bdt(CheckoutRules.DueLimit)}.");
                customer.DueBalance += due;
            }

            var sale = existing ?? new Sale { OperationKey = command.InvoiceNumber };
            SetSale(sale, command, summary, cashierName, Completed, rounding);
            sale.ShiftId = data.OpenShiftId() ?? Fail<int>("Open a shift first (Shift page) before taking payments.");
            sale.ReceiptNumber = data.NextReceiptNumber(sale.SaleDate); // DR-26: assigned only at completion
            sale.ApprovedBy = approvedBy;
            for (var k = 0; k < items.Count; k++)
                items[k].DiscountAmount = lineDiscounts[command.Items.IndexOf(ordered[k])];
            foreach (var item in items) data.FindProduct(item.ProductId)!.StockQuantity -= item.Quantity;
            sale.Payments = payments.Select(p => new SalePayment { Tender = p.Tender, Amount = p.Amount, Tendered = p.Tendered, Reference = p.Reference }).ToList();
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
            if (approvedBy is not null)
                data.AddAudit(new AuditEvent
                {
                    OccurredAt = sale.SaleDate, Actor = approvedBy, Action = "SaleApproval", EntityType = "Sale",
                    EntityKey = sale.ReceiptNumber, Reason = string.Join(", ", reasons.Distinct()),
                    Details = $"Cashier {cashierName}; discount {discountPercent:0.##}%; due {due:0.00}"
                });
            // Owner decision: associate customer; do not invent point earning/redemption.
            return (Sale: sale, Replay: false);
        });
        command.ReceiptCashierName = result.Sale.CashierName;
        command.ReceiptSaleDate = result.Sale.SaleDate;
        command.ReceiptPaymentMethod = result.Sale.PaymentMethod;
        command.ReceiptNumber = result.Sale.ReceiptNumber;
        command.ReceiptRounding = result.Sale.RoundingAmount;
        command.ReceiptPayments = result.Sale.Payments.Select(p => new ResolvedTenderDTO
        {
            Tender = p.Tender, Amount = p.Amount, Tendered = p.Tendered, Reference = p.Reference,
            Change = p.Tendered is decimal t ? t - p.Amount : 0
        }).ToList();
        return new CompletedCheckoutDTO
        {
            SaleId = result.Sale.SaleId, ReceiptNumber = result.Sale.ReceiptNumber,
            AlreadyCompleted = result.Replay, Receipt = command
        };
    }

    /// <summary>BP-17/CQ-10: money received against a customer's Due balance (any signed-in staff).</summary>
    public decimal CollectDue(int customerId, decimal amount, string tender, string? reference, string actor)
    {
        ValidateCashier(actor);
        if (amount <= 0 || Pricing.Money.R2(amount) != amount) Fail("Enter a valid amount.");
        if (tender is not ("Cash" or "Card" or "bKash" or "Nagad")) Fail("Collect due by Cash, Card, bKash or Nagad.");
        if (reference?.Length > 50) Fail("Reference is too long.");
        return repository.Execute(data =>
        {
            var customer = data.FindCustomer(customerId);
            if (customer is null) Fail("Customer not found.");
            if (amount > customer!.DueBalance) Fail($"The customer owes only {Pricing.Money.Bdt(customer.DueBalance)}.");
            var shiftId = data.OpenShiftId() ?? Fail<int>("Open a shift first (Shift page) before collecting money.");
            customer.DueBalance -= amount;
            var now = DateTime.Now;
            data.AddDuePayment(new CustomerDuePayment { CustomerId = customerId, Amount = amount, Tender = tender, Reference = reference, ReceivedAt = now, Actor = actor, ShiftId = shiftId });
            data.AddAudit(new AuditEvent { OccurredAt = now, Actor = actor, Action = "DueCollected", EntityType = "Customer", EntityKey = customerId.ToString(), Details = $"{amount:0.00} by {tender}" });
            return customer.DueBalance;
        });
    }

    public CustomerDTO FindCustomerByPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 30) Fail("Enter a valid phone number.");
        var c = repository.FindCustomerByPhone(phone.Trim());
        if (c is null) Fail("Customer not found.");
        return new CustomerDTO { CustomerId = c!.CustomerId, FullName = c.FullName, PhoneNumber = c.PhoneNumber, LoyaltyPoints = c.LoyaltyPoints, DueBalance = c.DueBalance };
    }

    public decimal GetDueBalance(int customerId) => repository.FindCustomer(customerId)?.DueBalance ?? 0m;

    public bool IsCompleted(string operationKey, string paymentMethod, string cashierName)
    {
        var sale = repository.FindSale(operationKey);
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
            if (Available(product!) < line.Quantity)
                Fail("Not enough stock. Reduce the requested quantity.");
            items.Add(new SaleItem
            {
                ProductId = line.ProductId, Quantity = line.Quantity, UnitPrice = product.SellingPrice,
                LineTotal = line.LineTotal, CostAtSale = product.CostPrice
            });
        }
        return items;
    }

    private static int Available(Product product) => product.StockQuantity - product.ReservedQuantity;

    private static void ReleaseReservation(ICheckoutDataSession data, Sale sale)
    {
        foreach (var item in sale.SaleItems)
        {
            var product = data.FindProduct(item.ProductId);
            if (product is null || product.ReservedQuantity < item.Quantity)
                Fail("Reserved stock for this order does not match. The order is preserved for review.");
            product!.ReservedQuantity -= item.Quantity;
        }
    }

    /// <summary>
    /// PR-Q18/CQ-20: cancel orders held longer than <paramref name="retentionHours"/> (0 = never),
    /// releasing their reservations. Orders stay in history as Cancelled. Returns how many expired.
    /// </summary>
    public int ExpireHeldOrders(int retentionHours, DateTime now)
    {
        if (retentionHours <= 0) return 0;
        return repository.Execute(data =>
        {
            var expired = data.HeldBefore(now.AddHours(-retentionHours));
            foreach (var sale in expired)
            {
                ReleaseReservation(data, sale);
                sale.OrderStatus = Cancelled;
            }
            return expired.Count;
        });
    }

    private static void SetSale(Sale sale, CheckoutSessionDTO cart, string payment, string actor, string status, decimal rounding)
    {
        sale.SaleDate = DateTime.Now; // Preserve current local-time convention, not a historical date migration.
        sale.SubTotal = cart.GrossSubTotal;
        sale.TaxAmount = cart.TaxAmount;
        sale.TotalAmount = cart.TotalAmount;
        sale.RoundingAmount = rounding;
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

    private static void RequireSameSale(Sale sale, CheckoutSessionDTO cart, string payment, string actor,
        decimal rounding, List<ResolvedTenderDTO>? payments)
    {
        var lines = cart.Items.ToDictionary(i => i.ProductId);
        if (payments is not null && (sale.Payments.Count != payments.Count ||
            payments.Any(p => !sale.Payments.Any(s => s.Tender == p.Tender && s.Amount == p.Amount))))
            Fail("This receipt already belongs to a different request. Reload the checkout.");
        if (sale.SubTotal != cart.GrossSubTotal || sale.TaxAmount != cart.TaxAmount ||
            sale.TotalAmount != cart.TotalAmount || sale.RoundingAmount != rounding ||
            sale.CustomerId != cart.CustomerId ||
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
        // VAT-inclusive (PR-Q08): the saved bill discount is subtotal minus total.
        cart.BillDiscountAmount = sale.SubTotal - sale.TotalAmount;
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

    private static void CheckMoney(decimal value, string field)
    {
        if (value < 0 || value > MaxMoney || Pricing.Money.R2(value) != value)
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
        if (cart.BillDiscountAmount > 0 && cart.BillDiscountPercentage > 0)
            Fail("Use either a flat or a percentage bill discount, not both."); // PR-Q12
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
    [DoesNotReturn]
    private static T Fail<T>(string message) => throw new CheckoutValidationException(message);

    private static CheckoutSessionDTO Copy(CheckoutSessionDTO cart)
    {
        ValidateCart(cart);
        return new CheckoutSessionDTO
        {
            SessionId = cart.SessionId, InvoiceNumber = cart.InvoiceNumber,
            PersistedSaleId = cart.PersistedSaleId, PersistedRowVersion = cart.PersistedRowVersion,
            ReservedByOrder = new Dictionary<int, int>(cart.ReservedByOrder ?? new()),
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
