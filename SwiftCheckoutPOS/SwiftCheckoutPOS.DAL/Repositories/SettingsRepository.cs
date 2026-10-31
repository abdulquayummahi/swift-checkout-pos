// Project: SwiftCheckoutPOS.DAL
// File: Repositories/SettingsRepository.cs
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class SettingsRepository
    {
        private readonly ApplicationDbContext _context;

        public SettingsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public string GetSettingValue(string key, string defaultValue = "")
        {
            var setting = _context.SystemSettings.AsNoTracking().FirstOrDefault(s => s.SettingKey == key);
            return setting != null ? setting.SettingValue : defaultValue;
        }

        public void UpdateSetting(string key, string value)
        {
            var setting = _context.SystemSettings.FirstOrDefault(s => s.SettingKey == key);
            if (setting != null)
            {
                setting.SettingValue = value;
                setting.LastUpdated = DateTime.Now;
                _context.SystemSettings.Update(setting);
            }
            else
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    LastUpdated = DateTime.Now
                });
            }
            _context.SaveChanges();
        }
    }
}