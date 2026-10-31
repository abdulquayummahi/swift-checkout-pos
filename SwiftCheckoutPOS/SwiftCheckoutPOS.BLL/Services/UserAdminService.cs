// Project: SwiftCheckoutPOS.BLL
// File: Services/UserAdminService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Security;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class UserAdminService
    {
        private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal) { "Admin", "Manager", "Cashier" };
        private readonly UserRepository _userRepo;

        public UserAdminService(UserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public IEnumerable<UserListDTO> GetAllUsers()
        {
            return _userRepo.GetAllUsers().Select(u => new UserListDTO
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive
            }).OrderBy(u => u.Role).ThenBy(u => u.FullName);
        }

        public UserActionDTO GetUserForEdit(int id)
        {
            var user = _userRepo.GetUserById(id);
            if (user == null) throw new Exception("User account not found in the database.");

            return new UserActionDTO
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive
            };
        }

        public void CreateUser(UserActionDTO dto)
        {
            NormalizeAndValidate(dto, requirePassword: true);
            if (_userRepo.GetUserByUsername(dto.Username) != null)
                throw new InvalidOperationException($"The username '{dto.Username}' is already taken.");

            string salt = PasswordHasher.GenerateSalt();
            string hash = PasswordHasher.Hash(dto.PlainTextPassword!, salt);

            var user = new User
            {
                Username = dto.Username,
                FullName = dto.FullName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = dto.Role,
                IsActive = true
            };

            _userRepo.AddUser(user);
        }

        public void UpdateUser(UserActionDTO dto)
        {
            NormalizeAndValidate(dto, requirePassword: false);
            var user = _userRepo.GetUserById(dto.UserId);
            if (user == null) throw new Exception("User not found.");

            // Prevent renaming to an existing username
            if (user.Username.ToLower() != dto.Username.ToLower())
            {
                if (_userRepo.GetUserByUsername(dto.Username) != null)
                    throw new InvalidOperationException($"The username '{dto.Username}' is already taken.");
            }

            // Prevent disabling the master admin
            if (user.Role == "Admin" && user.IsActive && (!dto.IsActive || !string.Equals(dto.Role, "Admin", StringComparison.Ordinal)))
            {
                var activeAdmins = _userRepo.GetAllUsers().Count(u => u.Role == "Admin" && u.IsActive);
                if (activeAdmins <= 1) throw new InvalidOperationException("You cannot disable the last active System Administrator.");
            }

            user.Username = dto.Username;
            user.FullName = dto.FullName;
            user.Role = dto.Role;
            user.IsActive = dto.IsActive;

            // Only update password if a new one was provided
            if (!string.IsNullOrWhiteSpace(dto.PlainTextPassword))
            {
                user.PasswordSalt = PasswordHasher.GenerateSalt();
                user.PasswordHash = PasswordHasher.Hash(dto.PlainTextPassword, user.PasswordSalt);
            }

            _userRepo.UpdateUser(user);
        }

        private static void NormalizeAndValidate(UserActionDTO dto, bool requirePassword)
        {
            dto.Username = dto.Username?.Trim() ?? string.Empty;
            dto.FullName = dto.FullName?.Trim() ?? string.Empty;
            dto.Role = dto.Role?.Trim() ?? string.Empty;

            if (dto.Username.Length is < 3 or > 50 || !dto.Username.All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                throw new ArgumentException("Usernames must be 3-50 characters and use only letters, numbers, or underscores.");
            }

            if (dto.FullName.Length is < 2 or > 100)
            {
                throw new ArgumentException("Employee names must be between 2 and 100 characters.");
            }

            if (!AllowedRoles.Contains(dto.Role))
            {
                throw new ArgumentException("The selected role is not valid.");
            }

            if (requirePassword && string.IsNullOrWhiteSpace(dto.PlainTextPassword))
            {
                throw new ArgumentException("A password is required for new accounts.");
            }

            if (!string.IsNullOrWhiteSpace(dto.PlainTextPassword))
            {
                PasswordHasher.EnsureMeetsPolicy(dto.PlainTextPassword);
            }
        }
    }
}
