// Project: SwiftCheckoutPOS.BLL
// File: Interfaces/IReceiptPrinter.cs
using SwiftCheckoutPOS.BLL.DTOs;

namespace SwiftCheckoutPOS.BLL.Interfaces
{
    public interface IReceiptPrinter
    {
        bool IsPrinterOnline();
        void OpenCashDrawer();
        void PrintReceipt(CheckoutSessionDTO session);
        string GenerateReceiptPreview(CheckoutSessionDTO session);

        // ARCHITECT NEW: Shift closure hardware contract
        void PrintZReport(ZReportDTO report);
    }
}