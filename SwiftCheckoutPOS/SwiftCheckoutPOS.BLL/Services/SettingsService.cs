// Project: SwiftCheckoutPOS.BLL
// File: Services/SettingsService.cs
using System;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class SettingsService
    {
        private readonly SettingsRepository _settingsRepo;

        public SettingsService(SettingsRepository settingsRepo)
        {
            _settingsRepo = settingsRepo;
        }

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