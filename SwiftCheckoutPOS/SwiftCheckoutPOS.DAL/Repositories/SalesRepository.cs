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

        /// <summary>
        /// IMM-04: full-sale refund as ONE unit inside the EF execution strategy, so
        /// EnableRetryOnFailure stays on. Each attempt starts from a clean change tracker and
        /// re-reads the sale, so a retry never reapplies stale state. The sale RowVersion makes
        /// a concurrent second refund fail instead of restocking twice.
        /// Business rules (window, approval, ledger reversal) are NOT decided here (BP-04/IMM-07).
        /// </summary>
        public RefundOutcome RefundCompletedSale(string receiptNumber)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                return strategy.Execute(() =>
                {
                    _context.ChangeTracker.Clear();
                    using var transaction = _context.Database.BeginTransaction();
                    var sale = _context.Sales.Include(s => s.SaleItems)
                        .SingleOrDefault(s => s.ReceiptNumber == receiptNumber);
                    if (sale == null) return RefundOutcome.NotFound;
                    if (sale.OrderStatus == "Refunded") return RefundOutcome.AlreadyRefunded;
                    if (sale.OrderStatus != "Completed") return RefundOutcome.NotRefundable;

                    sale.OrderStatus = "Refunded";
                    foreach (var item in sale.SaleItems)
                    {
                        var product = _context.Products.Find(item.ProductId);
                        if (product != null) product.StockQuantity += item.Quantity;
                    }

                    _context.SaveChanges();
                    transaction.Commit();
                    return RefundOutcome.Refunded;
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request changed this sale or its stock first; nothing of this attempt was committed.
                _context.ChangeTracker.Clear();
                var status = _context.Sales.AsNoTracking().Where(s => s.ReceiptNumber == receiptNumber)
                    .Select(s => s.OrderStatus).SingleOrDefault();
                return status == "Refunded" ? RefundOutcome.AlreadyRefunded : RefundOutcome.Conflict;
            }
            finally
            {
                _context.ChangeTracker.Clear();
            }
        }

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