// Project: SwiftCheckoutPOS.UI
// File: Models/CheckoutViewModel.cs
using System.Collections.Generic;
using SwiftCheckoutPOS.BLL.DTOs;

namespace SwiftCheckoutPOS.UI.Models
{
    public class CheckoutViewModel
    {
        // Encapsulates the active shopping cart
        public CheckoutSessionDTO Cart { get; set; } = new CheckoutSessionDTO();

        // ARCHITECT FIX: Strictly typed to ProductDTO to respect the BLL boundary
        public IEnumerable<ProductDTO> AvailableProducts { get; set; } = new List<ProductDTO>();

        public List<HeldOrderDTO> HeldOrders { get; set; } = new();
    }
}
