using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;

namespace SwiftCheckoutPOS.BLL.Hardware;

/// <summary>Where raw ESC/POS bytes go: a (USB virtual) COM port, or a Windows printer queue.</summary>
public interface IRawTransport
{
    bool IsAvailable();
    void Send(byte[] data);
}

/// <summary>USB-serial / COM port printer (PR-Q60: "USB virtual COM port").</summary>
public sealed class SerialTransport(string port, int baudRate) : IRawTransport
{
    public bool IsAvailable() => SerialPort.GetPortNames().Contains(port, StringComparer.OrdinalIgnoreCase);
    public void Send(byte[] data)
    {
        using var serial = new SerialPort(port, baudRate) { WriteTimeout = 3000 };
        serial.Open();
        serial.Write(data, 0, data.Length);
    }
}

/// <summary>USB printer installed in Windows (e.g. "POS-80"): raw bytes through the spooler.</summary>
public sealed class WindowsPrinterTransport(string printerName) : IRawTransport
{
    public bool IsAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero)) return false;
        ClosePrinter(handle);
        return true;
    }

    public void Send(byte[] data)
    {
        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero)) throw new IOException("Printer not found.");
        try
        {
            var doc = new DocInfo { DocName = "SwiftCheckoutPOS receipt", DataType = "RAW" };
            if (!StartDocPrinter(handle, 1, doc)) throw new IOException("Printer refused the job.");
            try
            {
                StartPagePrinter(handle);
                var ptr = Marshal.AllocCoTaskMem(data.Length);
                try
                {
                    Marshal.Copy(data, 0, ptr, data.Length);
                    if (!WritePrinter(handle, ptr, data.Length, out var written) || written != data.Length) throw new IOException("Printing failed.");
                }
                finally { Marshal.FreeCoTaskMem(ptr); }
                EndPagePrinter(handle);
            }
            finally { EndDocPrinter(handle); }
        }
        finally { ClosePrinter(handle); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocName = "";
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType = "RAW";
    }
    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool ClosePrinter(IntPtr handle);
    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(IntPtr handle, int level, [In] DocInfo doc);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr handle);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr handle, IntPtr bytes, int count, out int written);
}

/// <summary>
/// BP-08: 80 mm ESC/POS thermal printer (PR-Q60) with the cash drawer on the printer's RJ11 port (PR-Q76).
/// Printing happens only after the sale is saved; a printer error never changes the sale (NFR-07).
/// </summary>
public sealed class EscPosPrinter(IRawTransport transport, Func<ReceiptHeaderDTO> header) : IReceiptPrinter
{
    private static readonly byte[] Init = [0x1B, 0x40];
    private static readonly byte[] Cut = [0x1D, 0x56, 0x41, 0x10];
    private static readonly byte[] DrawerKick = [0x1B, 0x70, 0x00, 0x19, 0xFA]; // ESC p 0 25 250

    public bool IsPrinterOnline() => transport.IsAvailable();
    public void OpenCashDrawer() => transport.Send(DrawerKick);
    public void PrintReceipt(CheckoutSessionDTO session) => Print(ReceiptFormatter.Receipt(session, header()));
    public string GenerateReceiptPreview(CheckoutSessionDTO session) => ReceiptFormatter.Receipt(session, header());
    public void PrintZReport(ZReportDTO report) => Print(ReceiptFormatter.ZReport(report, header()));

    private void Print(string text)
    {
        // Thermal printers use a single-byte code page; keep to ASCII ("BDT", not the taka sign).
        var body = Encoding.ASCII.GetBytes(text.Replace("৳", "BDT").Replace("—", "-").Replace("\r\n", "\n"));
        transport.Send([.. Init, .. body, 0x0A, 0x0A, 0x0A, .. Cut]);
    }
}
