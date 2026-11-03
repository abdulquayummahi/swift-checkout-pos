// Project: SwiftCheckoutPOS.DAL
// File: Repositories/SalesRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class SalesRepository
    {
        private readonly ApplicationDbContext _context;

        public SalesRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Sale> GetSalesByDate(DateTime date)
        {
            return _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .AsNoTracking()
                .AsSplitQuery() // ARCHITECT FIX: Prevent Cartesian Explosion
                .Where(s => s.SaleDate.Date == date.Date)
                .OrderByDescending(s => s.SaleDate)
                .ToList();
        }

        public IEnumerable<Sale> GetSalesByDateRange(DateTime startDate, DateTime endDate)
        {
            return _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .AsNoTracking()
                .AsSplitQuery()
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
                .ToList();
        }

        // ARCHITECT FIX: Added DateTime cutoff filter to utilize the new SQL Index
        public IEnumerable<Sale> GetHeldOrders(DateTime? sinceDate = null)
        {
            var query = _context.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.Customer)
                .AsNoTracking()
                .AsSplitQuery()
                .Where(s => s.OrderStatus == "Held");

            if (sinceDate.HasValue)
            {
                query = query.Where(s => s.SaleDate >= sinceDate.Value);
            }

            return query.OrderByDescending(s => s.SaleDate).ToList();
        }

        public IEnumerable<Sale> GetCashSalesSince(DateTime sinceTime)
        {
            return _context.Sales
                .Where(s => s.SaleDate >= sinceTime && s.PaymentMethod == "Cash")
                .AsNoTracking()
                .ToList();
        }

        public System.Linq.IQueryable<Sale> GetAllSales()
        {
            return _context.Sales.AsQueryable();
        }

        public Sale GetSaleByReceiptNumber(string receiptNumber)
        {
            return _context.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.Customer)
                .FirstOrDefault(s => s.ReceiptNumber == receiptNumber);
        }

        /// <summary>Tracked sale with everything a refund/void needs. Use inside an AtomicUnit.</summary>
        public Sale? LoadForRefund(string receiptNumber) => _context.Sales
            .Include(s => s.SaleItems).ThenInclude(i => i.Product)
            .Include(s => s.Payments)
            .Include(s => s.Refunds).ThenInclude(r => r.Items)
            .Include(s => s.Refunds).ThenInclude(r => r.Payouts)
            .Include(s => s.Customer)
            .AsSplitQuery()
            .SingleOrDefault(s => s.ReceiptNumber == receiptNumber);

        public Product? FindProduct(int productId) => _context.Products.Find(productId);
        public void AddRefund(Refund refund) => _context.Refunds.Add(refund);
        public int? OpenShiftId() => _context.Shifts.Where(s => s.Status == "Open").Select(s => (int?)s.ShiftId).SingleOrDefault();
        public void AddLedger(LedgerEntry entry) => _context.LedgerEntries.Add(entry);

        public IEnumerable<Sale> GetSalesByCustomerId(int customerId)
        {
            return _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .AsNoTracking()
                .AsSplitQuery()
                .Where(s => s.CustomerId == customerId)
                .OrderByDescending(s => s.SaleDate)
                .ToList();
        }

        public IEnumerable<Sale> GetAllLoyaltySalesWithProducts()
        {
            return _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .AsNoTracking()
                .AsSplitQuery()
                .Where(s => s.CustomerId != null)
                .ToList();
        }
    }
}