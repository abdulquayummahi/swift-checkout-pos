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

        public void FinalizeSale(Sale sale)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                _context.Sales.Add(sale);
                foreach (var item in sale.SaleItems)
                {
                    var product = _context.Products.Find(item.ProductId);
                    if (product != null) product.StockQuantity -= item.Quantity;
                }
                _context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw new InvalidOperationException("CRITICAL FAULT: Transaction rolled back. Stock and Sale data preserved.");
            }
        }

        public void ParkHeldOrder(Sale heldSale)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                heldSale.OrderStatus = "Held";
                _context.Sales.Add(heldSale);

                foreach (var item in heldSale.SaleItems)
                {
                    var product = _context.Products.Find(item.ProductId);
                    if (product != null) product.StockQuantity -= item.Quantity;
                }

                _context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw new InvalidOperationException("Failed to hold order. Database rolled back.");
            }
        }

        public Sale ResumeHeldOrder(int saleId)
        {
            var sale = _context.Sales
                .Include(s => s.SaleItems)
                .Include(s => s.Customer)
                .FirstOrDefault(s => s.SaleId == saleId && s.OrderStatus == "Held");

            if (sale != null)
            {
                using var transaction = _context.Database.BeginTransaction();
                try
                {
                    foreach (var item in sale.SaleItems)
                    {
                        var product = _context.Products.Find(item.ProductId);
                        if (product != null) product.StockQuantity += item.Quantity;
                    }

                    _context.SaleItems.RemoveRange(sale.SaleItems);
                    _context.Sales.Remove(sale);
                    _context.SaveChanges();
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw new InvalidOperationException("Failed to resume order. Database rolled back.");
                }
            }

            return sale;
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

        public void ProcessRefundTransaction(Sale sale)
        {
            using var transaction = _context.Database.BeginTransaction();
            try
            {
                sale.OrderStatus = "Refunded";
                _context.Sales.Update(sale);

                foreach (var item in sale.SaleItems)
                {
                    var product = _context.Products.Find(item.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        _context.Products.Update(product);
                    }
                }

                _context.SaveChanges();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new InvalidOperationException($"CRITICAL REFUND FAULT: Transaction aborted. Database state secured. Details: {ex.Message}");
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