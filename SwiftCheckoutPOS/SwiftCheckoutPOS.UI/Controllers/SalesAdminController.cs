// Project: SwiftCheckoutPOS.UI
// File: Controllers/SalesAdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SalesAdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}