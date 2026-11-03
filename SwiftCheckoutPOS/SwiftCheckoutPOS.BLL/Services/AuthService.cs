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

        /// <summary>IMM-09: current role/status/stamp, checked on every request against the login cookie.</summary>
        public (bool Active, string Role, string Stamp)? SecurityState(int userId)
        {
            var user = _userRepo.GetUserById(userId);
            return user is null ? null : (user.IsActive, user.Role, user.SecurityStamp);
        }

        /// <summary>BP-13/PR-Q52: true until the first user exists (one-time setup).</summary>
        public bool NeedsFirstAdmin() => !_userRepo.GetAllUsers().Any();

        /// <summary>BP-13: create the first Admin only while no user exists. No default password anywhere.</summary>
        public void CreateFirstAdmin(string username, string fullName, string password)
        {
            if (!NeedsFirstAdmin()) throw new InvalidOperationException("Setup is already complete.");
            ValidateNewPassword(password);
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 50) throw new ArgumentException("Enter a username (max 50).");
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 100) throw new ArgumentException("Enter the full name.");
            var salt = PasswordHasher.GenerateSalt();
            _userRepo.AddUser(new SwiftCheckoutPOS.DAL.Entities.User
            {
                Username = username.Trim(), FullName = fullName.Trim(), PasswordSalt = salt,
                PasswordHash = PasswordHasher.Hash(password, salt), Role = "Admin", IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            });
        }

        /// <summary>PR-Q68: owner-only local maintenance (run on the shop PC): reset or create an Admin.</summary>
        public void EmergencyResetAdmin(string username, string password)
        {
            ValidateNewPassword(password);
            var user = _userRepo.GetUserByUsername(username.Trim());
            var salt = PasswordHasher.GenerateSalt();
            if (user is null)
            {
                _userRepo.AddUser(new SwiftCheckoutPOS.DAL.Entities.User { Username = username.Trim(), FullName = username.Trim(),
                    PasswordSalt = salt, PasswordHash = PasswordHasher.Hash(password, salt), Role = "Admin", IsActive = true,
                    SecurityStamp = Guid.NewGuid().ToString("N") });
                return;
            }
            user.PasswordSalt = salt; user.PasswordHash = PasswordHasher.Hash(password, salt);
            user.Role = "Admin"; user.IsActive = true; user.SecurityStamp = Guid.NewGuid().ToString("N");
            _userRepo.UpdateUser(user);
        }

        public static void ValidateNewPassword(string password)
        {
            PasswordHasher.EnsureMeetsPolicy(password); // same 12–128 character rule as user administration
        }

        public bool ValidateCredentials(string username, string plainTextPassword, out int userId, out string? role, out string? fullName) =>
            ValidateCredentials(username, plainTextPassword, out userId, out role, out fullName, out _);

        public bool ValidateCredentials(string username, string plainTextPassword, out int userId, out string? role, out string? fullName, out string? securityStamp)
        {
            userId = 0;
            role = null;
            fullName = null;
            securityStamp = null;

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
            securityStamp = user.SecurityStamp;

            return true;
        }
    }
}
