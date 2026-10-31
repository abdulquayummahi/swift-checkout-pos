// Project: SwiftCheckoutPOS.DAL
// File: Entities/SystemSetting.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class SystemSetting
    {
        [Key]
        [MaxLength(50)]
        public string SettingKey { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string SettingValue { get; set; } = null!;

        [MaxLength(255)]
        public string? Description { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}