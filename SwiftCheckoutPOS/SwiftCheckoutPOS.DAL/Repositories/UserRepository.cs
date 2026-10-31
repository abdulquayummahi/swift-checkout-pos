// Project: SwiftCheckoutPOS.DAL
// File: Repositories/UserRepository.cs
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class UserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<User> GetAllUsers()
        {
            return _context.Users.AsNoTracking().ToList();
        }

        public User? GetUserById(int userId)
        {
            return _context.Users.Find(userId);
        }

        public User? GetUserByUsername(string username)
        {
            return _context.Users.FirstOrDefault(u => u.Username.ToLower() == username.ToLower());
        }

        public void AddUser(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }

        public void UpdateUser(User user)
        {
            _context.Users.Update(user);
            _context.SaveChanges();
        }
    }
}