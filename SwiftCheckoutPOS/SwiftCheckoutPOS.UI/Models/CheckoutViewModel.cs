// Project: SwiftCheckoutPOS.UI
// File: Models/CheckoutViewModel.cs
using System.Collections.Generic;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.UI.Models
{
    public class CheckoutViewModel
    {
        // Encapsulates the active shopping cart
        public CheckoutSessionDTO Cart { get; set; } = new CheckoutSessionDTO();

        // ARCHITECT FIX: Strictly typed to ProductDTO to respect the BLL boundary
        public IEnumerable<ProductDTO> AvailableProducts { get; set; } = new List<ProductDTO>();

        // Binds to the DAL Sale entity specifically to power the Held Orders modal UI
        public List<Sale> HeldOrders { get; set; } = new List<Sale>();
    }
}