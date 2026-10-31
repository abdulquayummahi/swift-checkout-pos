// Project: SwiftCheckoutPOS.BLL
// File: Hardware/EscPosFormatter.cs
using System;
using System.Collections.Generic;
using System.Text;

namespace SwiftCheckoutPOS.BLL.Hardware
{
    public static class EscPosFormatter
    {
        // Low-level ESC/POS Hex Commands
        private static readonly byte[] INIT = { 0x1B, 0x40 };
        private static readonly byte[] ALIGN_CENTER = { 0x1B, 0x61, 1 };
        private static readonly byte[] ALIGN_LEFT = { 0x1B, 0x61, 0 };
        private static readonly byte[] BOLD_ON = { 0x1B, 0x45, 1 };
        private static readonly byte[] BOLD_OFF = { 0x1B, 0x45, 0 };
        private static readonly byte[] CUT_PAPER = { 0x1D, 0x56, 0x41, 0x10 };

        // 0x1B 0x70 [Pin] [OnTime] [OffTime] - Kicks the primary drawer
        private static readonly byte[] OPEN_CASH_DRAWER = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

        public static byte[] GenerateReceipt(string receiptNumber, decimal total, string cashierName)
        {
            List<byte> buffer = new List<byte>();

            // 1. Initialize Printer and Kick Drawer
            buffer.AddRange(INIT);
            buffer.AddRange(OPEN_CASH_DRAWER);

            // 2. Print Header
            buffer.AddRange(ALIGN_CENTER);
            buffer.AddRange(BOLD_ON);
            buffer.AddRange(Encoding.ASCII.GetBytes("SWIFT CHECKOUT POS\n"));
            buffer.AddRange(BOLD_OFF);
            buffer.AddRange(Encoding.ASCII.GetBytes("123 Enterprise Way, Local City\n"));
            buffer.AddRange(Encoding.ASCII.GetBytes($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}\n"));
            buffer.AddRange(Encoding.ASCII.GetBytes($"Receipt: {receiptNumber} | Cashier: {cashierName}\n"));
            buffer.AddRange(Encoding.ASCII.GetBytes("--------------------------------\n"));

            // 3. Print Totals
            buffer.AddRange(ALIGN_CENTER);
            buffer.AddRange(BOLD_ON);
            buffer.AddRange(Encoding.ASCII.GetBytes($"\nTOTAL: {total:C}\n"));
            buffer.AddRange(BOLD_OFF);

            // 4. Print Footer and Cut
            buffer.AddRange(ALIGN_CENTER);
            buffer.AddRange(Encoding.ASCII.GetBytes("\nThank you for shopping with us!\n\n\n\n"));
            buffer.AddRange(CUT_PAPER);

            return buffer.ToArray();
        }
    }
}