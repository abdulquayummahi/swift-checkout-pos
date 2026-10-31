// Project: SwiftCheckoutPOS.BLL
// File: Services/ComPortPrinterService.cs
using System;
using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class ComPortPrinterService : IReceiptPrinter
    {
        private readonly string _comPort = "COM3"; // Standard default for USB/Serial POS printers

        public bool IsPrinterOnline()
        {
            throw new NotImplementedException("Hardware not yet connected.");
        }

        public void OpenCashDrawer()
        {
            throw new NotImplementedException("Hardware not yet connected.");
        }

        public void PrintReceipt(CheckoutSessionDTO session)
        {
            throw new NotImplementedException("Hardware not yet connected.");
        }

        public string GenerateReceiptPreview(CheckoutSessionDTO session)
        {
            return BuildReceiptString(session);
        }

        // ARCHITECT FIX: Satisfy the IReceiptPrinter contract
        public void PrintZReport(ZReportDTO report)
        {
            throw new NotImplementedException("Hardware not yet connected.");
        }

        // Strict 48-Character 80mm ESC/POS Layout Engine
        private string BuildReceiptString(CheckoutSessionDTO session)
        {
            int paperWidth = 48;
            StringBuilder receipt = new StringBuilder();

            receipt.AppendLine(CenterText("SWIFT SUPERMARKET", paperWidth));
            receipt.AppendLine(CenterText("Dhaka, Bangladesh", paperWidth));
            receipt.AppendLine(CenterText("BIN: 000000000-0000", paperWidth));
            receipt.AppendLine(CenterText("Tel: +880-1234-567890", paperWidth));
            receipt.AppendLine(new string('-', paperWidth));

            receipt.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            receipt.AppendLine($"Receipt #: {session.InvoiceNumber}");
            receipt.AppendLine($"Cashier: Terminal 01");
            if (session.CustomerType == "Loyalty" && !string.IsNullOrEmpty(session.CustomerName))
            {
                receipt.AppendLine($"Customer: {session.CustomerName}");
            }
            receipt.AppendLine(new string('-', paperWidth));

            receipt.AppendLine(PadColumns("ITEM", "QTYxPRICE", "TOTAL", paperWidth));
            receipt.AppendLine(new string('-', paperWidth));

            foreach (var item in session.Items)
            {
                string name = item.ProductName.Length > 22 ? item.ProductName.Substring(0, 22) : item.ProductName;
                string qtyPrice = $"{item.Quantity}x{item.UnitPrice:N0}";
                string lineTotal = item.LineTotal.ToString("N0");

                receipt.AppendLine(PadColumns(name, qtyPrice, lineTotal, paperWidth));
            }

            receipt.AppendLine(new string('-', paperWidth));

            receipt.AppendLine(PadColumns("SUBTOTAL:", "", $"{session.GrossSubTotal:N0}", paperWidth));

            if (session.CalculatedDiscount > 0)
            {
                receipt.AppendLine(PadColumns("DISCOUNT:", "", $"-{session.CalculatedDiscount:N0}", paperWidth));
            }

            receipt.AppendLine(PadColumns("TOTAL DUE:", "", $"BDT {session.TotalAmount:N0}", paperWidth));
            receipt.AppendLine(new string('=', paperWidth));

            receipt.AppendLine(CenterText("Thank you for your purchase!", paperWidth));
            receipt.AppendLine(CenterText("Please come again.", paperWidth));
            receipt.AppendLine("\n\n\n");

            return receipt.ToString();
        }

        private string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return new string(' ', width);
            if (text.Length >= width) return text.Substring(0, width);
            int padding = (width - text.Length) / 2;
            return text.PadLeft(text.Length + padding).PadRight(width);
        }

        private string PadColumns(string left, string center, string right, int width)
        {
            int leftWidth = 22;
            int centerWidth = 12;
            int rightWidth = 14;

            string l = left.Length > leftWidth ? left.Substring(0, leftWidth) : left.PadRight(leftWidth);
            string c = center.Length > centerWidth ? center.Substring(0, centerWidth) : center.PadLeft(centerWidth);
            string r = right.Length > rightWidth ? right.Substring(0, rightWidth) : right.PadLeft(rightWidth);

            return l + c + r;
        }
    }
}