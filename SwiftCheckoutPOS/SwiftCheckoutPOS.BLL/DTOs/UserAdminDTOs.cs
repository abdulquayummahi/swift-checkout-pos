// Project: SwiftCheckoutPOS.BLL
// File: DTOs/UserAdminDTOs.cs
namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class UserListDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    public class UserActionDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? PlainTextPassword { get; set; } // Nullable for edits where password isn't changed
        public bool IsActive { get; set; }
    }
}