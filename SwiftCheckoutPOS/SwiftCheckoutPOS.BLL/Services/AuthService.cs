// Project: SwiftCheckoutPOS.BLL
// File: Services/AuthService.cs
using System;
using SwiftCheckoutPOS.BLL.Security;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepo;

        public AuthService(UserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public bool ValidateCredentials(string username, string plainTextPassword, out int userId, out string? role, out string? fullName)
        {
            userId = 0;
            role = null;
            fullName = null;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(plainTextPassword)) return false;

            var user = _userRepo.GetUserByUsername(username.Trim());

            // Fail fast if user does not exist or has been deactivated (Soft-Deleted)
            if (user == null || !user.IsActive) return false;

            if (!PasswordHasher.Verify(plainTextPassword, user.PasswordSalt, user.PasswordHash, out var needsRehash)) return false;

            if (needsRehash)
            {
                user.PasswordSalt = PasswordHasher.GenerateSalt();
                user.PasswordHash = PasswordHasher.Hash(plainTextPassword, user.PasswordSalt);
                _userRepo.UpdateUser(user);
            }

            userId = user.UserId;
            role = user.Role;
            fullName = user.FullName;

            return true;
        }
    }
}
