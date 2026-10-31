// Project: SwiftCheckoutPOS.UI
// File: Controllers/UserAdminController.cs
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Models;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserAdminController : Controller
    {
        private readonly UserAdminService _userService;

        public UserAdminController(UserAdminService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var users = _userService.GetAllUsers();
            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new UserViewModel { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(UserViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError("Password", "A password is required when creating a new user.");

            if (!ModelState.IsValid) return View(model);

            try
            {
                _userService.CreateUser(new UserActionDTO
                {
                    Username = model.Username,
                    FullName = model.FullName,
                    Role = model.Role,
                    PlainTextPassword = model.Password
                });
                TempData["Success"] = $"Employee account for {model.FullName} provisioned successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                var dto = _userService.GetUserForEdit(id);
                var model = new UserViewModel
                {
                    UserId = dto.UserId,
                    Username = dto.Username,
                    FullName = dto.FullName,
                    Role = dto.Role,
                    IsActive = dto.IsActive
                };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(UserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                _userService.UpdateUser(new UserActionDTO
                {
                    UserId = model.UserId,
                    Username = model.Username,
                    FullName = model.FullName,
                    Role = model.Role,
                    IsActive = model.IsActive,
                    PlainTextPassword = model.Password // If empty, BLL ignores it
                });
                TempData["Success"] = "User credentials and permissions updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }
    }
}
