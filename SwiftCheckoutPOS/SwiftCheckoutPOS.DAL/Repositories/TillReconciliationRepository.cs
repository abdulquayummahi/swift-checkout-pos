// Project: SwiftCheckoutPOS.DAL
// File: Repositories/TillReconciliationRepository.cs
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class TillReconciliationRepository
    {
        private readonly ApplicationDbContext _context;

        public TillReconciliationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public void AddReconciliation(TillReconciliation reconciliation)
        {
            _context.TillReconciliations.Add(reconciliation);
            _context.SaveChanges();
        }

        public TillReconciliation? GetLatestReconciliation()
        {
            // ARCHITECT FIX: Aligned with your entity's 'CloseTime' property
            return _context.TillReconciliations
                .OrderByDescending(t => t.CloseTime)
                .FirstOrDefault();
        }

        public IEnumerable<TillReconciliation> GetReconciliationHistory(int limit = 30)
        {
            // ARCHITECT FIX: Removed .Include(t => t.User) to match your flat entity structure
            return _context.TillReconciliations
                .OrderByDescending(t => t.CloseTime)
                .Take(limit)
                .AsNoTracking()
                .ToList();
        }
    }
}