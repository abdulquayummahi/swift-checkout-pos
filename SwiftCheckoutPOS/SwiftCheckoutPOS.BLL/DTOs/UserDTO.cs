// Project: SwiftCheckoutPOS.BLL
// File: DTOs/UserDTO.cs
using System;
using System.Collections.Generic;
using System.Text;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class UserDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
    }
}