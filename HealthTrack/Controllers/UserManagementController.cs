using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class UserManagementController : Controller
    {
        private readonly HealthTrackContext _db;

        public UserManagementController(HealthTrackContext db)
        {
            _db = db;
        }

        public IActionResult Index(string? q)
        {
            try
            {
                var users = _db.Users
                    .Include(u => u.Role)
                    .AsQueryable();
                
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var ql = q.ToLower();
                    users = users.Where(u => 
                        (u.Email ?? string.Empty).ToLower().Contains(ql) || 
                        (u.FirstName ?? string.Empty).ToLower().Contains(ql) || 
                        (u.LastName ?? string.Empty).ToLower().Contains(ql));
                }
                
                return View(users.ToList());
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error loading users: {ex.Message}");
                return View(new List<User>());
            }
        }

        [HttpGet]
        public IActionResult CreateClient()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateClient(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("Email", "Email is required");
                return View(model);
            }

            var emailLower = model.Email.ToLower();
            
            // Only get the ID first to avoid materializing NULL fields
            var existingUserId = _db.Users
                .AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower() == emailLower)
                .Select(u => (int?)u.UserID)
                .FirstOrDefault();

            if (existingUserId != null)
            {
                ModelState.AddModelError("Email", "Email already in use");
                return View(model);
            }

            var role = _db.Roles.FirstOrDefault(r => r.RoleName == "Client");
            if (role == null) 
            {
                ModelState.AddModelError(string.Empty, "Client role not configured");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("Password", "Password is required");
                return View(model);
            }

            PasswordHasher.CreatePasswordHash(model.Password, out var hash, out var salt);

            using var tx = _db.Database.BeginTransaction();
            try
            {
                var names = (model.FullName ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var user = new User
                {
                    FirstName = names.Length > 0 ? names[0] : string.Empty,
                    LastName = names.Length > 1 ? names[1] : string.Empty,
                    Email = model.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsActive = true,
                    RoleID = role.RoleID,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Users.Add(user);
                _db.SaveChanges();

                _db.ClientProfiles.Add(new ClientProfile { ClientProfileID = user.UserID });
                _db.SaveChanges();

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                ModelState.AddModelError(string.Empty, $"Could not create client: {ex.Message}");
                return View(model);
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult CreateTrainer()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateTrainer(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("Email", "Email is required");
                return View(model);
            }

            var emailLower = model.Email.ToLower();
            
            // Only get the ID first to avoid materializing NULL fields
            var existingUserId = _db.Users
                .AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower() == emailLower)
                .Select(u => (int?)u.UserID)
                .FirstOrDefault();

            if (existingUserId != null)
            {
                ModelState.AddModelError("Email", "Email already in use");
                return View(model);
            }

            var role = _db.Roles.FirstOrDefault(r => r.RoleName == "Trainer");
            if (role == null)
            {
                ModelState.AddModelError(string.Empty, "Trainer role not configured");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("Password", "Password is required");
                return View(model);
            }

            // Create trainer account in pending activation state
            PasswordHasher.CreatePasswordHash(model.Password, out var hash, out var salt);

            using var tx = _db.Database.BeginTransaction();
            try
            {
                var names = (model.FullName ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var user = new User
                {
                    FirstName = names.Length > 0 ? names[0] : string.Empty,
                    LastName = names.Length > 1 ? names[1] : string.Empty,
                    Email = model.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsActive = false, // pending activation
                    RoleID = role.RoleID,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Users.Add(user);
                _db.SaveChanges();

                _db.TrainerProfiles.Add(new TrainerProfile 
                { 
                    TrainerProfileID = user.UserID // Assuming TrainerID = UserID relationship
                });
                _db.SaveChanges();

                // TODO: generate activation token and send email invitation

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                ModelState.AddModelError(string.Empty, $"Could not create trainer: {ex.Message}");
                return View(model);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleActive(int id)
        {
            var user = _db.Users.Find(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            _db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
