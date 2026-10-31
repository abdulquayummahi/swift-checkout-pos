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
    Sale? FindSale(string receiptNumber);
    T Execute<T>(Func<ICheckoutDataSession, T> operation);
}

public interface ICheckoutDataSession
{
    Product? FindProduct(int productId);
    Customer? FindCustomer(int customerId);
    Sale? FindSale(string receiptNumber);
    Sale? FindSale(int saleId);
    void AddSale(Sale sale);
    void ReplaceItems(Sale sale, IReadOnlyList<SaleItem> items);
    void AddLedgerEntry(LedgerEntry entry);
}
