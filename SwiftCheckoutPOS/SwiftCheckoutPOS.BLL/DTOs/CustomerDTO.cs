// Project: SwiftCheckoutPOS.BLL
// File: DTOs/CustomerDTO.cs
namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class CustomerDTO
    {
        public int CustomerId { get; set; }
        public string PhoneNumber { get; set; }
        public string FullName { get; set; }
        public int LoyaltyPoints { get; set; }
    }
}