using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Service for managing system settings using the key-value store pattern
    /// </summary>
    public class SystemSettingsService
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<SystemSettingsService> _logger;

        public SystemSettingsService(HealthTrackContext context, ILogger<SystemSettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get a setting value by key
        /// </summary>
        public async Task<string?> GetSettingAsync(string key)
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == key);
                return setting?.SettingValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving setting: {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Set a setting value by key
        /// </summary>
        public async Task<bool> SetSettingAsync(string key, string? value, string? description = null)
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == key);

                if (setting == null)
                {
                    setting = new SystemSettings
                    {
                        SettingKey = key,
                        SettingValue = value,
                        Description = description,
                        DataType = "String",
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemSettings.Add(setting);
                }
                else
                {
                    setting.SettingValue = value;
                    setting.UpdatedAt = DateTime.UtcNow;
                    _context.SystemSettings.Update(setting);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for key: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Get SystemName setting
        /// </summary>
        public async Task<string> GetSystemNameAsync()
        {
            var value = await GetSettingAsync("SystemName");
            return value ?? "HealthTrack";
        }

        /// <summary>
        /// Set SystemName setting
        /// </summary>
        public async Task<bool> SetSystemNameAsync(string name)
        {
            return await SetSettingAsync("SystemName", name, "System brand name");
        }

        /// <summary>
        /// Get SystemLogo setting
        /// </summary>
        public async Task<string?> GetSystemLogoAsync()
        {
            return await GetSettingAsync("SystemLogo");
        }

        /// <summary>
        /// Set SystemLogo setting
        /// </summary>
        public async Task<bool> SetSystemLogoAsync(string? logoPath)
        {
            return await SetSettingAsync("SystemLogo", logoPath, "Logo file path");
        }

        /// <summary>
        /// Get MaintenanceMode setting
        /// </summary>
        public async Task<bool> GetMaintenanceModeAsync()
        {
            var value = await GetSettingAsync("MaintenanceMode");
            return value == "true" || value == "1";
        }

        /// <summary>
        /// Set MaintenanceMode setting
        /// </summary>
        public async Task<bool> SetMaintenanceModeAsync(bool enabled)
        {
            return await SetSettingAsync("MaintenanceMode", enabled ? "true" : "false", "Application maintenance mode");
        }

        /// <summary>
        /// Get all settings as a dictionary
        /// </summary>
        public async Task<Dictionary<string, string?>> GetAllSettingsAsync()
        {
            try
            {
                var settings = await _context.SystemSettings.ToListAsync();
                return settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all settings");
                return new Dictionary<string, string?>();
            }
        }

        /// <summary>
        /// Synchronous helper method to get setting value by key
        /// </summary>
        public string? GetSetting(string key)
        {
            try
            {
                var setting = _context.SystemSettings
                    .FirstOrDefault(s => s.SettingKey == key);
                return setting?.SettingValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving setting: {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Synchronous helper method to set setting value by key
        /// </summary>
        public bool SetSetting(string key, string? value, string? description = null)
        {
            try
            {
                var setting = _context.SystemSettings
                    .FirstOrDefault(s => s.SettingKey == key);

                if (setting == null)
                {
                    setting = new SystemSettings
                    {
                        SettingKey = key,
                        SettingValue = value,
                        Description = description,
                        DataType = "String",
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.SystemSettings.Add(setting);
                }
                else
                {
                    setting.SettingValue = value;
                    setting.UpdatedAt = DateTime.UtcNow;
                    _context.SystemSettings.Update(setting);
                }

                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting value for key: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Force refresh from database - clears EF Core cache
        /// </summary>
        public void ClearCache()
        {
            try
            {
                _context.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }

        /// <summary>
        /// Synchronous helper to get SystemName with cache clear
        /// </summary>
        public string GetSystemName()
        {
            ClearCache();
            var value = GetSetting("SystemName");
            return value ?? "HealthTrack";
        }

        /// <summary>
        /// Synchronous helper to set SystemName
        /// </summary>
        public bool SetSystemName(string name)
        {
            return SetSetting("SystemName", name, "System brand name");
        }

        /// <summary>
        /// Synchronous helper to get SystemLogo with cache clear
        /// </summary>
        public string? GetSystemLogo()
        {
            ClearCache();
            return GetSetting("SystemLogo");
        }

        /// <summary>
        /// Synchronous helper to set SystemLogo
        /// </summary>
        public bool SetSystemLogo(string? logoPath)
        {
            return SetSetting("SystemLogo", logoPath, "Logo file path");
        }

        /// <summary>
        /// Synchronous helper to get MaintenanceMode with cache clear
        /// </summary>
        public bool GetMaintenanceMode()
        {
            ClearCache();
            var value = GetSetting("MaintenanceMode");
            return value == "true" || value == "1";
        }

        /// <summary>
        /// Synchronous helper to set MaintenanceMode
        /// </summary>
        public bool SetMaintenanceMode(bool enabled)
        {
            return SetSetting("MaintenanceMode", enabled ? "true" : "false", "Application maintenance mode");
        }
    }
}