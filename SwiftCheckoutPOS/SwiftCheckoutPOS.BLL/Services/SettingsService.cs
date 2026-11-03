// Project: SwiftCheckoutPOS.BLL
// File: Services/SettingsService.cs
using System;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class SettingsService
    {
        private readonly SettingsRepository _settingsRepo;
        private readonly AtomicUnit? _unit;
        public const string ManagerFinancialViewKey = "AllowManagerFinancialView";

        public SettingsService(SettingsRepository settingsRepo, AtomicUnit? unit = null)
        {
            _settingsRepo = settingsRepo;
            _unit = unit;
        }

        /// <summary>PR-Q92: Admin setting "Allow Managers to view financial reports" (default OFF).</summary>
        public bool GetAllowManagerFinancialView() =>
            string.Equals(_settingsRepo.GetSettingValue(ManagerFinancialViewKey, "false"), "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>Only Admin may change it; every change is logged (who, when, old/new).</summary>
        public void SetAllowManagerFinancialView(bool allow, string actor, string role)
        {
            if (role != "Admin") throw new InvalidOperationException("Only an Admin can change financial privacy.");
            var old = GetAllowManagerFinancialView();
            if (old == allow) return;
            void Save()
            {
                _settingsRepo.UpdateSetting(ManagerFinancialViewKey, allow ? "true" : "false");
                _unit?.Audit(actor, "SettingChanged", "Setting", ManagerFinancialViewKey, details: $"{old} -> {allow}");
            }
            if (_unit is null) Save(); else _unit.Run(Save);
        }

        /// <summary>CQ-03/PR-Q92: Admin always; Manager only while the Admin setting is ON.</summary>
        public bool CanViewFinancials(string? role) => role == "Admin" || (role == "Manager" && GetAllowManagerFinancialView());

        public int GetHeldOrderRetentionHours()
        {
            string value = _settingsRepo.GetSettingValue("HeldOrderRetentionHours", "72");
            if (int.TryParse(value, out int hours))
            {
                return hours;
            }
            return 72; // Absolute fail-safe fallback
        }

        public void SetHeldOrderRetentionHours(int hours)
        {
            if (hours < 0)
                throw new ArgumentException("Retention hours cannot be negative.");

            // ARCHITECT FIX: Domain Boundary Enforcement. Cap at 10 years (87,600 hours).
            if (hours > 87600)
                throw new ArgumentException("Hardware Limit Exceeded: Retention cannot exceed 87,600 hours (10 years).");

            _settingsRepo.UpdateSetting("HeldOrderRetentionHours", hours.ToString());
        }
    }
}