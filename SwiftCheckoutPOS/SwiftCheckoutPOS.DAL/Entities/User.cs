// Project: SwiftCheckoutPOS.DAL
// File: Entities/User.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = null!;

        // ARCHITECT FIX: Added to support the new Employee UI
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        // ARCHITECT FIX: Added to support offline PBKDF2 cryptography
        [Required]
        [MaxLength(50)]
        public string PasswordSalt { get; set; } = null!;

        // ARCHITECT FIX: Converted from a custom class/enum to a strict string. 
        // This resolves the "Cannot implicitly convert type 'string' to 'Role'" exception.
        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = null!;

        public bool IsActive { get; set; }
    }
}