using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "Trainer")]
    [Route("trainer")]
    public class TrainerController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<TrainerController> _logger;

        public TrainerController(HealthTrackContext context, ILogger<TrainerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Get current authenticated trainer
        private async Task<TrainerProfile?> GetCurrentTrainerAsync()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            return await _context.TrainerProfiles.FirstOrDefaultAsync(t => t.UserID == userId);
        }

        #region Dashboard

        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var trainer = await GetCurrentTrainerAsync();
                if (trainer == null)
                    return Unauthorized();

                var viewModel = new TrainerDashboardViewModel();

                // Get assigned clients
                viewModel.AssignedClientsCount = await _context.ClientProfiles.CountAsync();

                // Get upcoming appointments
                viewModel.UpcomingAppointments = await _context.Appointments
                    .Include(a => a.Client).ThenInclude(c => c!.User)
                    .Where(a => a.TrainerID == trainer.TrainerProfileID && 
                           a.StartTime > DateTime.UtcNow && 
                           a.Status != "Cancelled")
                    .OrderBy(a => a.StartTime)
                    .Take(5)
                    .ToListAsync();

                // Get recent activity summary
                var recentAppointments = await _context.Appointments
                    .Where(a => a.TrainerID == trainer.TrainerProfileID)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                viewModel.TotalAppointments = await _context.Appointments
                    .CountAsync(a => a.TrainerID == trainer.TrainerProfileID);

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading trainer dashboard");
                return View(new TrainerDashboardViewModel { ErrorMessage = ex.Message });
            }
        }

        #endregion

        #region My Clients

        [HttpGet("clients")]
        public async Task<IActionResult> Clients(int page = 1)
        {
            try
            {
                var trainer = await GetCurrentTrainerAsync();
                if (trainer == null)
                    return Unauthorized();

                const int pageSize = 10;
                var clients = await _context.ClientProfiles
                    .Include(c => c.User)
                    .OrderBy(c => c.User!.FirstName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalClients = await _context.ClientProfiles.CountAsync();
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalClients / pageSize);
                ViewBag.CurrentPage = page;

                return View(clients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading clients");
                ViewBag.ErrorMessage = ex.Message;
                return View(new List<ClientProfile>());
            }
        }

        [HttpGet("clients/{id}")]
        public async Task<IActionResult> ClientDetails(int id)
        {
            try
            {
                var client = await _context.ClientProfiles
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.ClientProfileID == id);

                if (client == null)
                    return NotFound();

                var viewModel = new ClientDetailViewModel
                {
                    ClientProfile = client
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading client details");
                return BadRequest(ex.Message);
            }
        }

        #endregion

        #region Appointments

        [HttpGet("appointments")]
        public async Task<IActionResult> Appointments(int page = 1)
        {
            try
            {
                var trainer = await GetCurrentTrainerAsync();
                if (trainer == null)
                    return Unauthorized();

                const int pageSize = 10;
                var appointments = await _context.Appointments
                    .Include(a => a.Client).ThenInclude(c => c!.User)
                    .Where(a => a.TrainerID == trainer.TrainerProfileID)
                    .OrderByDescending(a => a.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalAppointments = await _context.Appointments
                    .CountAsync(a => a.TrainerID == trainer.TrainerProfileID);

                ViewBag.TotalPages = (int)Math.Ceiling((double)totalAppointments / pageSize);
                ViewBag.CurrentPage = page;

                return View(appointments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading appointments");
                return View(new List<Appointment>());
            }
        }

        #endregion

        #region Workout Plans

        [HttpGet("workouts")]
        public IActionResult WorkoutPlans()
        {
            return View();
        }

        #endregion

        #region Diet Plans

        [HttpGet("diets")]
        public IActionResult DietPlans()
        {
            return View();
        }

        #endregion

        #region Progress

        [HttpGet("progress")]
        public IActionResult ClientProgress()
        {
            return View();
        }

        #endregion

        #region Profile Management

        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
                var user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (user == null)
                    return NotFound();

                var trainerProfile = await _context.TrainerProfiles
                    .FirstOrDefaultAsync(t => t.UserID == userId);

                var viewModel = new TrainerProfileViewModel
                {
                    User = user,
                    TrainerProfile = trainerProfile
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading trainer profile");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("profile/edit")]
        public async Task<IActionResult> EditProfile(string firstName, string lastName, string? phoneNumber, string? specialization)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return NotFound();

                user.FirstName = firstName;
                user.LastName = lastName;

                var trainerProfile = await _context.TrainerProfiles.FirstOrDefaultAsync(t => t.UserID == userId);
                if (trainerProfile != null)
                {
                    trainerProfile.PhoneNumber = phoneNumber;
                    trainerProfile.Specialization = specialization;
                    _context.TrainerProfiles.Update(trainerProfile);
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile");
                TempData["ErrorMessage"] = "Error updating profile: " + ex.Message;
                return RedirectToAction("Profile");
            }
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(currentPassword) || 
                    string.IsNullOrWhiteSpace(newPassword) ||
                    string.IsNullOrWhiteSpace(confirmPassword))
                {
                    TempData["ErrorMessage"] = "All password fields are required.";
                    return RedirectToAction("Profile");
                }

                if (newPassword != confirmPassword)
                {
                    TempData["ErrorMessage"] = "New passwords do not match.";
                    return RedirectToAction("Profile");
                }

                if (newPassword.Length < 8)
                {
                    TempData["ErrorMessage"] = "New password must be at least 8 characters long.";
                    return RedirectToAction("Profile");
                }

                // Check password complexity (at least one letter, one number, one special character)
                bool hasLetter = newPassword.Any(c => char.IsLetter(c));
                bool hasNumber = newPassword.Any(c => char.IsDigit(c));
                bool hasSpecial = newPassword.Any(c => !char.IsLetterOrDigit(c));

                if (!hasLetter || !hasNumber || !hasSpecial)
                {
                    TempData["ErrorMessage"] = "Password must contain letters, numbers, and special characters.";
                    return RedirectToAction("Profile");
                }

                // Get current user
                var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                    return Unauthorized();

                // Verify current password
                if (!PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return RedirectToAction("Profile");
                }

                // Hash new password
                PasswordHasher.CreatePasswordHash(newPassword, out string newHash, out string newSalt);

                // Update password
                user.PasswordHash = newHash;
                user.PasswordSalt = newSalt;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password changed successfully.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                TempData["ErrorMessage"] = "Error changing password: " + ex.Message;
                return RedirectToAction("Profile");
            }
        }

        #endregion
    }
}
