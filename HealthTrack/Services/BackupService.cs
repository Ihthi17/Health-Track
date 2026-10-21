using Microsoft.Data.SqlClient;
using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;
using System.Timers;

namespace HealthTrack.Services
{
    public interface IBackupService
    {
        Task<bool> CreateBackupAsync(string backupPath);
        Task<bool> RestoreBackupAsync(string backupPath);
        Task ScheduleAutoBackupsAsync();
        void StopAutoBackups();
    }

    public class BackupService : IBackupService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private System.Timers.Timer? _backupTimer;
        private DateTime? _lastBackupDate; // Track last backup to avoid duplicates on same day

        public BackupService(
            IConfiguration configuration,
            ILogger<BackupService> logger,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
            _lastBackupDate = null;
        }

        /// <summary>
        /// Creates a SQL Server database backup to the specified path
        /// </summary>
        public async Task<bool> CreateBackupAsync(string backupPath)
        {
            try
            {
                // Ensure backup directory exists
                var backupDirectory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDirectory) && !Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Create backup command
                    string backupCommand = $@"
                        BACKUP DATABASE [HealthTrackDB] 
                        TO DISK = N'{backupPath}'
                        WITH NOFORMAT, NOINIT, NAME = N'HealthTrack Backup', SKIP, REWIND, NOUNLOAD, STATS = 10
                    ";

                    using (var command = new SqlCommand(backupCommand, connection))
                    {
                        command.CommandTimeout = 600; // 10 minutes
                        await command.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation($"Database backup created successfully at: {backupPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating backup at {backupPath}");
                throw;
            }
        }

        /// <summary>
        /// Restores a SQL Server database from a backup file
        /// </summary>
        public async Task<bool> RestoreBackupAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException($"Backup file not found: {backupPath}");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Set database to single user mode
                    string singleUserCommand = @"
                        ALTER DATABASE [HealthTrackDB] 
                        SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                    ";

                    using (var command = new SqlCommand(singleUserCommand, connection))
                    {
                        command.CommandTimeout = 300;
                        await command.ExecuteNonQueryAsync();
                    }

                    // Restore database
                    string restoreCommand = $@"
                        RESTORE DATABASE [HealthTrackDB] 
                        FROM DISK = N'{backupPath}'
                        WITH REPLACE, RECOVERY
                    ";

                    using (var command = new SqlCommand(restoreCommand, connection))
                    {
                        command.CommandTimeout = 600;
                        await command.ExecuteNonQueryAsync();
                    }

                    // Set database back to multi-user mode
                    string multiUserCommand = @"
                        ALTER DATABASE [HealthTrackDB] 
                        SET MULTI_USER
                    ";

                    using (var command = new SqlCommand(multiUserCommand, connection))
                    {
                        command.CommandTimeout = 300;
                        await command.ExecuteNonQueryAsync();
                    }

                    _logger.LogInformation($"Database restored successfully from: {backupPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error restoring backup from {backupPath}");
                throw;
            }
        }

        /// <summary>
        /// Schedules automatic backups based on SystemSettings (from database).
        /// Polls every 1 minute to check if backup should run.
        /// Respects AutoBackupEnabled, BackupTime, BackupFrequency, BackupDayOfWeek settings.
        /// </summary>
        public async Task ScheduleAutoBackupsAsync()
        {
            try
            {
                // Create backup directory
                var backupDirectory = Path.Combine(AppContext.BaseDirectory, "Backups", "Auto");
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                _logger.LogInformation("Auto-backup scheduler started. Polling database for backup settings every 1 minute.");

                // Initialize timer to poll every 1 minute
                _backupTimer = new System.Timers.Timer();
                _backupTimer.Interval = 60 * 1000; // 1 minute in milliseconds
                _backupTimer.Elapsed += async (sender, e) => await CheckAndPerformBackupIfNeeded(backupDirectory);
                _backupTimer.AutoReset = true;
                _backupTimer.Start();

                _logger.LogInformation("Auto-backup scheduler now polling database settings.");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling automatic backups");
            }
        }

        /// <summary>
        /// Checks database settings and performs backup if conditions are met.
        /// Called by timer every 1 minute.
        /// </summary>
        private async Task CheckAndPerformBackupIfNeeded(string backupDirectory)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<HealthTrackContext>();
                    
                    // Get current backup settings from database
                    var settings = await context.SystemSettings.FirstOrDefaultAsync();
                    
                    if (settings == null || !settings.AutoBackupEnabled)
                    {
                        // Auto-backup is disabled
                        return;
                    }

                    // Get current time
                    var now = DateTime.Now;
                    var currentDate = now.Date;
                    
                    // Check if backup already ran today
                    if (_lastBackupDate.HasValue && _lastBackupDate.Value == currentDate)
                    {
                        // Already backed up today
                        return;
                    }

                    // Check if current time matches backup time (with 1-minute tolerance)
                    var backupTime = settings.BackupTime ?? new TimeSpan(2, 0, 0);
                    var timeOfDay = now.TimeOfDay;
                    var timeDifference = Math.Abs((timeOfDay - backupTime).TotalSeconds);

                    if (timeDifference > 60) // Allow 1 minute tolerance
                    {
                        // Not yet time for backup
                        return;
                    }

                    // Check frequency and day constraints
                    bool shouldBackup = false;

                    if (settings.BackupFrequency == "Daily")
                    {
                        shouldBackup = true;
                    }
                    else if (settings.BackupFrequency == "Weekly")
                    {
                        int dayOfWeek = (int)now.DayOfWeek; // 0=Sunday, 6=Saturday
                        if (settings.BackupDayOfWeek.HasValue && dayOfWeek == settings.BackupDayOfWeek.Value)
                        {
                            shouldBackup = true;
                        }
                    }
                    else if (settings.BackupFrequency == "Monthly")
                    {
                        // Run on first day of month
                        if (now.Day == 1)
                        {
                            shouldBackup = true;
                        }
                    }

                    if (shouldBackup)
                    {
                        _logger.LogInformation("Backup conditions met. Starting automatic backup.");
                        await PerformAutoBackup(backupDirectory, settings, context);
                        _lastBackupDate = currentDate;
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                // Service provider may be disposed during shutdown
                _logger.LogWarning(ex, "Service provider disposed during backup check");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking backup conditions");
            }
        }

        /// <summary>
        /// Called when it's time to perform auto-backup
        /// </summary>
        private async Task OnBackupTimerElapsed()
        {
            var backupDirectory = Path.Combine(AppContext.BaseDirectory, "Backups", "Auto");
            await PerformAutoBackup(backupDirectory, null, null);
        }

        /// <summary>
        /// Performs the actual auto-backup operation
        /// </summary>
        private async Task PerformAutoBackup(string backupDirectory, SystemSettings? settings, HealthTrackContext? contextPassedIn)
        {
            try
            {
                var backupFileName = $"HealthTrack_AutoBackup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var backupPath = Path.Combine(backupDirectory, backupFileName);

                _logger.LogInformation($"Starting automatic backup: {backupFileName}");

                if (await CreateBackupAsync(backupPath))
                {
                    // Log the backup using a new context instance if not provided
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = contextPassedIn ?? scope.ServiceProvider.GetRequiredService<HealthTrackContext>();
                        
                        var backupLog = new BackupLog
                        {
                            BackupFileName = backupFileName,
                            BackupPath = backupPath,
                            BackupDate = DateTime.UtcNow,
                            BackupType = "Scheduled",
                            Status = "Success",
                            Size = new FileInfo(backupPath).Length
                        };

                        context.BackupLogs.Add(backupLog);

                        // Update LastAutoBackupAt in SystemSettings
                        if (settings != null)
                        {
                            settings.LastAutoBackupAt = DateTime.UtcNow;
                            context.SystemSettings.Update(settings);
                        }

                        await context.SaveChangesAsync();

                        // Clean up old backups based on retention count
                        if (settings != null && settings.BackupRetentionCount > 0)
                        {
                            var oldBackups = Directory.GetFiles(backupDirectory, "*.bak")
                                .OrderByDescending(f => File.GetCreationTime(f))
                                .Skip(settings.BackupRetentionCount ?? 5)
                                .ToList();

                            foreach (var oldBackup in oldBackups)
                            {
                                try
                                {
                                    File.Delete(oldBackup);
                                    _logger.LogInformation($"Deleted old backup: {Path.GetFileName(oldBackup)}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, $"Could not delete old backup: {Path.GetFileName(oldBackup)}");
                                }
                            }
                        }
                    }

                    _logger.LogInformation($"Automatic backup completed: {backupFileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing automatic backup");
            }
        }

        /// <summary>
        /// Stops the auto-backup scheduler
        /// </summary>
        public void StopAutoBackups()
        {
            if (_backupTimer != null)
            {
                _backupTimer.Stop();
                _backupTimer.Dispose();
                _logger.LogInformation("Auto-backup scheduler stopped");
            }
        }
    }
}
