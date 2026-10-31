// Project: SwiftCheckoutPOS.UI
// File: Models/UserViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.UI.Models
{
    public class UserViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Username is required for login.")]
        [StringLength(50, MinimumLength = 3)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Employee Full Name")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Role designation is required.")]
        [RegularExpression("^(Admin|Manager|Cashier)$", ErrorMessage = "Select a valid security role.")]
        public string Role { get; set; } = null!; // "Admin" or "Cashier"

        [Display(Name = "Account Password")]
        [DataType(DataType.Password)]
        [StringLength(128, MinimumLength = 12, ErrorMessage = "Passwords must be between 12 and 128 characters.")]
        public string? Password { get; set; } // Nullable on Edit, Required on Create

        [Display(Name = "Account is Active (Can Login)")]
        public bool IsActive { get; set; }
    }
}
