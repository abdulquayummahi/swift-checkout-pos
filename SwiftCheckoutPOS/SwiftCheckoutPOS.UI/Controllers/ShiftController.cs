using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Interfaces;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.UI.Controllers;

/// <summary>BP-19: open/close the till shift (any signed-in staff), cash in/out, Manager/Admin "No sale".</summary>
[Authorize]
public sealed class ShiftController(ShiftService shifts, Func<IReceiptPrinter> printerFactory, ILogger<ShiftController> logger) : Controller
{
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? "Cashier";

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.CanSeeFigures = ShiftService.CanSeeFigures(Role);
        ViewBag.Recent = ShiftService.CanSeeFigures(Role) ? shifts.Recent() : Array.Empty<ShiftSummaryDTO>();
        return View(shifts.Current());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Open(decimal openingFloat) => Do(() =>
    {
        shifts.Open(openingFloat, User.Actor());
        TempData["Success"] = "Shift opened. You can take payments now.";
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Cash(string type, decimal amount, string reason) => Do(() =>
    {
        shifts.RecordCash(type, amount, reason, User.Actor());
        TempData["Success"] = (type == "PaidIn" ? "Cash paid in" : "Cash paid out") + " recorded.";
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult NoSale(string reason) => Do(() =>
    {
        shifts.NoSale(reason, User.Actor(), Role);
        try { printerFactory().OpenCashDrawer(); TempData["Success"] = "Drawer opened (No sale) and logged."; }
        catch (Exception ex)
        {
            logger.LogWarning("Drawer open failed ({ErrorType}).", ex.GetType().Name);
            TempData["Warning"] = "Logged, but the drawer did not respond.";
        }
    });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Close(decimal countedCash, string? notes, string? approverUsername, string? approverPassword) => Do(() =>
    {
        var closed = shifts.Close(countedCash, notes, User.Actor(), Role, approverUsername, approverPassword);
        TempData["Success"] = ShiftService.CanSeeFigures(Role)
            ? $"Shift closed. Expected {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(closed.ExpectedCash)}, counted {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(closed.CountedCash ?? 0)}, difference {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(closed.Difference ?? 0)}."
            : "Shift closed. Your count has been recorded."; // CQ-12: blind for cashiers
    });

    private IActionResult Do(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
