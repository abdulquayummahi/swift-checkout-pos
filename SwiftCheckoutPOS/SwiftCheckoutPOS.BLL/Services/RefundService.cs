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
            var sale = _salesRepo.GetSaleByReceiptNumber(receiptNumber.Trim());

            if (sale == null)
                throw new InvalidOperationException("Transaction not found.");

            if (sale.OrderStatus == "Refunded")
                throw new InvalidOperationException("SECURITY FAULT: This transaction has already been refunded.");

            if (sale.OrderStatus == "Held")
                throw new InvalidOperationException("Held orders cannot be refunded. Resume and clear the order instead.");

            // Delegate to the DAL to execute the atomic database transaction
            _salesRepo.ProcessRefundTransaction(sale);
        }
    }
}