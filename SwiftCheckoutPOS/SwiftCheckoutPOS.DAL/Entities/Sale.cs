// Project: SwiftCheckoutPOS.DAL
// File: Entities/Sale.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class Sale
    {
        [Key]
        public int SaleId { get; set; }

        /// <summary>Idempotency key from the cart (duplicate submissions find this, not a new sale).</summary>
        [MaxLength(50)]
        public string? OperationKey { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReceiptNumber { get; set; } = null!;

        public DateTime SaleDate { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = null!;

        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Required]
        [MaxLength(20)]
        public string OrderStatus { get; set; } = "Completed";

        // ARCHITECT NEW: Immutable Cashier Audit Log
        /// <summary>PR-Q07/Q91: whole-taka adjustment on the cash part (payable = TotalAmount + RoundingAmount).</summary>
        [Column(TypeName = "decimal(18, 2)")]
        public decimal RoundingAmount { get; set; }

        [MaxLength(100)]
        public string? CashierName { get; set; }

        /// <summary>BP-03/CQ-20: when the order was last put on hold; the 72h expiry clock runs only while Held.</summary>
        public DateTime? HeldAt { get; set; }

        /// <summary>BP-19: the till shift the sale was completed in.</summary>
        public int? ShiftId { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        /// <summary>Who approved a discount/Due/below-cost exception, if one was needed (BP-20/BP-17).</summary>
        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        /// <summary>Total exact value refunded/voided so far (always changes on a refund, so RowVersion guards repeats).</summary>
        [Column(TypeName = "decimal(18, 2)")]
        public decimal RefundedAmount { get; set; }

        public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

        public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
