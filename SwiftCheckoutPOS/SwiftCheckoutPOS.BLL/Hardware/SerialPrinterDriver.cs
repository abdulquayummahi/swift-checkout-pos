// Project: SwiftCheckoutPOS.BLL
// File: Hardware/SerialPrinterDriver.cs
using System;
using System.IO.Ports;
using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;

namespace SwiftCheckoutPOS.BLL.Hardware
{
    public class SerialPrinterDriver : IReceiptPrinter
    {
        private readonly string _comPort;
        private readonly int _baudRate;

        public SerialPrinterDriver(string comPort = "COM1", int baudRate = 9600)
        {
            _comPort = comPort;
            _baudRate = baudRate;
        }

        public bool IsPrinterOnline()
        {
            try
            {
                using var port = new SerialPort(_comPort, _baudRate);
                port.Open();
                return port.IsOpen;
            }
            catch
            {
                return false;
            }
        }

        public void OpenCashDrawer()
        {
            // Standard ESC/POS Pulse Command
            byte[] pulse = new byte[] { 27, 112, 0, 25, 250 };
            SendToPrinter(pulse);
        }

        public void PrintReceipt(CheckoutSessionDTO session)
        {
            // Receipt implementation omitted for focus on Z-Report scope.
            // Maintains existing architectural boundaries.
        }

        public string GenerateReceiptPreview(CheckoutSessionDTO session)
        {
            return "Receipt preview rendering active.";
        }

        // --- ARCHITECT NEW: Z-Report Hardware Spooler ---
        public void PrintZReport(ZReportDTO report)
        {
            var sb = new StringBuilder();

            const char ESC = '\x1B';
            const char GS = '\x1D';

            sb.Append(ESC).Append('@'); // Initialize printer

            sb.Append(ESC).Append('a').Append('\x01'); // Center Align
            sb.AppendLine("================================");
            sb.AppendLine("END OF DAY Z-REPORT");
            sb.AppendLine("================================");

            sb.Append(ESC).Append('a').Append('\x00'); // Left Align
            sb.AppendLine($"Report Date: {report.ReportDate:yyyy-MM-dd}");
            sb.AppendLine($"Printed At:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("--------------------------------");

            sb.AppendLine($"Total Transactions:   {report.TotalTransactions}");
            sb.AppendLine($"Gross Revenue:    BDT {report.GrossRevenue:N2}");
            sb.AppendLine($"Net Revenue:      BDT {report.NetRevenue:N2}");
            sb.AppendLine($"Tax Collected:    BDT {report.TotalTaxCollected:N2}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine("TENDER BREAKDOWN:");
            sb.AppendLine($"Cash:             BDT {report.CashTotal:N2}");
            sb.AppendLine($"Card:             BDT {report.CardTotal:N2}");
            sb.AppendLine($"Mobile Wallet:    BDT {report.MobileWalletTotal:N2}");
            sb.AppendLine("================================");

            sb.Append(ESC).Append('a').Append('\x01'); // Center Align
            sb.AppendLine("SHIFT CLOSED SECURELY");
            sb.AppendLine("");
            sb.AppendLine("");

            sb.Append(GS).Append('V').Append('\x41').Append('\x03'); // Feed paper and partial cut

            SendToPrinter(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        private void SendToPrinter(byte[] data)
        {
            using var port = new SerialPort(_comPort, _baudRate);
            port.WriteTimeout = 2000; // Physical hardware timeout set to strict 2000ms

            try
            {
                port.Open();
                port.Write(data, 0, data.Length);
            }
            catch (UnauthorizedAccessException)
            {
                throw new Exception($"HARDWARE LOCK: Port {_comPort} is occupied by another process.");
            }
            catch (TimeoutException)
            {
                throw new Exception("HARDWARE TIMEOUT: Printer failed to respond within 2000ms. Check physical connection or paper jam.");
            }
            catch (Exception ex)
            {
                throw new Exception($"COM FAULT: {ex.Message}");
            }
            finally
            {
                if (port.IsOpen) port.Close();
            }
        }
    }
}