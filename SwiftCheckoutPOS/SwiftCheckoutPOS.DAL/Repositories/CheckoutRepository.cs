using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories;

public sealed class CheckoutRepository(DbContextOptions<ApplicationDbContext> options) : ICheckoutRepository
{
    public IReadOnlyList<Product> GetAvailableProducts()
    {
        using var context = new ApplicationDbContext(options);
        return context.Products.AsNoTracking().Where(p => p.IsActive && p.StockQuantity > 0)
            .OrderBy(p => p.ProductName).ToList();
    }

    public Product? FindProduct(int productId)
    {
        using var context = new ApplicationDbContext(options);
        return context.Products.AsNoTracking().SingleOrDefault(p => p.ProductId == productId);
    }

    public Product? FindProductByBarcode(string barcode)
    {
        using var context = new ApplicationDbContext(options);
        return context.Products.AsNoTracking().SingleOrDefault(p => p.Barcode == barcode);
    }

    public IReadOnlyList<Sale> GetRecoverableOrders()
    {
        using var context = new ApplicationDbContext(options);
        // Never silently hide an old stock reservation. Expiry policy is still undecided.
        return context.Sales.AsNoTracking().Include(s => s.Customer).Include(s => s.SaleItems)
            .Where(s => s.OrderStatus == "Held" || s.OrderStatus == "Resumed")
            .OrderByDescending(s => s.SaleDate).ToList();
    }

    public Customer? FindCustomerByPhone(string phone)
    {
        using var context = new ApplicationDbContext(options);
        return context.Customers.AsNoTracking().FirstOrDefault(c => c.PhoneNumber == phone);
    }

    public Sale? FindSale(string receiptNumber)
    {
        using var context = new ApplicationDbContext(options);
        return context.Sales.AsNoTracking().Include(s => s.SaleItems).Include(s => s.Customer)
            .SingleOrDefault(s => s.ReceiptNumber == receiptNumber);
    }

    public T Execute<T>(Func<ICheckoutDataSession, T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        using var strategyContext = new ApplicationDbContext(options);
        var strategy = strategyContext.Database.CreateExecutionStrategy();

        T Attempt() => strategy.Execute(() =>
        {
            // Never reuse a modified/tracked graph across retry attempts.
            using var context = new ApplicationDbContext(options);
            using var transaction = context.Database.BeginTransaction();
            var result = operation(new DataSession(context));
            context.SaveChanges();
            transaction.Commit();
            return result;
        });

        try
        {
            return Attempt();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A simultaneous insertion may have won the receipt's unique index.
            // Re-enter once with fresh state: BLL must verify the stored outcome,
            // including its payload, rather than treating any duplicate as success.
            return Attempt();
        }
    }

    private sealed class DataSession(ApplicationDbContext context) : ICheckoutDataSession
    {
        public Product? FindProduct(int productId) => context.Products.Find(productId);
        public Customer? FindCustomer(int customerId) => context.Customers.Find(customerId);
        public Sale? FindSale(string receiptNumber) => Sales().SingleOrDefault(s => s.ReceiptNumber == receiptNumber);
        public Sale? FindSale(int saleId) => Sales().SingleOrDefault(s => s.SaleId == saleId);
        private IQueryable<Sale> Sales() => context.Sales.Include(s => s.SaleItems).Include(s => s.Customer);
        public void AddSale(Sale sale) => context.Sales.Add(sale);
        public void AddLedgerEntry(LedgerEntry entry) => context.LedgerEntries.Add(entry);

        public void ReplaceItems(Sale sale, IReadOnlyList<SaleItem> items)
        {
            context.SaleItems.RemoveRange(sale.SaleItems);
            sale.SaleItems = items.ToList();
        }
    }
}
