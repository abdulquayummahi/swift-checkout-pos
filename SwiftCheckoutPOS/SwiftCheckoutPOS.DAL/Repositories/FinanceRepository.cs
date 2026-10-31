// Project: SwiftCheckoutPOS.DAL
// File: Repositories/FinanceRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class FinanceRepository
    {
        private readonly ApplicationDbContext _context;

        public FinanceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<LedgerEntry> GetLedgerByDateRange(DateTime startDate, DateTime endDate)
        {
            return _context.LedgerEntries
                .AsNoTracking()
                .Where(e => e.TransactionDate >= startDate && e.TransactionDate <= endDate)
                .OrderByDescending(e => e.TransactionDate)
                .ToList();
        }

        public void AddEntry(LedgerEntry entry)
        {
            _context.LedgerEntries.Add(entry);
            _context.SaveChanges();
        }
    }
}