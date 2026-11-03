using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories;

// Narrow checkout seam: BLL owns rules; DAL owns contexts, retries and commit.
public interface ICheckoutRepository
{
    IReadOnlyList<Product> GetAvailableProducts();
    Product? FindProduct(int productId);
    Product? FindProductByBarcode(string barcode);
    Customer? FindCustomerByPhone(string phone);
    IReadOnlyList<Sale> GetRecoverableOrders();
    /// <summary>Find by the cart's operation key (idempotency), not the printed receipt number.</summary>
    Sale? FindSale(string operationKey);
    Customer? FindCustomer(int customerId);
    T Execute<T>(Func<ICheckoutDataSession, T> operation);
}

public interface ICheckoutDataSession
{
    Product? FindProduct(int productId);
    Customer? FindCustomer(int customerId);
    Sale? FindSale(string operationKey);
    Sale? FindSale(int saleId);
    /// <summary>DR-26: next SCP-YYYYMMDD-NNNN number for the business day, inside the current transaction.</summary>
    string NextReceiptNumber(DateTime businessDay);
    void AddDuePayment(CustomerDuePayment payment);
    void AddAudit(AuditEvent audit);
    void AddSale(Sale sale);
    void ReplaceItems(Sale sale, IReadOnlyList<SaleItem> items);
    void AddLedgerEntry(LedgerEntry entry);
    /// <summary>Held orders put on hold before <paramref name="cutoff"/> (BP-03 expiry).</summary>
    IReadOnlyList<Sale> HeldBefore(DateTime cutoff);
}
