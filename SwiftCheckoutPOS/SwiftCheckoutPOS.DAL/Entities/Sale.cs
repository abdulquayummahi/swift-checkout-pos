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
        [MaxLength(100)]
        public string? CashierName { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
