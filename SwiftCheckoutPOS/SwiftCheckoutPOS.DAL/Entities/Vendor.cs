// Project: SwiftCheckoutPOS.DAL
// File: Entities/Vendor.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class Vendor
    {
        [Key]
        public int VendorId { get; set; }

        [Required]
        [MaxLength(150)]
        public string VendorName { get; set; } = null!;

        [MaxLength(100)]
        public string? ContactName { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}