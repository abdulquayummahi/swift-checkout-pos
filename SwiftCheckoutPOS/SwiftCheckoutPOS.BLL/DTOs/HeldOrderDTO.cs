namespace SwiftCheckoutPOS.BLL.DTOs;

public sealed class HeldOrderDTO
{
    public int SaleId { get; init; }
    public string ReceiptNumber { get; init; } = "";
    public DateTime SaleDate { get; init; }
    public decimal TotalAmount { get; init; }
    public string? CustomerName { get; init; }
    public long TotalQuantity { get; init; }
    public string Status { get; init; } = "";
}

public sealed class CompletedCheckoutDTO
{
    public int SaleId { get; init; }
    public string ReceiptNumber { get; init; } = "";
    public bool AlreadyCompleted { get; init; }
    public CheckoutSessionDTO Receipt { get; init; } = new();
}
