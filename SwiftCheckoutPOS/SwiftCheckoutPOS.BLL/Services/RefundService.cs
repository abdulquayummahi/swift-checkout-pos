// Project: SwiftCheckoutPOS.BLL
// File: Services/RefundService.cs
using System;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class RefundService
    {
        private readonly SalesRepository _salesRepo;

        public RefundService(SalesRepository salesRepo)
        {
            _salesRepo = salesRepo;
        }

        public SaleDTO LookupTransaction(string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber))
                throw new ArgumentException("Receipt barcode cannot be empty.");

            var sale = _salesRepo.GetSaleByReceiptNumber(receiptNumber.Trim());

            if (sale == null)
                throw new InvalidOperationException($"Transaction [{receiptNumber}] not found in the historical ledger.");

            return new SaleDTO
            {
                SaleId = sale.SaleId,
                ReceiptNumber = sale.ReceiptNumber,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                PaymentMethod = sale.PaymentMethod,
                OrderStatus = sale.OrderStatus, // Critical for UI rendering
                SaleItems = sale.SaleItems.Select(si => new SaleItemDTO
                {
                    ProductId = si.ProductId,
                    ProductName = si.Product?.ProductName ?? "Unknown Product",
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice,
                    LineTotal = si.LineTotal
                }).ToList()
            };
        }

        public void ExecuteRefund(string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber))
                throw new ArgumentException("Receipt barcode cannot be empty.");

            // Status checks run again inside the database unit; this avoids acting on a stale read.
            switch (_salesRepo.RefundCompletedSale(receiptNumber.Trim()))
            {
                case RefundOutcome.Refunded: return;
                case RefundOutcome.NotFound: throw new InvalidOperationException("Transaction not found.");
                case RefundOutcome.AlreadyRefunded: throw new InvalidOperationException("This transaction has already been refunded.");
                case RefundOutcome.NotRefundable: throw new InvalidOperationException("Only completed sales can be refunded.");
                default: throw new InvalidOperationException("The sale was changed by another action. Look it up again and retry.");
            }
        }
    }
}
