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

        /// <summary>PR-Q48: receipt header fields, Admin-only (BIN only if VAT-registered).</summary>
        public SwiftCheckoutPOS.BLL.Hardware.ReceiptHeaderDTO GetReceiptHeader() => new(
            _settingsRepo.GetSettingValue("Receipt.ShopName", "SWIFT CHECKOUT"),
            _settingsRepo.GetSettingValue("Receipt.Address", ""),
            _settingsRepo.GetSettingValue("Receipt.Phone", ""),
            _settingsRepo.GetSettingValue("Receipt.Bin", ""),
            _settingsRepo.GetSettingValue("Receipt.Footer", "Thank you for your purchase!"));

        public void SetReceiptHeader(SwiftCheckoutPOS.BLL.Hardware.ReceiptHeaderDTO header, string actor, string role)
        {
            if (role != "Admin") throw new InvalidOperationException("Only an Admin can change the receipt header.");
            var values = new Dictionary<string, string>
            {
                ["Receipt.ShopName"] = header.ShopName?.Trim() ?? "", ["Receipt.Address"] = header.Address?.Trim() ?? "",
                ["Receipt.Phone"] = header.Phone?.Trim() ?? "", ["Receipt.Bin"] = header.Bin?.Trim() ?? "",
                ["Receipt.Footer"] = header.Footer?.Trim() ?? ""
            };
            if (string.IsNullOrWhiteSpace(values["Receipt.ShopName"])) throw new ArgumentException("Enter the shop name.");
            if (values.Values.Any(v => v.Length > 48)) throw new ArgumentException("Each receipt line can be at most 48 characters (80 mm paper).");
            void Save()
            {
                foreach (var kv in values) _settingsRepo.UpdateSetting(kv.Key, kv.Value);
                _unit?.Audit(actor, "SettingChanged", "Setting", "ReceiptHeader", details: string.Join(" | ", values.Values));
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