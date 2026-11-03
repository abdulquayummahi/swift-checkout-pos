// Project: SwiftCheckoutPOS.BLL
// File: DTOs/CheckoutSessionDTO.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCheckoutPOS.BLL.Pricing;

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
        /// <summary>BP-03: units this resumed order still reserves, per product (CQ-04).</summary>
        public Dictionary<int, int> ReservedByOrder { get; set; } = new();
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

        // OWNER_DECISIONS A–B: prices are VAT-inclusive; all rounding is 2 decimals, half away from zero.
        public decimal GrossSubTotal => R2(Items.Sum(i => i.LineTotal));

        public decimal CalculatedDiscount
        {
            get
            {
                decimal discount = 0;
                if (BillDiscountAmount > 0) discount = BillDiscountAmount;
                else if (BillDiscountPercentage > 0) discount = GrossSubTotal * (BillDiscountPercentage / 100);
                // A discount can never exceed the subtotal of the cart.
                return R2(Math.Min(discount, GrossSubTotal));
            }
        }

        public decimal NetSubTotal => GrossSubTotal - CalculatedDiscount > 0 ? GrossSubTotal - CalculatedDiscount : 0;

        /// <summary>
        /// PR-Q05/08/09: VAT contained in the VAT-inclusive bill. The bill discount is spread across lines in
        /// proportion (CQ-17 residual rule), then VAT is calculated for the whole bill and rounded once.
        /// The shop is not VAT-registered (PR-Q10), so with 0% rates this is 0.
        /// </summary>
        public decimal TaxAmount
        {
            get
            {
                if (GrossSubTotal <= 0) return 0;
                var shares = Money.Allocate(CalculatedDiscount, Items.Select(i => i.LineTotal).ToList());
                decimal vat = 0;
                for (var i = 0; i < Items.Count; i++)
                    vat += Money.VatIncluded(Math.Max(0, Items[i].LineTotal - shares[i]), Math.Clamp(Items[i].TaxRate, 0, 100));
                return R2(vat);
            }
        }

        /// <summary>Exact payable amount, VAT included. Cash rounding is separate (PR-Q07/Q91).</summary>
        public decimal TotalAmount => R2(NetSubTotal);

        /// <summary>Whole-taka adjustment for a cash payment (PR-Q07/Q91/CQ-01); 0 for other tenders.</summary>
        public decimal RoundingFor(string? paymentMethod) =>
            Money.IsCash(paymentMethod) ? Money.CashRounding(TotalAmount) : 0m;

        private static decimal R2(decimal value) => Money.R2(value);

        private static string CreateInvoiceNumber() =>
            $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
