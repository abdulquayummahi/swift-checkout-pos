// Project: SwiftCheckoutPOS.BLL
// File: Services/VirtualPrinterService.cs
using System;
using System.IO;
using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class VirtualPrinterService : IReceiptPrinter
    {
        private readonly string _outputDirectory;

        public VirtualPrinterService()
        {
            _outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VirtualReceipts");
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        public bool IsPrinterOnline() => true;

        public void OpenCashDrawer()
        {
            LogVirtualHardwareEvent("CASH DRAWER KICK SIGNAL SENT [ESC p 0 25 250]");
        }

        public void PrintReceipt(CheckoutSessionDTO session)
        {
            string receiptText = BuildReceiptString(session);

            string fileName = $"Receipt_{session.InvoiceNumber}_{DateTime.Now:HHmmss}.txt";
            File.WriteAllText(Path.Combine(_outputDirectory, fileName), receiptText);

            LogVirtualHardwareEvent($"PRINT JOB COMPLETED: {fileName}");
        }

        public string GenerateReceiptPreview(CheckoutSessionDTO session)
        {
            return BuildReceiptString(session);
        }

        // ARCHITECT FIX: Satisfy the IReceiptPrinter contract for Shift Closures
        public void PrintZReport(ZReportDTO report)
        {
            int paperWidth = 48;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(CenterText("SWIFT SUPERMARKET", paperWidth));
            sb.AppendLine(CenterText("END OF DAY Z-REPORT", paperWidth));
            sb.AppendLine(new string('=', paperWidth));

            sb.AppendLine($"Report Date: {report.ReportDate:yyyy-MM-dd}");
            sb.AppendLine($"Printed At:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('-', paperWidth));

            sb.AppendLine($"Total Transactions:   {report.TotalTransactions}");
            sb.AppendLine($"Gross Revenue:    BDT {report.GrossRevenue:N2}");
            sb.AppendLine($"Net Revenue:      BDT {report.NetRevenue:N2}");
            sb.AppendLine($"Tax Collected:    BDT {report.TotalTaxCollected:N2}");
            sb.AppendLine(new string('-', paperWidth));

            sb.AppendLine("TENDER BREAKDOWN:");
            sb.AppendLine($"Cash:             BDT {report.CashTotal:N2}");
            sb.AppendLine($"Card:             BDT {report.CardTotal:N2}");
            sb.AppendLine($"Mobile Wallet:    BDT {report.MobileWalletTotal:N2}");
            sb.AppendLine(new string('=', paperWidth));

            sb.AppendLine(CenterText("SHIFT CLOSED SECURELY", paperWidth));
            sb.AppendLine("\n\n\n"); // Feed paper to cutter margin

            string fileName = $"ZReport_{report.ReportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.txt";
            File.WriteAllText(Path.Combine(_outputDirectory, fileName), sb.ToString());

            LogVirtualHardwareEvent($"Z-REPORT JOB COMPLETED: {fileName}");
        }

        // Strict 48-Character 80mm ESC/POS Layout Engine
        private string BuildReceiptString(CheckoutSessionDTO session)
        {
            int paperWidth = 48;
            StringBuilder receipt = new StringBuilder();

            receipt.AppendLine(CenterText("SWIFT SUPERMARKET", paperWidth));
            receipt.AppendLine(CenterText("Dhaka, Bangladesh", paperWidth));
            receipt.AppendLine(CenterText("BIN: 000000000-0000", paperWidth)); // BD Mushak compliance
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
                // Strict Truncation to 22 characters to protect column alignment
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
            receipt.AppendLine("\n\n\n"); // Feed paper to cutter margin

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
            // Distribution matching 48 character paper width
            int leftWidth = 22;
            int centerWidth = 12;
            int rightWidth = 14;

            string l = left.Length > leftWidth ? left.Substring(0, leftWidth) : left.PadRight(leftWidth);
            string c = center.Length > centerWidth ? center.Substring(0, centerWidth) : center.PadLeft(centerWidth);
            string r = right.Length > rightWidth ? right.Substring(0, rightWidth) : right.PadLeft(rightWidth);

            return l + c + r;
        }

        private void LogVirtualHardwareEvent(string message)
        {
            string logPath = Path.Combine(_outputDirectory, "HardwareEventLog.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
    }
}