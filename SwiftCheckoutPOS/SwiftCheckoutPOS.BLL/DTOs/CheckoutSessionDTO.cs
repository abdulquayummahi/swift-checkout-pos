// Project: SwiftCheckoutPOS.BLL
// File: DTOs/CheckoutSessionDTO.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class CheckoutSessionDTO
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string InvoiceNumber { get; set; } = CreateInvoiceNumber();
        public string Revision { get; set; } = Guid.NewGuid().ToString("N");
        public bool AwaitingConfirmation { get; set; }
        public string? PendingPaymentMethod { get; set; }

        // A resumed order retains its SQL copy until completion/cancellation.
        public int? PersistedSaleId { get; set; }
        public string? PersistedRowVersion { get; set; }
        public string? ReceiptCashierName { get; set; }
        public DateTime? ReceiptSaleDate { get; set; }
        public string? ReceiptPaymentMethod { get; set; }

        public string CustomerType { get; set; } = "Walk-in";
        public string CustomerName { get; set; } = "";
        public int? CustomerId { get; set; }

        public decimal BillDiscountPercentage { get; set; } = 0;
        public decimal BillDiscountAmount { get; set; } = 0;

        public List<CartItemDTO> Items { get; set; } = new List<CartItemDTO>();

        public decimal GrossSubTotal => Math.Round(Items.Sum(i => i.LineTotal), 2);

        public decimal CalculatedDiscount
        {
            get
            {
                decimal discount = 0;
                if (BillDiscountAmount > 0) discount = BillDiscountAmount;
                else if (BillDiscountPercentage > 0) discount = GrossSubTotal * (BillDiscountPercentage / 100);

                // ARCHITECT NOTE: Strict Business Logic. 
                // A discount can never exceed the subtotal of the cart.
                return Math.Round(Math.Min(discount, GrossSubTotal), 2);
            }
        }

        public decimal NetSubTotal => GrossSubTotal - CalculatedDiscount > 0 ? GrossSubTotal - CalculatedDiscount : 0;

        public decimal TaxAmount
        {
            get
            {
                if (GrossSubTotal <= 0) return 0;

                var tax = Items.Sum(item =>
                {
                    var allocatedDiscount = CalculatedDiscount * (item.LineTotal / GrossSubTotal);
                    var taxableAmount = Math.Max(0, item.LineTotal - allocatedDiscount);
                    return taxableAmount * Math.Clamp(item.TaxRate, 0, 100) / 100;
                });

                return Math.Round(tax, 2);
            }
        }

        public decimal TotalAmount => Math.Round(NetSubTotal + TaxAmount, 2);

        private static string CreateInvoiceNumber() =>
            $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
