using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("superadmin")]
    public class SuperAdminController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<SuperAdminController> _logger;
        private readonly IBackupService _backupService;
        private readonly IConfiguration _configuration;
        private readonly SystemSettingsService _settingsService;

        public SuperAdminController(
            HealthTrackContext context, 
            ILogger<SuperAdminController> logger,
            IBackupService backupService,
            IConfiguration configuration,
            SystemSettingsService settingsService)
        {
            _context = context;
            _logger = logger;
            _backupService = backupService;
            _configuration = configuration;
            _settingsService = settingsService;
        }

        #region Helper Methods

        /// <summary>
        /// Loads all active users with Client role for the appointment form dropdown
        /// </summary>
        private async Task<List<ClientProfile>> GetClientsAsync()
        {
            try
            {
                var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");
                if (clientRole == null)
                    return new List<ClientProfile>();

                // Get all active users with Client role
                var clientUsers = await _context.Users
                    .Where(u => u.RoleID == clientRole.RoleID && u.IsActive)
                    .ToListAsync();

                // Convert to ClientProfile list (for compatibility with view)
                var result = clientUsers.Select(u => new ClientProfile
                {
                    ClientProfileID = 0, // Will be set from database if profile exists
                    UserID = u.UserID,
                    User = u
                }).ToList();

                _logger.LogInformation($"Loaded {result.Count} active clients");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading clients");
                return new List<ClientProfile>();
            }
        }

        /// <summary>
        /// Loads all active users with Trainer role for the appointment form dropdown
        /// </summary>
        private async Task<List<TrainerProfile>> GetTrainersAsync()
        {
            try
            {
                var trainerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Trainer");
                if (trainerRole == null)
                    return new List<TrainerProfile>();

                // Get all active users with Trainer role
                var trainerUsers = await _context.Users
                    .Where(u => u.RoleID == trainerRole.RoleID && u.IsActive)
                    .ToListAsync();

                // Convert to TrainerProfile list (for compatibility with view)
                var result = trainerUsers.Select(u => new TrainerProfile
                {
                    TrainerProfileID = 0, // Will be set from database if profile exists
                    UserID = u.UserID,
                    User = u
                }).ToList();

                _logger.LogInformation($"Loaded {result.Count} active trainers");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading trainers");
                return new List<TrainerProfile>();
            }
        }

        #endregion

        #region Dashboard

        // Dashboard Index
        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Index()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            
            var vm = new SuperAdminDashboardViewModel();

            try
            {
                _logger.LogInformation("=== SuperAdmin Dashboard Loading ===");

                // Get roles
                var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");
                var trainerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Trainer");
                var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");

                try
                {
                    // Count users
                    vm.TotalUsers = await _context.Users.CountAsync();
                    vm.ActiveUsers = await _context.Users.CountAsync(u => u.IsActive);
                    vm.TotalClients = clientRole != null ? await _context.Users.CountAsync(u => u.RoleID == clientRole.RoleID) : 0;
                    vm.TotalTrainers = trainerRole != null ? await _context.Users.CountAsync(u => u.RoleID == trainerRole.RoleID) : 0;
                    vm.TotalAdmins = adminRole != null ? await _context.Users.CountAsync(u => u.RoleID == adminRole.RoleID) : 0;

                    _logger.LogInformation($"Users: Total={vm.TotalUsers}, Active={vm.ActiveUsers}, Clients={vm.TotalClients}, Trainers={vm.TotalTrainers}, Admins={vm.TotalAdmins}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error counting users");
                }

                try
                {
                    // Count appointments by status
                    vm.TotalAppointments = await _context.Appointments.CountAsync();
                    vm.PendingAppointments = await _context.Appointments.CountAsync(a => a.Status == "Pending");
                    vm.ApprovedAppointments = await _context.Appointments.CountAsync(a => a.Status == "Approved");
                    vm.CompletedAppointments = await _context.Appointments.CountAsync(a => a.Status == "Completed");
                    vm.CancelledAppointments = await _context.Appointments.CountAsync(a => a.Status == "Cancelled");

                    _logger.LogInformation($"Appointments: Total={vm.TotalAppointments}, Pending={vm.PendingAppointments}, Approved={vm.ApprovedAppointments}, Completed={vm.CompletedAppointments}, Cancelled={vm.CancelledAppointments}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error counting appointments");
                }

                try
                {
                    // Get recent appointments with names
                    var recentAppointments = await _context.Appointments
                        .Include(a => a.Client)
                            .ThenInclude(c => c!.User)
                        .Include(a => a.Trainer)
                            .ThenInclude(t => t!.User)
                        .OrderByDescending(a => a.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    vm.RecentAppointments = recentAppointments.Select(a => new AppointmentSummary
                    {
                        AppointmentID = a.AppointmentID,
                        ClientName = (a.Client?.User?.FirstName ?? "Unknown") + " " + (a.Client?.User?.LastName ?? ""),
                        TrainerName = (a.Trainer?.User?.FirstName ?? "Unknown") + " " + (a.Trainer?.User?.LastName ?? ""),
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Status = a.Status
                    }).ToList();

                    _logger.LogInformation($"Recent appointments loaded: {vm.RecentAppointments.Count}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading appointments");
                }

                try
                {
                    // Get recent notifications
                    var recentNotifications = await _context.Notifications
                        .Where(n => n.UserID == userId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    vm.RecentNotifications = recentNotifications.Select(n => new NotificationSummary
                    {
                        NotificationID = n.NotificationID,
                        Title = n.Title,
                        Message = n.Message,
                        Type = n.Type,
                        CreatedAt = n.CreatedAt,
                        IsRead = n.IsRead
                    }).ToList();

                    _logger.LogInformation($"Recent notifications loaded: {vm.RecentNotifications.Count}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading notifications");
                }

                try
                {
                    // Get recent audit logs
                    var recentLogs = await _context.AuditLogs
                        .OrderByDescending(a => a.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    vm.RecentAuditLogs = recentLogs.Select(a => new AuditLogSummary
                    {
                        AuditID = a.AuditID,
                        Action = a.Action,
                        Details = a.Details,
                        CreatedAt = a.CreatedAt,
                        IPAddress = a.IPAddress,
                        PerformedBy = a.UserID.HasValue ? $"User {a.UserID}" : "System"
                    }).ToList();

                    _logger.LogInformation($"Recent audit logs loaded: {vm.RecentAuditLogs.Count}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading audit logs");
                }

                // Build chart data as JSON
                var appointmentStatusData = new
                {
                    labels = new[] { "Pending", "Approved", "Completed", "Cancelled" },
                    datasets = new[] {
                        new {
                            data = new[] { vm.PendingAppointments, vm.ApprovedAppointments, vm.CompletedAppointments, vm.CancelledAppointments },
                            backgroundColor = new[] { "#FFC107", "#28A745", "#007BFF", "#DC3545" }
                        }
                    }
                };
                vm.AppointmentStatusChartJson = System.Text.Json.JsonSerializer.Serialize(appointmentStatusData);

                var usersByRoleData = new
                {
                    labels = new[] { "Clients", "Trainers", "Admins" },
                    datasets = new[] {
                        new {
                            label = "Users by Role",
                            data = new[] { vm.TotalClients, vm.TotalTrainers, vm.TotalAdmins },
                            backgroundColor = new[] { "#007BFF", "#28A745", "#FFC107" }
                        }
                    }
                };
                vm.UsersByRoleChartJson = System.Text.Json.JsonSerializer.Serialize(usersByRoleData);

                _logger.LogInformation("=== Dashboard Loading Complete ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outer error loading SuperAdmin dashboard");
                vm.ErrorMessage = $"Error loading dashboard: {ex.Message}";
            }

            return View(vm);
        }

        [HttpGet("api/notifications")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            
            var notifications = await _context.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserID == userId && !n.IsRead);

            return Json(new { notifications, unreadCount });
        }

        [HttpPost("api/notifications/{id}/read")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);

            if (notification != null)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost("api/notifications/mark-all-read")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            
            var notifications = await _context.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in notifications)
            {
                n.IsRead = true;
            }

            _context.Notifications.UpdateRange(notifications);
            await _context.SaveChangesAsync();

            return Ok();
        }

        #endregion

        #region User Management

        [HttpGet("users")]
        public async Task<IActionResult> Users(int page = 1)
        {
            try
            {
                const int pageSize = 10;
                var users = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.Role != null)  // Filter for valid Role references
                    .OrderByDescending(u => u.UserID)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalUsers = await _context.Users.CountAsync();
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
                ViewBag.CurrentPage = page;

                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users list");
                ViewBag.ErrorMessage = $"Error loading users: {ex.Message}";
                ViewBag.TotalPages = 0;
                ViewBag.CurrentPage = page;
                
                return View(new List<User>());
            }
        }

        [HttpGet("users/create")]
        public async Task<IActionResult> CreateUser()
        {
            var roles = await _context.Roles.ToListAsync();
            ViewBag.Roles = roles;
            return View();
        }

        [HttpPost("users/create")]
        public async Task<IActionResult> CreateUser(string email, string firstName, string lastName, int roleId, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    ModelState.AddModelError("", "Email and Password are required.");
                    var roles = await _context.Roles.ToListAsync();
                    ViewBag.Roles = roles;
                    return View();
                }

                // Ensure firstName and lastName are not empty
                if (string.IsNullOrWhiteSpace(firstName))
                    firstName = "N/A";
                if (string.IsNullOrWhiteSpace(lastName))
                    lastName = "N/A";

                if (password.Length < 8)
                {
                    ModelState.AddModelError("", "Password must be at least 8 characters.");
                    var roles = await _context.Roles.ToListAsync();
                    ViewBag.Roles = roles;
                    return View();
                }

                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == email))
                {
                    ModelState.AddModelError("Email", "Email already exists.");
                    var roles = await _context.Roles.ToListAsync();
                    ViewBag.Roles = roles;
                    return View();
                }

                // Hash password using PasswordHasher
                PasswordHasher.CreatePasswordHash(
                    password,
                    out var newHash,
                    out var newSalt);

                var user = new User
                {
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    RoleID = roleId > 0 ? roleId : null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    PasswordHash = newHash,
                    PasswordSalt = newSalt
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Create role-specific profiles if needed
                if (roleId > 0)
                {
                    var role = await _context.Roles.FindAsync(roleId);
                    if (role?.RoleName == "Trainer")
                    {
                        _context.TrainerProfiles.Add(new TrainerProfile
                        {
                            TrainerProfileID = user.UserID
                        });
                        await _context.SaveChangesAsync();
                    }
                    else if (role?.RoleName == "Client")
                    {
                        _context.ClientProfiles.Add(new ClientProfile
                        {
                            ClientProfileID = user.UserID
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // Log audit
                await LogAudit($"Created user: {user.Email}", "UserCreate", user.Email);

                TempData["SuccessMessage"] = $"User {user.Email} created successfully.";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                ModelState.AddModelError("", "Error creating user: " + ex.Message);
                var roles = await _context.Roles.ToListAsync();
                ViewBag.Roles = roles;
                return View();
            }
        }

        [HttpGet("users/edit/{id}")]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == id);
            if (user == null)
                return NotFound();

            var roles = await _context.Roles.ToListAsync();
            ViewBag.Roles = roles;
            return View(user);
        }

        [HttpPost("users/edit/{id}")]
        public async Task<IActionResult> EditUser(int id, [Bind("UserID,Email,FirstName,LastName,RoleID,IsActive")] User user)
        {
            if (id != user.UserID)
                return BadRequest();

            try
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser == null)
                    return NotFound();

                // Check if email is already taken by another user
                if (user.Email != existingUser.Email && await _context.Users.AnyAsync(u => u.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists.");
                    var roles = await _context.Roles.ToListAsync();
                    ViewBag.Roles = roles;
                    return View(user);
                }

                existingUser.Email = user.Email;
                existingUser.FirstName = user.FirstName;
                existingUser.LastName = user.LastName;
                existingUser.RoleID = user.RoleID;
                existingUser.IsActive = user.IsActive;

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();

                await LogAudit($"Updated user: {user.Email}", "UserEdit", user.Email);
                TempData["SuccessMessage"] = $"User {user.Email} updated successfully.";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                ModelState.AddModelError("", "Error updating user: " + ex.Message);
                var roles = await _context.Roles.ToListAsync();
                ViewBag.Roles = roles;
                return View(user);
            }
        }

        [HttpPost("users/delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                    return NotFound();

                var userEmail = user.Email;
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                await LogAudit($"Deleted user: {userEmail}", "UserDelete", userEmail);
                TempData["SuccessMessage"] = $"User {userEmail} deleted successfully.";
                return RedirectToAction("Users");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                TempData["ErrorMessage"] = "Error deleting user: " + ex.Message;
                return RedirectToAction("Users");
            }
        }

        #endregion

        #region Role & Permission Management

        [HttpGet("roles")]
        public async Task<IActionResult> Roles()
        {
            var roles = await _context.Roles
                .Include(r => r.Permissions)
                .ToListAsync();
            
            // Feature B: Pass all permissions for the toggle UI
            var allPermissions = await _context.Permissions.ToListAsync();
            ViewBag.AllPermissions = allPermissions;
            
            return View(roles);
        }

        [HttpGet("roles/create")]
        public async Task<IActionResult> CreateRole()
        {
            var allPermissions = await _context.Permissions.ToListAsync();
            ViewBag.AllPermissions = allPermissions;
            return View();
        }

        [HttpPost("roles/create")]
        public async Task<IActionResult> CreateRole([Bind("RoleName,Description")] Role role, int[] permissions)
        {
            try
            {
                if (await _context.Roles.AnyAsync(r => r.RoleName == role.RoleName))
                {
                    ModelState.AddModelError("RoleName", "Role name already exists.");
                    var allPerms = await _context.Permissions.ToListAsync();
                    ViewBag.AllPermissions = allPerms;
                    return View(role);
                }

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();

                // Add permissions to the role
                if (permissions != null && permissions.Length > 0)
                {
                    foreach (var permId in permissions)
                    {
                        var permission = await _context.Permissions.FindAsync(permId);
                        if (permission != null)
                            role.Permissions.Add(permission);
                    }
                    _context.Roles.Update(role);
                    await _context.SaveChangesAsync();
                }

                await LogAudit($"Created role: {role.RoleName}", "RoleCreate", role.RoleName);
                TempData["SuccessMessage"] = $"Role {role.RoleName} created successfully.";
                return RedirectToAction("Roles");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                ModelState.AddModelError("", "Error creating role: " + ex.Message);
                var allPerms = await _context.Permissions.ToListAsync();
                ViewBag.AllPermissions = allPerms;
                return View(role);
            }
        }

        [HttpGet("roles/edit/{id}")]
        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _context.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.RoleID == id);

            if (role == null)
                return NotFound();

            var allPermissions = await _context.Permissions.ToListAsync();
            ViewBag.AllPermissions = allPermissions;
            ViewBag.RolePermissionIds = role.Permissions.Select(p => p.PermissionID).ToList();

            return View(role);
        }

        [HttpPost("roles/edit/{id}")]
        public async Task<IActionResult> EditRole(int id, [Bind("RoleID,RoleName,Description")] Role role, int[] permissions)
        {
            if (id != role.RoleID)
                return BadRequest();

            try
            {
                var existingRole = await _context.Roles
                    .Include(r => r.Permissions)
                    .FirstOrDefaultAsync(r => r.RoleID == id);

                if (existingRole == null)
                    return NotFound();

                existingRole.RoleName = role.RoleName;
                existingRole.Description = role.Description;

                // Update permissions
                var currentPermissions = existingRole.Permissions.Select(p => p.PermissionID).ToList();
                var permissionsToAdd = permissions.Except(currentPermissions).ToList();
                var permissionsToRemove = currentPermissions.Except(permissions).ToList();

                foreach (var permId in permissionsToAdd)
                {
                    var permission = await _context.Permissions.FindAsync(permId);
                    if (permission != null)
                        existingRole.Permissions.Add(permission);
                }

                foreach (var permId in permissionsToRemove)
                {
                    var permission = existingRole.Permissions.FirstOrDefault(p => p.PermissionID == permId);
                    if (permission != null)
                        existingRole.Permissions.Remove(permission);
                }

                _context.Roles.Update(existingRole);
                await _context.SaveChangesAsync();

                await LogAudit($"Updated role: {role.RoleName}", "RoleEdit", role.RoleName);
                TempData["SuccessMessage"] = $"Role {role.RoleName} updated successfully.";
                return RedirectToAction("Roles");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role");
                ModelState.AddModelError("", "Error updating role: " + ex.Message);

                var allPermissions = await _context.Permissions.ToListAsync();
                ViewBag.AllPermissions = allPermissions;
                ViewBag.RolePermissionIds = permissions.ToList();

                return View(role);
            }
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> Permissions()
        {
            var permissions = await _context.Permissions.ToListAsync();
            return View(permissions);
        }

        [HttpGet("permissions/create")]
        public IActionResult CreatePermission()
        {
            return View();
        }

        [HttpPost("permissions/create")]
        public async Task<IActionResult> CreatePermission([Bind("PermissionName,Description")] Permission permission)
        {
            try
            {
                if (await _context.Permissions.AnyAsync(p => p.PermissionName == permission.PermissionName))
                {
                    ModelState.AddModelError("PermissionName", "Permission name already exists.");
                    return View(permission);
                }

                _context.Permissions.Add(permission);
                await _context.SaveChangesAsync();

                await LogAudit($"Created permission: {permission.PermissionName}", "PermissionCreate", permission.PermissionName);
                TempData["SuccessMessage"] = $"Permission {permission.PermissionName} created successfully.";
                return RedirectToAction("Permissions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating permission");
                ModelState.AddModelError("", "Error creating permission: " + ex.Message);
                return View(permission);
            }
        }

        [HttpPost("permissions/delete/{id}")]
        public async Task<IActionResult> DeletePermission(int id)
        {
            try
            {
                var permission = await _context.Permissions.FindAsync(id);
                if (permission == null)
                    return NotFound();

                var permName = permission.PermissionName;
                _context.Permissions.Remove(permission);
                await _context.SaveChangesAsync();

                await LogAudit($"Deleted permission: {permName}", "PermissionDelete", permName);
                TempData["SuccessMessage"] = $"Permission {permName} deleted successfully.";
                return RedirectToAction("Permissions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting permission");
                TempData["ErrorMessage"] = "Error deleting permission: " + ex.Message;
                return RedirectToAction("Permissions");
            }
        }

        /// <summary>
        /// Feature B: Save role permission toggles (which permissions each role has)
        /// </summary>
        [HttpPost("roles/{roleId}/permissions")]
        public async Task<IActionResult> SaveRolePermissions(int roleId, [FromForm] string[] permissionIds)
        {
            try
            {
                var role = await _context.Roles
                    .Include(r => r.Permissions)
                    .FirstOrDefaultAsync(r => r.RoleID == roleId);

                if (role == null)
                    return NotFound();

                // Parse permission IDs
                var selectedPermIds = permissionIds
                    .Where(id => int.TryParse(id, out _))
                    .Select(int.Parse)
                    .ToHashSet();

                // Get current permission IDs for this role
                var currentPermIds = role.Permissions
                    .Select(p => p.PermissionID)
                    .ToHashSet();

                // Determine permissions to add and remove
                var toAdd = selectedPermIds.Except(currentPermIds).ToList();
                var toRemove = currentPermIds.Except(selectedPermIds).ToList();

                // Add new permissions
                foreach (var permId in toAdd)
                {
                    var permission = await _context.Permissions.FindAsync(permId);
                    if (permission != null)
                    {
                        role.Permissions.Add(permission);
                    }
                }

                // Remove permissions
                foreach (var permId in toRemove)
                {
                    var permission = role.Permissions.FirstOrDefault(p => p.PermissionID == permId);
                    if (permission != null)
                    {
                        role.Permissions.Remove(permission);
                    }
                }

                await _context.SaveChangesAsync();

                await LogAudit(
                    $"Updated permissions for role {role.RoleName}: Added {toAdd.Count}, Removed {toRemove.Count}",
                    "RolePermissionChange",
                    role.RoleName
                );

                TempData["SuccessMessage"] = $"Permissions for role '{role.RoleName}' updated successfully.";
                return RedirectToAction("Roles");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving role permissions");
                TempData["ErrorMessage"] = "Error saving role permissions: " + ex.Message;
                return RedirectToAction("Roles");
            }
        }

        #endregion

        #region System Settings

        [HttpGet("settings")]
        public async Task<IActionResult> Settings()
        {
            // Clear any cached settings
            _settingsService.ClearCache();

            var systemSettings = await _context.SystemSettings.FirstOrDefaultAsync() ?? new SystemSettings();
            
            // Populate the NotMapped properties from the settings service
            systemSettings.SystemName = await _settingsService.GetSystemNameAsync();
            systemSettings.SystemLogo = await _settingsService.GetSystemLogoAsync();
            systemSettings.MaintenanceMode = await _settingsService.GetMaintenanceModeAsync();
            
            var emailSettings = await _context.EmailSettings.FirstOrDefaultAsync();

            ViewBag.EmailSettings = emailSettings;
            
            // Add no-cache headers to prevent browser caching
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            
            return View(systemSettings);
        }

        [HttpPost("settings")]
        public async Task<IActionResult> Settings(SystemSettings settings, IFormFile? LogoFile)
        {
            try
            {
                var existing = await _context.SystemSettings.FirstOrDefaultAsync();

                // Handle logo file upload
                if (LogoFile != null && LogoFile.Length > 0)
                {
                    // Validate file
                    const long maxFileSize = 2 * 1024 * 1024; // 2 MB
                    var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
                    var allowedMimeTypes = new[] { "image/png", "image/jpeg", "image/webp" };

                    if (LogoFile.Length > maxFileSize)
                    {
                        ModelState.AddModelError("LogoFile", "File size cannot exceed 2 MB");
                        var s = await _context.SystemSettings.FirstOrDefaultAsync();
                        return View(s ?? new SystemSettings());
                    }

                    var fileExtension = Path.GetExtension(LogoFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("LogoFile", "Only PNG, JPG, JPEG, and WebP files are allowed");
                        var s = await _context.SystemSettings.FirstOrDefaultAsync();
                        return View(s ?? new SystemSettings());
                    }

                    if (!allowedMimeTypes.Contains(LogoFile.ContentType))
                    {
                        ModelState.AddModelError("LogoFile", "Invalid file type");
                        var s = await _context.SystemSettings.FirstOrDefaultAsync();
                        return View(s ?? new SystemSettings());
                    }

                    // GET OLD LOGO PATH BEFORE SAVING NEW ONE
                    var oldLogo = await _settingsService.GetSystemLogoAsync();

                    // Create upload directory if it doesn't exist
                    var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "logos");
                    Directory.CreateDirectory(uploadDir);

                    // Generate unique filename
                    var fileName = $"logo_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{fileExtension}";
                    var filePath = Path.Combine(uploadDir, fileName);

                    // Save file to disk
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await LogoFile.CopyToAsync(stream);
                    }

                    // Verify file was saved
                    if (!System.IO.File.Exists(filePath))
                    {
                        ModelState.AddModelError("LogoFile", "Failed to save logo file");
                        return View(settings);
                    }

                    // Store relative path using settings service
                    var logoPath = $"/uploads/logos/{fileName}";
                    await _settingsService.SetSystemLogoAsync(logoPath);

                    // Delete old logo if it exists and is an uploaded file
                    if (oldLogo != null && oldLogo.StartsWith("/uploads/logos/"))
                    {
                        var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldLogo.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldPath);
                                _logger.LogInformation("Deleted old logo file: {0}", oldPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Could not delete old logo file: {0}", oldPath);
                            }
                        }
                    }
                }

                // Update system name using settings service
                if (!string.IsNullOrWhiteSpace(settings.SystemName))
                {
                    await _settingsService.SetSystemNameAsync(settings.SystemName);
                }

                // Update maintenance mode using settings service
                await _settingsService.SetMaintenanceModeAsync(settings.MaintenanceMode);

                await LogAudit("Updated system settings", "SettingsEdit", "SystemSettings");
                TempData["SuccessMessage"] = "System settings updated successfully.";
                return RedirectToAction("Settings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings");
                ModelState.AddModelError("", "Error updating settings: " + ex.Message);
                return View(settings);
            }
        }

        #endregion

        #region Email Settings

        [HttpGet("settings/email")]
        public async Task<IActionResult> EmailSettings()
        {
            var emailSettings = await _context.EmailSettings.FirstOrDefaultAsync();
            return View(emailSettings ?? new EmailSettings());
        }

        [HttpPost("settings/email")]
        public async Task<IActionResult> EmailSettings([Bind("EmailSettingsID,SMTPServer,SMTPPort,SMTPUsername,SMTPPassword,FromEmail,FromName,EnableSSL")] EmailSettings emailSettings)
        {
            try
            {
                var existing = await _context.EmailSettings.FirstOrDefaultAsync();

                if (existing == null)
                {
                    emailSettings.CreatedAt = DateTime.UtcNow;
                    _context.EmailSettings.Add(emailSettings);
                }
                else
                {
                    existing.SMTPServer = emailSettings.SMTPServer;
                    existing.SMTPPort = emailSettings.SMTPPort;
                    existing.SMTPUsername = emailSettings.SMTPUsername;
                    existing.SMTPPassword = emailSettings.SMTPPassword;
                    existing.FromEmail = emailSettings.FromEmail;
                    existing.FromName = emailSettings.FromName;
                    existing.EnableSSL = emailSettings.EnableSSL;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _context.EmailSettings.Update(existing);
                }

                await _context.SaveChangesAsync();
                await LogAudit("Updated email settings", "EmailSettingsEdit", "EmailSettings");
                TempData["SuccessMessage"] = "Email settings updated successfully.";
                return RedirectToAction("EmailSettings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating email settings");
                ModelState.AddModelError("", "Error updating email settings: " + ex.Message);
                return View(emailSettings);
            }
        }

        #endregion

        #region Audit Logs

        [HttpGet("audit-logs")]
        public async Task<IActionResult> AuditLogs(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // Validate pageSize to prevent abuse
                if (pageSize < 5 || pageSize > 100)
                    pageSize = 10;

                var query = _context.AuditLogs.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(a => a.Action.Contains(search) || (a.Details != null && a.Details.Contains(search)));
                }

                var totalLogs = await query.CountAsync();
                var logs = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalLogs / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = totalLogs;
                ViewBag.SearchQuery = search;

                return View(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit logs");
                ViewBag.ErrorMessage = $"Error loading audit logs: {ex.Message}";
                ViewBag.TotalPages = 0;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.SearchQuery = search;
                
                return View(new List<AuditLog>());
            }
        }

        #endregion

        #region Profile Management

        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpGet("profile/edit")]
        public async Task<IActionResult> EditProfile()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            
            if (user == null)
                return NotFound();

            return View(user);
        }

        [HttpPost("profile/edit")]
        public async Task<IActionResult> EditProfile([Bind("UserID,FirstName,LastName,Email")] User user)
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            
            if (user.UserID != userId)
                return Unauthorized();

            try
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserID == user.UserID);
                
                if (existingUser == null)
                    return NotFound();

                existingUser.FirstName = user.FirstName;
                existingUser.LastName = user.LastName;
                
                // Check if email is already in use
                if (existingUser.Email != user.Email && await _context.Users.AnyAsync(u => u.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "Email already in use");
                    return View(user);
                }
                
                existingUser.Email = user.Email;

                _context.Users.Update(existingUser);
                await _context.SaveChangesAsync();

                await LogAudit("Updated profile information", "ProfileEdit", user.Email ?? "Unknown");
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                ModelState.AddModelError("", "Error updating profile: " + ex.Message);
                return View(user);
            }
        }

        [HttpGet("profile/change-password")]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost("profile/change-password")]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("confirmPassword", "Passwords do not match");
                return View();
            }

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                ModelState.AddModelError("", "All fields are required");
                return View();
            }

            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            
            if (user == null)
                return NotFound();

            try
            {
                // Verify current password (you'll need to implement proper password verification)
                // This is a placeholder - implement proper password hashing verification
                
                // Generate new password hash and salt using modern PBKDF2
                // Hash new password using PasswordHasher
                PasswordHasher.CreatePasswordHash(
                    newPassword,
                    out var hashNew,
                    out var saltNew);
                
                user.PasswordHash = hashNew;
                user.PasswordSalt = saltNew;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await LogAudit("Changed password", "PasswordChange", user.Email ?? "Unknown");
                TempData["SuccessMessage"] = "Password changed successfully.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                ModelState.AddModelError("", "Error changing password: " + ex.Message);
                return View();
            }
        }

        #endregion

        #region Appointments

        [HttpGet("appointments")]
        public async Task<IActionResult> Appointments(int page = 1, string status = "", DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                const int pageSize = 20;
                var query = _context.Appointments
                    .Include(a => a.Client)
                    .Include(a => a.Trainer)
                    .Where(a => a.Client != null)  // Filter for valid Client references
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(a => a.Status == status);

                if (fromDate.HasValue)
                    query = query.Where(a => a.StartTime >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(a => a.StartTime <= toDate.Value.AddDays(1));

                var appointments = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalAppointments = await query.CountAsync();
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalAppointments / pageSize);
                ViewBag.CurrentPage = page;
                ViewBag.StatusFilter = status;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;

                return View(appointments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading appointments list");
                ViewBag.ErrorMessage = $"Error loading appointments: {ex.Message}";
                ViewBag.TotalPages = 0;
                ViewBag.CurrentPage = page;
                
                return View(new List<Appointment>());
            }
        }

        [HttpGet("appointments/create")]
        public async Task<IActionResult> CreateAppointment()
        {
            try
            {
                var clients = await GetClientsAsync();
                var trainers = await GetTrainersAsync();
                
                ViewBag.Clients = clients;
                ViewBag.Trainers = trainers;
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create appointment form");
                ViewBag.ErrorMessage = $"Error loading form data: {ex.Message}";
                ViewBag.Clients = new List<ClientProfile>();
                ViewBag.Trainers = new List<TrainerProfile>();
                
                return View();
            }
        }

        [HttpPost("appointments/create")]
        public async Task<IActionResult> CreateAppointment([Bind("ClientID,TrainerID,StartTime,EndTime,Reason")] Appointment appointment)
        {
            try
            {
                // Resolve UserID to ClientProfile ID
                if (appointment.ClientID > 0)
                {
                    var clientProfile = await _context.ClientProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.UserID == appointment.ClientID);
                    
                    if (clientProfile != null)
                    {
                        appointment.ClientID = clientProfile.ClientProfileID;
                    }
                    else
                    {
                        ModelState.AddModelError("ClientID", "Selected client is invalid");
                    }
                }
                else
                {
                    ModelState.AddModelError("ClientID", "Please select a client");
                }

                // Resolve UserID to TrainerProfile ID
                if (appointment.TrainerID > 0)
                {
                    var trainerProfile = await _context.TrainerProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.UserID == appointment.TrainerID);
                    
                    if (trainerProfile != null)
                    {
                        appointment.TrainerID = trainerProfile.TrainerProfileID;
                    }
                    else
                    {
                        ModelState.AddModelError("TrainerID", "Selected trainer is invalid");
                    }
                }
                else
                {
                    ModelState.AddModelError("TrainerID", "Please select a trainer");
                }

                if (appointment.StartTime >= appointment.EndTime)
                {
                    ModelState.AddModelError("", "Start time must be before end time");
                }

                if (appointment.StartTime <= DateTime.Now)
                {
                    ModelState.AddModelError("", "Appointment must be scheduled for the future");
                }

                if (!ModelState.IsValid)
                {
                    var clients = await GetClientsAsync();
                    var trainers = await GetTrainersAsync();
                    ViewBag.Clients = clients;
                    ViewBag.Trainers = trainers;
                    return View(appointment);
                }

                appointment.Status = "Pending";
                appointment.CreatedAt = DateTime.UtcNow;

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Appointment created: ID {appointment.AppointmentID}, Client {appointment.ClientID}, Trainer {appointment.TrainerID}");
                await LogAudit($"Created appointment ID {appointment.AppointmentID}", "AppointmentCreate", "Appointment");
                TempData["SuccessMessage"] = "Appointment created successfully.";
                return RedirectToAction("Appointments");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating appointment");
                var innerEx = dbEx.InnerException;
                var errorMessage = "Error creating appointment: ";
                
                if (innerEx != null)
                {
                    _logger.LogError($"Inner exception: {innerEx.Message}");
                    errorMessage += ExtractSqlErrorMessage(innerEx.Message);
                }
                else
                {
                    errorMessage += "Failed to save appointment to database.";
                }

                var clients = await GetClientsAsync();
                var trainers = await GetTrainersAsync();
                ViewBag.Clients = clients;
                ViewBag.Trainers = trainers;
                ModelState.AddModelError("", errorMessage);
                return View(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating appointment");
                var clients = await GetClientsAsync();
                var trainers = await GetTrainersAsync();
                ViewBag.Clients = clients;
                ViewBag.Trainers = trainers;
                ModelState.AddModelError("", "An unexpected error occurred while creating the appointment. Please try again.");
                return View(appointment);
            }
        }

        [HttpGet("appointments/edit/{id}")]
        public async Task<IActionResult> EditAppointment(int id)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Client)
                    .Include(a => a.Trainer)
                    .FirstOrDefaultAsync(a => a.AppointmentID == id);

                if (appointment == null)
                    return NotFound();

                var clients = await GetClientsAsync();
                var trainers = await GetTrainersAsync();
                
                ViewBag.Clients = clients;
                ViewBag.Trainers = trainers;

                return View(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading edit appointment form for ID {id}");
                ViewBag.ErrorMessage = $"Error loading appointment: {ex.Message}";
                return NotFound();
            }
        }

        [HttpPost("appointments/edit/{id}")]
        public async Task<IActionResult> EditAppointment(int id, [Bind("AppointmentID,ClientID,TrainerID,StartTime,EndTime,Status,Reason,TrainerNotes")] Appointment appointment)
        {
            if (id != appointment.AppointmentID)
                return BadRequest();

            try
            {
                var existing = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == id);
                
                if (existing == null)
                    return NotFound();

                // Validate client exists
                if (appointment.ClientID > 0)
                {
                    var clientExists = await _context.ClientProfiles
                        .AsNoTracking()
                        .AnyAsync(c => c.ClientProfileID == appointment.ClientID);
                    
                    if (!clientExists)
                    {
                        ModelState.AddModelError("ClientID", "Selected client is invalid");
                    }
                }

                // Validate trainer exists
                if (appointment.TrainerID > 0)
                {
                    var trainerExists = await _context.TrainerProfiles
                        .AsNoTracking()
                        .AnyAsync(t => t.TrainerProfileID == appointment.TrainerID);
                    
                    if (!trainerExists)
                    {
                        ModelState.AddModelError("TrainerID", "Selected trainer is invalid");
                    }
                }

                if (appointment.StartTime >= appointment.EndTime)
                {
                    ModelState.AddModelError("", "Start time must be before end time");
                }

                if (!ModelState.IsValid)
                {
                    var clients = await GetClientsAsync();
                    var trainers = await GetTrainersAsync();
                    ViewBag.Clients = clients;
                    ViewBag.Trainers = trainers;
                    return View(appointment);
                }

                existing.ClientID = appointment.ClientID;
                existing.TrainerID = appointment.TrainerID;
                existing.StartTime = appointment.StartTime;
                existing.EndTime = appointment.EndTime;
                existing.Status = appointment.Status;
                existing.Reason = appointment.Reason;
                existing.TrainerNotes = appointment.TrainerNotes;

                _context.Appointments.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Appointment updated: ID {id}");
                await LogAudit($"Updated appointment ID {id}", "AppointmentEdit", "Appointment");
                TempData["SuccessMessage"] = "Appointment updated successfully.";
                return RedirectToAction("Appointments");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating appointment");
                var innerEx = dbEx.InnerException;
                var errorMessage = "Error updating appointment: ";
                
                if (innerEx != null)
                {
                    _logger.LogError($"Inner exception: {innerEx.Message}");
                    errorMessage += ExtractSqlErrorMessage(innerEx.Message);
                }
                else
                {
                    errorMessage += "Failed to save appointment to database.";
                }

                var clients = await GetClientsAsync();
                var trainers = await GetTrainersAsync();
                ViewBag.Clients = clients;
                ViewBag.Trainers = trainers;
                ModelState.AddModelError("", errorMessage);
                return View(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating appointment");
                var clients = await GetClientsAsync();
                var trainers = await GetTrainersAsync();
                ViewBag.Clients = clients;
                ViewBag.Trainers = trainers;
                ModelState.AddModelError("", "An unexpected error occurred while updating the appointment. Please try again.");
                return View(appointment);
            }
        }

        [HttpPost("appointments/approve/{id}")]
        public async Task<IActionResult> ApproveAppointment(int id)
        {
            try
            {
                var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == id);
                
                if (appointment == null)
                    return NotFound();

                appointment.Status = "Approved";

                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();

                await LogAudit($"Approved appointment ID {id}", "AppointmentApproved", "Appointment");
                TempData["SuccessMessage"] = "Appointment approved successfully.";
                return RedirectToAction("Appointments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving appointment");
                TempData["ErrorMessage"] = "Error approving appointment: " + ex.Message;
                return RedirectToAction("Appointments");
            }
        }

        [HttpPost("appointments/reject/{id}")]
        public async Task<IActionResult> RejectAppointment(int id, string? rejectionReason)
        {
            try
            {
                var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == id);
                
                if (appointment == null)
                    return NotFound();

                appointment.Status = "Rejected";
                appointment.Reason = rejectionReason;

                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync();

                await LogAudit($"Rejected appointment ID {id}", "AppointmentRejected", rejectionReason ?? "No reason provided");
                TempData["SuccessMessage"] = "Appointment rejected successfully.";
                return RedirectToAction("Appointments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting appointment");
                TempData["ErrorMessage"] = "Error rejecting appointment: " + ex.Message;
                return RedirectToAction("Appointments");
            }
        }

        #endregion

        #region Backup & Restore

        [HttpGet("backup")]
        public async Task<IActionResult> Backup()
        {
            try
            {
                var backupLogs = await _context.BackupLogs
                    .OrderByDescending(b => b.BackupDate)
                    .ToListAsync();

                // Get current backup settings from SystemSettings using the service
                var autoBackupEnabled = await _settingsService.GetMaintenanceModeAsync(); // Temporary use
                var backupTimeStr = await _settingsService.GetSettingAsync("BackupTime");
                var backupFrequency = await _settingsService.GetSettingAsync("BackupFrequency") ?? "Daily";
                var backupDayOfWeekStr = await _settingsService.GetSettingAsync("BackupDayOfWeek");

                // Parse backup time
                var backupTime = "02:00"; // Default
                if (!string.IsNullOrEmpty(backupTimeStr))
                {
                    // If stored as TimeSpan string like "02:00:00"
                    if (TimeSpan.TryParse(backupTimeStr, out var ts))
                    {
                        backupTime = ts.ToString(@"hh\:mm");
                    }
                    else
                    {
                        backupTime = backupTimeStr;
                    }
                }

                // Get auto-backup enabled setting
                var autoBackupEnabledStr = await _settingsService.GetSettingAsync("AutoBackupEnabled");
                autoBackupEnabled = autoBackupEnabledStr == "true" || autoBackupEnabledStr == "1";

                ViewBag.AutoBackupEnabled = autoBackupEnabled;
                ViewBag.BackupTime = backupTime;
                ViewBag.BackupFrequency = backupFrequency;
                ViewBag.BackupDayOfWeek = backupDayOfWeekStr ?? "";

                return View(backupLogs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading backup page");
                TempData["ErrorMessage"] = "Error loading backup settings: " + ex.Message;
                return View(new List<BackupLog>());
            }
        }

        [HttpPost("backup/create")]
        public async Task<IActionResult> CreateBackup()
        {
            try
            {
                var backupFileName = $"HealthTrack_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var backupPath = Path.Combine(AppContext.BaseDirectory, "Backups", backupFileName);

                // Ensure backup directory exists
                var backupDirectory = Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                // Create SQL backup command
                string backupCommand = $@"
                    BACKUP DATABASE [HealthTrackDB] 
                    TO DISK = '{backupPath}'
                    WITH FORMAT, 
                    MEDIANAME = 'HealthTrackBackup',
                    NAME = '{backupFileName}';
                ";

                // Execute backup (you'll need to implement this using SqlCommand)
                await ExecuteSqlBackupCommand(backupCommand);

                var backupLog = new BackupLog
                {
                    BackupFileName = backupFileName,
                    BackupPath = backupPath,
                    BackupDate = DateTime.UtcNow,
                    BackupType = "Manual",
                    Status = "Success",
                    Size = new FileInfo(backupPath).Length
                };

                _context.BackupLogs.Add(backupLog);
                await _context.SaveChangesAsync();

                await LogAudit($"Created manual backup: {backupFileName}", "BackupCreate", backupFileName);
                TempData["SuccessMessage"] = $"Backup created successfully: {backupFileName}";
                return RedirectToAction("Backup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating backup");
                TempData["ErrorMessage"] = "Error creating backup: " + ex.Message;
                return RedirectToAction("Backup");
            }
        }

        [HttpPost("backup/restore/{id}")]
        public async Task<IActionResult> RestoreBackup(int id)
        {
            try
            {
                var backup = await _context.BackupLogs.FindAsync(id);
                if (backup == null || !System.IO.File.Exists(backup.BackupPath))
                    return NotFound();

                string restoreCommand = $@"
                    ALTER DATABASE [HealthTrackDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    RESTORE DATABASE [HealthTrackDB] 
                    FROM DISK = '{backup.BackupPath}' 
                    WITH REPLACE;
                    ALTER DATABASE [HealthTrackDB] SET MULTI_USER;
                ";

                // Execute restore (you'll need to implement this using SqlCommand)
                await ExecuteSqlRestoreCommand(restoreCommand);

                backup.RestoredDate = DateTime.UtcNow;
                _context.BackupLogs.Update(backup);
                await _context.SaveChangesAsync();

                await LogAudit($"Restored backup: {backup.BackupFileName}", "BackupRestore", backup.BackupFileName);
                TempData["SuccessMessage"] = "Database restored successfully from backup.";
                return RedirectToAction("Backup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring backup");
                TempData["ErrorMessage"] = "Error restoring backup: " + ex.Message;
                return RedirectToAction("Backup");
            }
        }

        /// <summary>
        /// Feature A: Save dynamic backup schedule settings (auto-backup enable, time, frequency, retention)
        /// </summary>
        [HttpPost("backup/settings")]
        public async Task<IActionResult> SaveBackupSettings(
            bool autoBackupEnabled,
            string? backupTime,
            string? backupFrequency,
            int? backupDayOfWeek)
        {
            try
            {
                // Save AutoBackupEnabled setting
                await _settingsService.SetSettingAsync("AutoBackupEnabled", autoBackupEnabled ? "true" : "false", "Enable/Disable automatic backups");

                // Save BackupTime setting
                if (!string.IsNullOrEmpty(backupTime))
                {
                    await _settingsService.SetSettingAsync("BackupTime", backupTime, "Time to run automatic backups (HH:mm format)");
                }

                // Save BackupFrequency setting
                await _settingsService.SetSettingAsync("BackupFrequency", backupFrequency ?? "Daily", "Frequency of automatic backups (Daily, Weekly, Monthly)");

                // Save BackupDayOfWeek setting (for weekly backups)
                if (backupDayOfWeek.HasValue)
                {
                    await _settingsService.SetSettingAsync("BackupDayOfWeek", backupDayOfWeek.ToString(), "Day of week for weekly backups (0=Sunday, 6=Saturday)");
                }
                else
                {
                    await _settingsService.SetSettingAsync("BackupDayOfWeek", "", "Day of week for weekly backups");
                }

                await LogAudit(
                    $"Updated backup schedule: Enabled={autoBackupEnabled}, Time={backupTime}, Frequency={backupFrequency}, DayOfWeek={backupDayOfWeek}",
                    "BackupSettingsSave",
                    "Backup Settings"
                );

                TempData["SuccessMessage"] = "Backup schedule settings saved successfully. Auto-Backup is now " + (autoBackupEnabled ? "ENABLED" : "DISABLED") + ".";
                return RedirectToAction("Backup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving backup settings");
                TempData["ErrorMessage"] = "Error saving backup settings: " + ex.Message;
                return RedirectToAction("Backup");
            }
        }

        #endregion

        #region Private Methods

        private async Task LogAudit(string details, string action, string performedBy)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Action = action,
                    Details = details,
                    CreatedAt = DateTime.UtcNow,
                    IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserID = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : (int?)null
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit");
            }
        }

        private string ExtractSqlErrorMessage(string exceptionMessage)
        {
            if (string.IsNullOrEmpty(exceptionMessage))
                return "Database error occurred.";

            // Extract key SQL error patterns
            if (exceptionMessage.Contains("FOREIGN KEY constraint"))
                return "The selected client or trainer is invalid.";
            
            if (exceptionMessage.Contains("NOT NULL constraint") || exceptionMessage.Contains("Cannot insert the value NULL"))
                return "Required information is missing. Please check your selections.";
            
            if (exceptionMessage.Contains("Invalid column name"))
                return "Database schema error. Please contact administrator.";
            
            if (exceptionMessage.Contains("Arithmetic overflow"))
                return "Invalid time values provided.";

            // For other errors, return a generic message
            return "Database error occurred. Please contact administrator if problem persists.";
        }

        private async Task ExecuteSqlBackupCommand(string command)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var sqlCommand = new SqlCommand(command, connection))
                    {
                        sqlCommand.CommandTimeout = 600; // 10 minutes
                        await sqlCommand.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing backup command");
                throw;
            }
        }

        private async Task ExecuteSqlRestoreCommand(string command)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var sqlCommand = new SqlCommand(command, connection))
                    {
                        sqlCommand.CommandTimeout = 600; // 10 minutes
                        await sqlCommand.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing restore command");
                throw;
            }
        }

        /// <summary>
        /// Download a backup file
        /// </summary>
        [HttpGet("backup/download/{id}")]
        public async Task<IActionResult> DownloadBackup(int id)
        {
            try
            {
                var backup = await _context.BackupLogs.FindAsync(id);
                if (backup == null || string.IsNullOrEmpty(backup.BackupPath))
                {
                    return NotFound("Backup not found");
                }

                if (!System.IO.File.Exists(backup.BackupPath))
                {
                    _logger.LogWarning($"Backup file not found: {backup.BackupPath}");
                    return NotFound("Backup file not found on server");
                }

                // Read the file
                var fileBytes = await System.IO.File.ReadAllBytesAsync(backup.BackupPath);
                
                // Log the download
                await LogAudit($"Downloaded backup: {backup.BackupFileName}", "BackupDownload", backup.BackupFileName);

                // Return file as download
                return File(fileBytes, "application/octet-stream", backup.BackupFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading backup");
                TempData["ErrorMessage"] = "Error downloading backup: " + ex.Message;
                return RedirectToAction("Backup");
            }
        }

        #endregion
    
        #region Diagnostics

        [HttpGet("diagnostic/data-check")]
        public async Task<IActionResult> DataCheck()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalRoles = await _context.Roles.CountAsync();
                var totalClients = await _context.ClientProfiles.CountAsync();
                var totalTrainers = await _context.TrainerProfiles.CountAsync();
                var totalAppointments = await _context.Appointments.CountAsync();

                var clients = await _context.ClientProfiles
                    .Include(c => c.User)
                    .ToListAsync();

                var trainers = await _context.TrainerProfiles
                    .Include(t => t.User)
                    .ToListAsync();

                var diagnostics = new
                {
                    timestamp = DateTime.UtcNow,
                    totalUsers,
                    totalRoles,
                    totalClients,
                    totalTrainers,
                    totalAppointments,
                    clientsWithUser = clients.Count(c => c.User != null),
                    clientsWithoutUser = clients.Count(c => c.User == null),
                    trainersWithUser = trainers.Count(t => t.User != null),
                    trainersWithoutUser = trainers.Count(t => t.User == null),
                    clients = clients.Select(c => new
                    {
                        c.ClientProfileID,
                        c.UserID,
                        userName = c.User?.Email ?? "NULL",
                        userFullName = c.User != null ? $"{c.User.FirstName} {c.User.LastName}".Trim() : "NO USER"
                    }).ToList(),
                    trainers = trainers.Select(t => new
                    {
                        t.TrainerProfileID,
                        t.UserID,
                        userName = t.User?.Email ?? "NULL",
                        userFullName = t.User != null ? $"{t.User.FirstName} {t.User.LastName}".Trim() : "NO USER"
                    }).ToList()
                };

                return Json(diagnostics);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        #endregion

        #region Trainer Assignments Management

        [HttpGet("trainer-assignments")]
        public async Task<IActionResult> TrainerAssignments()
        {
            try
            {
                var trainerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Trainer");
                var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");

                if (trainerRole == null || clientRole == null)
                    return BadRequest("Trainer or Client role not found");

                // Get active trainers and clients
                var trainers = await _context.TrainerProfiles
                    .Include(t => t.User)
                    .Where(t => t.User != null && t.User.IsActive && t.User.RoleID == trainerRole.RoleID)
                    .ToListAsync();

                var clients = await _context.ClientProfiles
                    .Include(c => c.User)
                    .Where(c => c.User != null && c.User.IsActive && c.User.RoleID == clientRole.RoleID)
                    .ToListAsync();

                // Get current assignments (for now, this is simulated - you'd need a TrainerClientAssignment table)
                var assignments = new List<TrainerAssignmentViewModel>();

                var viewModel = new ManageAssignmentsViewModel
                {
                    AvailableTrainers = trainers,
                    AvailableClients = clients,
                    CurrentAssignments = assignments,
                    TotalActiveAssignments = assignments.Count
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading trainer assignments");
                TempData["ErrorMessage"] = "Error loading trainer assignments: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost("trainer-assignments/assign")]
        public async Task<IActionResult> AssignTrainer(int trainerId, int clientId)
        {
            try
            {
                // Validate trainer exists
                var trainer = await _context.TrainerProfiles.FindAsync(trainerId);
                if (trainer == null)
                    return BadRequest("Trainer not found");

                // Validate client exists
                var client = await _context.ClientProfiles.FindAsync(clientId);
                if (client == null)
                    return BadRequest("Client not found");

                // TODO: Create assignment record in database
                // For now, we'll just log this action
                await LogAudit($"Assigned trainer {trainerId} to client {clientId}", "TrainerAssignment", $"Trainer:{trainerId},Client:{clientId}");

                TempData["SuccessMessage"] = "Trainer assigned to client successfully.";
                return RedirectToAction("TrainerAssignments");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning trainer");
                TempData["ErrorMessage"] = "Error assigning trainer: " + ex.Message;
                return RedirectToAction("TrainerAssignments");
            }
        }

        #endregion

        #region Reports and Analytics

        [HttpGet("reports")]
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate, int page = 1)
        {
            try
            {
                var actualStartDate = startDate ?? DateTime.UtcNow.AddMonths(-1);
                var actualEndDate = endDate ?? DateTime.UtcNow;

                var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");
                var trainerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Trainer");

                // User registrations
                var newUsers = await _context.Users
                    .Where(u => u.CreatedAt >= actualStartDate && u.CreatedAt <= actualEndDate)
                    .ToListAsync();

                var newClients = newUsers.Where(u => u.RoleID == clientRole?.RoleID).ToList();
                var newTrainers = newUsers.Where(u => u.RoleID == trainerRole?.RoleID).ToList();

                // User registration details
                var userRegDetails = newUsers.Select(u => new UserRegistrationRecord
                {
                    UserId = u.UserID,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role?.RoleName ?? "Unknown",
                    CreatedAt = u.CreatedAt,
                    IsActive = u.IsActive
                }).ToList();

                // Appointment statistics
                var appointmentsThisPeriod = await _context.Appointments
                    .Where(a => a.CreatedAt >= actualStartDate && a.CreatedAt <= actualEndDate)
                    .Include(a => a.Client)
                    .Include(a => a.Trainer)
                    .ToListAsync();

                var completedAppointments = appointmentsThisPeriod.Count(a => a.Status == "Completed");
                var cancelledAppointments = appointmentsThisPeriod.Count(a => a.Status == "Cancelled");
                var pendingAppointments = appointmentsThisPeriod.Count(a => a.Status == "Pending");
                var approvedAppointments = appointmentsThisPeriod.Count(a => a.Status == "Approved");

                var appointmentDetails = appointmentsThisPeriod.Select(a => new AppointmentDetailRecord
                {
                    AppointmentId = a.AppointmentID,
                    ClientName = a.Client?.User != null ? $"{a.Client.User.FirstName} {a.Client.User.LastName}".Trim() : "Unknown",
                    TrainerName = a.Trainer?.User != null ? $"{a.Trainer.User.FirstName} {a.Trainer.User.LastName}".Trim() : "Unknown",
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Status = a.Status,
                    Reason = a.Reason
                }).ToList();

                // Client and trainer counts
                var allClients = await _context.ClientProfiles
                    .Include(c => c.User)
                    .Where(c => c.User != null)
                    .ToListAsync();

                var allTrainers = await _context.TrainerProfiles
                    .Include(t => t.User)
                    .Where(t => t.User != null)
                    .ToListAsync();

                var viewModel = new ReportsAndAnalyticsViewModel
                {
                    StartDate = actualStartDate,
                    EndDate = actualEndDate,
                    NewRegistrationsThisPeriod = newUsers.Count,
                    NewClientsThisPeriod = newClients.Count,
                    NewTrainersThisPeriod = newTrainers.Count,
                    TotalActiveClients = allClients.Count(c => c.User?.IsActive ?? false),
                    TotalInactiveClients = allClients.Count(c => !(c.User?.IsActive ?? false)),
                    TotalActiveTrainers = allTrainers.Count(t => t.User?.IsActive ?? false),
                    TotalInactiveTrainers = allTrainers.Count(t => !(t.User?.IsActive ?? false)),
                    TotalAppointmentsThisPeriod = appointmentsThisPeriod.Count,
                    CompletedAppointmentsThisPeriod = completedAppointments,
                    CancelledAppointmentsThisPeriod = cancelledAppointments,
                    PendingAppointmentsThisPeriod = pendingAppointments,
                    ApprovedAppointmentsThisPeriod = approvedAppointments,
                    CompletionRate = appointmentsThisPeriod.Count > 0 ? (completedAppointments * 100.0 / appointmentsThisPeriod.Count) : 0,
                    CancellationRate = appointmentsThisPeriod.Count > 0 ? (cancelledAppointments * 100.0 / appointmentsThisPeriod.Count) : 0,
                    UserRegistrationDetails = userRegDetails,
                    AppointmentDetails = appointmentDetails,
                    CurrentPage = page,
                    TotalRecords = appointmentDetails.Count
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating reports");
                TempData["ErrorMessage"] = "Error generating reports: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpGet("reports/export-csv")]
        public async Task<IActionResult> ExportReportsCsv(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var actualStartDate = startDate ?? DateTime.UtcNow.AddMonths(-1);
                var actualEndDate = endDate ?? DateTime.UtcNow;

                var appointmentsThisPeriod = await _context.Appointments
                    .Where(a => a.CreatedAt >= actualStartDate && a.CreatedAt <= actualEndDate)
                    .Include(a => a.Client)
                    .Include(a => a.Trainer)
                    .ToListAsync();

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Appointment ID,Client Name,Trainer Name,Start Time,End Time,Status,Reason");

                foreach (var apt in appointmentsThisPeriod)
                {
                    var clientName = apt.Client?.User != null ? $"{apt.Client.User.FirstName} {apt.Client.User.LastName}".Trim() : "Unknown";
                    var trainerName = apt.Trainer?.User != null ? $"{apt.Trainer.User.FirstName} {apt.Trainer.User.LastName}".Trim() : "Unknown";
                    csv.AppendLine($"{apt.AppointmentID},\"{clientName}\",\"{trainerName}\",{apt.StartTime:yyyy-MM-dd HH:mm},{apt.EndTime:yyyy-MM-dd HH:mm},{apt.Status},\"{apt.Reason ?? ""}\"");
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"appointments_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting reports");
                TempData["ErrorMessage"] = "Error exporting reports: " + ex.Message;
                return RedirectToAction("Reports");
            }
        }

        [HttpGet("reports/export")]
        public async Task<IActionResult> ExportReport(string? reportType, string? format, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var actualStartDate = startDate ?? DateTime.UtcNow.AddMonths(-1);
                var actualEndDate = endDate ?? DateTime.UtcNow;
                var exportFormat = format ?? "csv";
                var type = reportType ?? "appointments";

                // Generate appropriate report based on type
                return exportFormat switch
                {
                    "csv" => await ExportReportAsCSV(type, actualStartDate, actualEndDate),
                    "excel" => await ExportReportAsExcel(type, actualStartDate, actualEndDate),
                    "pdf" => await ExportReportAsPDF(type, actualStartDate, actualEndDate),
                    _ => await ExportReportAsCSV(type, actualStartDate, actualEndDate)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                TempData["ErrorMessage"] = "Error exporting report: " + ex.Message;
                return RedirectToAction("Reports");
            }
        }

        private async Task<IActionResult> ExportReportAsCSV(string reportType, DateTime startDate, DateTime endDate)
        {
            var csv = new System.Text.StringBuilder();

            return reportType switch
            {
                "analytics" => await ExportAnalyticsAsCSV(startDate, endDate),
                "users-report" => await ExportUsersAsCSV(startDate, endDate),
                "appointments-report" => await ExportAppointmentsAsCSV(startDate, endDate),
                "trainers-report" => await ExportTrainersAsCSV(startDate, endDate),
                "workouts-report" => ExportWorkoutsAsCSV(),
                "diet-report" => ExportDietAsCSV(),
                "progress-report" => ExportProgressAsCSV(),
                _ => await ExportAppointmentsAsCSV(startDate, endDate)
            };
        }

        private async Task<IActionResult> ExportAnalyticsAsCSV(DateTime startDate, DateTime endDate)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Analytics Report");
            csv.AppendLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            csv.AppendLine();
            csv.AppendLine("Metric,Value");

            var users = await _context.Users.ToListAsync();
            var appointments = await _context.Appointments
                .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .ToListAsync();

            csv.AppendLine($"Total Users,{users.Count}");
            csv.AppendLine($"Active Users,{users.Count(u => u.IsActive)}");
            csv.AppendLine($"Total Appointments,{appointments.Count}");
            csv.AppendLine($"Completed Appointments,{appointments.Count(a => a.Status == "Completed")}");
            csv.AppendLine($"Pending Appointments,{appointments.Count(a => a.Status == "Pending")}");
            csv.AppendLine($"Cancelled Appointments,{appointments.Count(a => a.Status == "Cancelled")}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"analytics_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private async Task<IActionResult> ExportUsersAsCSV(DateTime startDate, DateTime endDate)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("User ID,Email,First Name,Last Name,Role,Status,Created Date");

            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
                .ToListAsync();

            foreach (var user in users)
            {
                csv.AppendLine($"{user.UserID},\"{user.Email}\",\"{user.FirstName}\",\"{user.LastName}\",\"{user.Role?.RoleName ?? "N/A"}\",\"{(user.IsActive ? "Active" : "Inactive")}\",{user.CreatedAt:yyyy-MM-dd HH:mm}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"users_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private async Task<IActionResult> ExportAppointmentsAsCSV(DateTime startDate, DateTime endDate)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Appointment ID,Client,Trainer,Start Time,End Time,Duration (min),Status,Reason");

            var appointments = await _context.Appointments
                .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                .Include(a => a.Client).ThenInclude(c => c.User)
                .Include(a => a.Trainer).ThenInclude(t => t.User)
                .ToListAsync();

            foreach (var apt in appointments)
            {
                var clientName = apt.Client?.User != null ? $"{apt.Client.User.FirstName} {apt.Client.User.LastName}".Trim() : "Unknown";
                var trainerName = apt.Trainer?.User != null ? $"{apt.Trainer.User.FirstName} {apt.Trainer.User.LastName}".Trim() : "Unknown";
                var duration = (apt.EndTime - apt.StartTime).TotalMinutes;
                csv.AppendLine($"{apt.AppointmentID},\"{clientName}\",\"{trainerName}\",{apt.StartTime:yyyy-MM-dd HH:mm},{apt.EndTime:yyyy-MM-dd HH:mm},{duration:F0},{apt.Status},\"{apt.Reason ?? ""}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"appointments_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private async Task<IActionResult> ExportTrainersAsCSV(DateTime startDate, DateTime endDate)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Trainer ID,Name,Email,Total Clients,Total Appointments,Completed Appointments,Specialization");

            var trainers = await _context.TrainerProfiles
                .Include(t => t.User)
                .ToListAsync();

            foreach (var trainer in trainers)
            {
                var appointments = await _context.Appointments
                    .Where(a => a.TrainerID == trainer.TrainerProfileID && a.CreatedAt >= startDate && a.CreatedAt <= endDate)
                    .ToListAsync();

                var completedCount = appointments.Count(a => a.Status == "Completed");
                csv.AppendLine($"{trainer.TrainerProfileID},\"{trainer.User?.FirstName} {trainer.User?.LastName}\",\"{trainer.User?.Email}\",0,{appointments.Count},{completedCount},\"{trainer.Specialization ?? ""}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"trainers_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private IActionResult ExportWorkoutsAsCSV()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Workout Plans Report");
            csv.AppendLine("No workout data available - Feature integration pending");
            csv.AppendLine();
            csv.AppendLine("Client,Plan Name,Start Date,End Date,Status");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"workouts_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private IActionResult ExportDietAsCSV()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Diet Plans Report");
            csv.AppendLine("No diet data available - Feature integration pending");
            csv.AppendLine();
            csv.AppendLine("Client,Plan Name,Calories Target,Meals Per Day,Status");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"diet_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private IActionResult ExportProgressAsCSV()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Progress & Health Metrics Report");
            csv.AppendLine("No progress data available - Feature integration pending");
            csv.AppendLine();
            csv.AppendLine("Client,Goal,Target,Current,Progress %,Status");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"progress_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        private async Task<IActionResult> ExportReportAsExcel(string reportType, DateTime startDate, DateTime endDate)
        {
            // For Excel, we'll use CSV for now with .xlsx extension
            // Full Excel export would require a library like EPPlus or ClosedXML
            return await ExportReportAsCSV(reportType, startDate, endDate);
        }

        private async Task<IActionResult> ExportReportAsPDF(string reportType, DateTime startDate, DateTime endDate)
        {
            // PDF export would require a library like iTextSharp or SelectPdf
            // For now, return CSV as fallback
            return await ExportReportAsCSV(reportType, startDate, endDate);
        }

        #endregion

        #region Activation and Security Management

        [HttpGet("activation-security")]
        public async Task<IActionResult> ActivationSecurity(string? selectedStatus, int page = 1)
        {
            try
            {
                var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");
                var trainerRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Trainer");

                // Get all users
                var allUsers = await _context.Users
                    .Include(u => u.Role)
                    .ToListAsync();

                // Pending activations - join with users to get user info
                var pendingTokens = await _context.ActivationTokens
                    .Where(t => !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                var expiredTokens = await _context.ActivationTokens
                    .Where(t => !t.IsUsed && t.ExpiresAt <= DateTime.UtcNow)
                    .ToListAsync();

                var allPendingTokens = pendingTokens.Concat(expiredTokens).ToList();

                var pendingActivations = new List<PendingActivationRecord>();
                foreach (var t in allPendingTokens)
                {
                    var user = allUsers.FirstOrDefault(u => u.UserID == t.UserID);
                    if (user != null)
                    {
                        pendingActivations.Add(new PendingActivationRecord
                        {
                            UserId = user.UserID,
                            Email = user.Email,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            Role = user.Role?.RoleName ?? "Unknown",
                            RegistrationDate = user.CreatedAt,
                            TokenExpiryDate = t.ExpiresAt,
                            IsExpired = t.ExpiresAt <= DateTime.UtcNow,
                            ActivationTokenId = t.ActivationTokenID
                        });
                    }
                }

                // Account status
                var accountStatus = allUsers.Select(u => new AccountStatusRecord
                {
                    UserId = u.UserID,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role?.RoleName ?? "Unknown",
                    IsActive = u.IsActive,
                    IsLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTime.UtcNow,
                    LockoutEnd = u.LockoutEnd,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt
                }).ToList();

                // Failed login attempts
                var failedLogins = allUsers
                    .Where(u => u.FailedLoginCount > 0)
                    .Select(u => new FailedLoginRecord
                    {
                        UserId = u.UserID,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        FailedAttempts = u.FailedLoginCount,
                        LastFailedAttempt = u.LastLoginAt ?? DateTime.UtcNow,
                        IsCurrentlyLocked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTime.UtcNow,
                        LockoutEnd = u.LockoutEnd
                    })
                    .ToList();

                // Security audit events
                var auditLogs = await _context.AuditLogs
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(100)
                    .ToListAsync();

                var securityEvents = auditLogs.Select(a => new SecurityAuditRecord
                {
                    AuditId = a.AuditID,
                    Action = a.Action,
                    Details = a.Details,
                    CreatedAt = a.CreatedAt,
                    PerformedBy = a.UserID.HasValue ? allUsers.FirstOrDefault(u => u.UserID == a.UserID)?.Email ?? "System" : "System",
                    IPAddress = a.IPAddress,
                    Severity = DetermineEventSeverity(a.Action)
                }).ToList();

                var viewModel = new ActivationSecurityViewModel
                {
                    PendingActivations = pendingActivations,
                    TotalPendingActivations = pendingTokens.Count,
                    TotalExpiredTokens = expiredTokens.Count,
                    AccountStatus = accountStatus,
                    TotalActiveAccounts = accountStatus.Count(a => a.IsActive && !a.IsLocked),
                    TotalInactiveAccounts = accountStatus.Count(a => !a.IsActive),
                    TotalLockedAccounts = accountStatus.Count(a => a.IsLocked),
                    FailedLoginAttempts = failedLogins,
                    TotalFailedAttemptsThisPeriod = failedLogins.Sum(f => f.FailedAttempts),
                    SecurityAuditEvents = securityEvents,
                    CurrentPage = page
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading activation and security data");
                TempData["ErrorMessage"] = "Error loading activation and security data: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost("activation-security/renew-token/{tokenId}")]
        public async Task<IActionResult> RenewActivationToken(int tokenId)
        {
            try
            {
                var token = await _context.ActivationTokens.FindAsync(tokenId);
                if (token == null)
                    return NotFound();

                // Renew the token for another 7 days
                token.ExpiresAt = DateTime.UtcNow.AddDays(7);
                _context.ActivationTokens.Update(token);
                await _context.SaveChangesAsync();

                await LogAudit($"Renewed activation token for user {token.UserID}", "TokenRenewal", $"TokenID:{tokenId}");
                TempData["SuccessMessage"] = "Activation token renewed successfully.";
                return RedirectToAction("ActivationSecurity");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing activation token");
                TempData["ErrorMessage"] = "Error renewing activation token: " + ex.Message;
                return RedirectToAction("ActivationSecurity");
            }
        }

        [HttpPost("activation-security/unlock-account/{userId}")]
        public async Task<IActionResult> UnlockAccount(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound();

                user.LockoutEnd = null;
                user.FailedLoginCount = 0;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await LogAudit($"Unlocked account for user {userId}", "AccountUnlock", user.Email);
                TempData["SuccessMessage"] = "Account unlocked successfully.";
                return RedirectToAction("ActivationSecurity");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking account");
                TempData["ErrorMessage"] = "Error unlocking account: " + ex.Message;
                return RedirectToAction("ActivationSecurity");
            }
        }

        [HttpPost("activation-security/toggle-account-status/{userId}")]
        public async Task<IActionResult> ToggleAccountStatus(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound();

                user.IsActive = !user.IsActive;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var action = user.IsActive ? "activated" : "deactivated";
                await LogAudit($"{char.ToUpper(action[0])}{action.Substring(1)} user account {userId}", "AccountStatusChange", user.Email);
                TempData["SuccessMessage"] = $"Account {action} successfully.";
                return RedirectToAction("ActivationSecurity");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling account status");
                TempData["ErrorMessage"] = "Error toggling account status: " + ex.Message;
                return RedirectToAction("ActivationSecurity");
            }
        }

        private string DetermineEventSeverity(string action)
        {
            return action switch
            {
                "UserCreate" => "Info",
                "UserUpdate" => "Info",
                "UserDelete" => "Warning",
                "AccountUnlock" => "Warning",
                "FailedLogin" => "Warning",
                "PermissionChange" => "Critical",
                "RoleChange" => "Critical",
                "SettingsEdit" => "Warning",
                _ => "Info"
            };
        }

        #endregion
    }
}
