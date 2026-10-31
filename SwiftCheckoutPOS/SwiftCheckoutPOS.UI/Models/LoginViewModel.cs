// Project: SwiftCheckoutPOS.UI
// File: Models/LoginViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.UI.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
