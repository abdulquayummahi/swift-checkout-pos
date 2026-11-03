// Project: SwiftCheckoutPOS.UI
// File: Controllers/ManagerDashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    /// <summary>Operational dashboard (no financial figures). DR-24: Admin inherits Manager pages.</summary>
    [Authorize(Roles = "Admin,Manager")]
    public class ManagerDashboardController(InventoryService inventory, CheckoutService checkout, ShiftService shifts) : Controller
    {
        public IActionResult Index()
        {
            ViewBag.LowStock = inventory.GetAllSystemProducts().Where(p => p.IsActive && p.StockQuantity - p.ReservedQuantity <= p.ReorderLevel)
                .OrderBy(p => p.StockQuantity).Take(30).ToList();
            ViewBag.Held = checkout.GetHeldOrders();
            ViewBag.Shift = shifts.Current();
            return View();
        }
    }
}
