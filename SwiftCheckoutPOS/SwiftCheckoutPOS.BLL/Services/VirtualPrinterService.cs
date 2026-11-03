// Project: SwiftCheckoutPOS.BLL
// File: Services/VirtualPrinterService.cs
using System;
using System.IO;
using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;

namespace SwiftCheckoutPOS.BLL.Services
{
    /// <summary>Safe default "printer": writes receipts to text files and is clearly labelled Virtual (PR-Q60).</summary>
    public class VirtualPrinterService : IReceiptPrinter
    {
        private readonly string _outputDirectory;
        private readonly Func<SwiftCheckoutPOS.BLL.Hardware.ReceiptHeaderDTO> _header;

        public VirtualPrinterService() : this(null, null) { }

        public VirtualPrinterService(Func<SwiftCheckoutPOS.BLL.Hardware.ReceiptHeaderDTO>? header, string? outputDirectory)
        {
            _header = header ?? (() => SwiftCheckoutPOS.BLL.Hardware.ReceiptHeaderDTO.Default);
            _outputDirectory = outputDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VirtualReceipts");
        }

        public bool IsPrinterOnline() => true;

        public void OpenCashDrawer() => LogVirtualHardwareEvent("VIRTUAL cash drawer kick [ESC p 0 25 250]");

        public void PrintReceipt(CheckoutSessionDTO session)
        {
            Directory.CreateDirectory(_outputDirectory);
            var fileName = $"Receipt_{session.ReceiptNumber ?? session.InvoiceNumber}_{DateTime.Now:HHmmss}.txt";
            File.WriteAllText(Path.Combine(_outputDirectory, fileName), "[VIRTUAL PRINTER]\n" + SwiftCheckoutPOS.BLL.Hardware.ReceiptFormatter.Receipt(session, _header()));
            LogVirtualHardwareEvent($"VIRTUAL print job: {fileName}");
        }

        public string GenerateReceiptPreview(CheckoutSessionDTO session) => SwiftCheckoutPOS.BLL.Hardware.ReceiptFormatter.Receipt(session, _header());

        public void PrintZReport(ZReportDTO report)
        {
            Directory.CreateDirectory(_outputDirectory);
            var fileName = $"ZReport_{report.ReportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.txt";
            File.WriteAllText(Path.Combine(_outputDirectory, fileName), SwiftCheckoutPOS.BLL.Hardware.ReceiptFormatter.ZReport(report, _header()));
            LogVirtualHardwareEvent($"VIRTUAL Z-report: {fileName}");
        }

        private void LogVirtualHardwareEvent(string message)
        {
            Directory.CreateDirectory(_outputDirectory);
            File.AppendAllText(Path.Combine(_outputDirectory, "HardwareEventLog.txt"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
    }
}
