
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HealthTrack.Models;
using HealthTrack.Data;
using HealthTrack.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HealthTrack.Controllers
{
    public class AccountController : Controller
    {
        private readonly HealthTrackContext _db;
        private readonly ILogger<AccountController> _logger;
        private readonly IPasswordResetTokenService _tokenService;
        private readonly IEmailService _emailService;

        public AccountController(
            HealthTrackContext db, 
            ILogger<AccountController> logger,
            IPasswordResetTokenService tokenService,
            IEmailService emailService)
        {
            _db = db;
            _logger = logger;
            _tokenService = tokenService;
            _emailService = emailService;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            // Find user and load the associated role.
            var email = model.EmailOrUsername?.Trim();

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError(
                    string.Empty, "Please enter your email and password.");

                return View(model);
            }

            var normalizedEmail = email.Trim();
            var normalizedEmailLower = normalizedEmail.ToLower();

            // Step 1: Only get the UserID first (avoid materializing NULL fields)
            var userIdResult = await _db.Users
                .AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower() == normalizedEmailLower)
                .Select(u => (int?)u.UserID)
                .FirstOrDefaultAsync();

            if (userIdResult == null)
            {
                ModelState.AddModelError(
                    string.Empty, "Invalid email or password.");

                return View(model);
            }

            // Step 2: Now load the full user with role relationship
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserID == userIdResult);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty, "Invalid email or password.");

                return View(model);
            }

            // Reject inactive accounts.
            if (!user.IsActive)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Account is inactive. Contact administrator.");

                return View(model);
            }

            // Account lockout check disabled - allowing all login attempts

            // Verify password - ensure hash and salt are not null
            if (user.PasswordHash == null || user.PasswordSalt == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Account is not properly configured. Contact administrator.");

                return View(model);
            }

            if (!PasswordHasher.VerifyPassword(
                model.Password,
                user.PasswordHash,
                user.PasswordSalt))
            {
                // Account lockout disabled - no longer incrementing failed login count or locking accounts
                ModelState.AddModelError(
                    string.Empty, "Invalid email or password.");

                return View(model);
            }

            // Ensure the user has a configured role.
            var roleName = user.Role?.RoleName;

            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account role is not configured. Contact administrator.");

                return View(model);
            }

            // Update login information.
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Create authentication claims.
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserID.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    $"{user.FirstName} {user.LastName}".Trim()),

                new Claim(
                    ClaimTypes.Email,
                    user.Email),

                new Claim(
                    ClaimTypes.Role,
                    roleName)
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true
            };

            // Sign in and issue the authentication cookie.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                properties);

            // Honour a safe local return URL only when the user's
            // role is authorized to access that destination.
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                var isAuthorized = await IsReturnUrlAllowedAsync(
                    returnUrl, roleName);

                if (isAuthorized)
                    return Redirect(returnUrl);
            }

            // Redirect to the appropriate dashboard.
            switch (roleName)
            {
                case "SuperAdmin":
                    return RedirectToAction("Index", "SuperAdmin");

                case "Admin":
                    return RedirectToAction("Index", "Admin");

                case "Trainer":
                    return RedirectToAction("Index", "Trainer");

                case "Client":
                    return RedirectToAction("Index", "Client");

                default:
                    await HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);

                    return Forbid();
            }
        }

        // Restrict return URLs to the user's own role area.
        private Task<bool> IsReturnUrlAllowedAsync(
            string returnUrl,
            string roleName)
        {
            var path = returnUrl.Split(
                '?', '#')[0].TrimEnd('/');

            var firstSegment = path
                .TrimStart('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;

            var allowed = roleName switch
            {
                "SuperAdmin" => firstSegment.Equals(
                    "Home", StringComparison.OrdinalIgnoreCase),

                "Admin" => firstSegment.Equals(
                    "Admin", StringComparison.OrdinalIgnoreCase),

                "Trainer" => firstSegment.Equals(
                    "Trainer", StringComparison.OrdinalIgnoreCase),

                "Client" => firstSegment.Equals(
                    "Client", StringComparison.OrdinalIgnoreCase),

                _ => false
            };

            return Task.FromResult(allowed);
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Public registration is restricted to Clients.
            var clientRole = await _db.Roles.FirstOrDefaultAsync(
                r => r.RoleName.ToLower() == "client");

            if (clientRole == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Registration is unavailable. Roles are not configured.");

                return View(model);
            }

            var email = model.Email?.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("Email", "Email is required.");
                return View(model);
            }

            var emailLower = email.ToLower();
            
            // Only get the ID first to avoid materializing NULL fields
            var existingUserId = await _db.Users
                .AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower() == emailLower)
                .Select(u => (int?)u.UserID)
                .FirstOrDefaultAsync();

            if (existingUserId != null)
            {
                ModelState.AddModelError(
                    "Email",
                    "An account with that email already exists.");

                return View(model);
            }

            // Split full name.
            var names = (model.FullName ?? string.Empty)
                .Trim()
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            var firstName = names.Length > 0 ? names[0] : string.Empty;
            var lastName = names.Length > 1 ? names[1] : string.Empty;

            // Hash the password before storing it.
            PasswordHasher.CreatePasswordHash(
                model.Password!,
                out var hash,
                out var salt);

            await using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                var user = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsActive = true,
                    RoleID = clientRole.RoleID,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                // Verify user was created and has a valid UserID
                if (user.UserID <= 0)
                {
                    throw new InvalidOperationException("User creation failed: Invalid UserID.");
                }

                var profile = new ClientProfile
                {
                    ClientProfileID = user.UserID  // ClientID is the UserID foreign key
                };

                _db.ClientProfiles.Add(profile);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                _logger.LogInformation($"Client registered successfully: {email}");
            }
            catch (DbUpdateException dbEx)
            {
                await tx.RollbackAsync();
                _logger.LogError($"Database error during registration for {email}: {dbEx.InnerException?.Message}");

                ModelState.AddModelError(
                    string.Empty,
                    "Could not complete registration. Please try again later.");

                return View(model);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError($"Unexpected error during registration for {email}: {ex.Message}");

                ModelState.AddModelError(
                    string.Empty,
                    "Could not complete registration. Please try again later.");

                return View(model);
            }

            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var email = model.Email?.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(email))
            {
                ViewData["StatusMessage"] = "If an account exists for that email, a password reset link has been sent.";
                return View(model);
            }

            try
            {
                // Step 1: Find user by email
                var user = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

                if (user == null)
                {
                    // Don't reveal whether email exists - security best practice
                    _logger.LogWarning($"Forgot password request for non-existent email: {email}");
                    ViewData["StatusMessage"] = "If an account exists for that email, a password reset link has been sent.";
                    return View(model);
                }

                // Step 2: Clean up expired tokens for this user
                var expiredTokens = await _db.ActivationTokens
                    .Where(t => t.UserID == user.UserID && t.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();
                
                if (expiredTokens.Any())
                {
                    _db.ActivationTokens.RemoveRange(expiredTokens);
                    await _db.SaveChangesAsync();
                }

                // Step 3: Generate new reset token
                _tokenService.GenerateToken(out var plainToken, out var tokenHash);

                // Step 4: Save token to database
                var resetToken = new ActivationToken
                {
                    UserID = user.UserID,
                    TokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // Token valid for 24 hours
                    IsUsed = false
                };

                _db.ActivationTokens.Add(resetToken);
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Password reset token generated for user {user.UserID} ({email})");

                // Step 5: Create password reset link
                var resetLink = Url.Action("ResetPassword", "Account", 
                    new { token = plainToken, email = email }, 
                    protocol: Request.Scheme) ?? "https://localhost:5293/Account/ResetPassword";

                // Step 6: Send email with reset link
                var userName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrEmpty(userName))
                    userName = email;

                var emailSent = await _emailService.SendPasswordResetEmailAsync(
                    email, 
                    userName, 
                    resetLink);

                if (emailSent)
                {
                    _logger.LogInformation($"Password reset email sent to {email}");
                    ViewData["StatusMessage"] = "✅ Password reset link sent! Please check your email inbox.";
                }
                else
                {
                    _logger.LogWarning($"Failed to send password reset email to {email}. Email service may not be configured.");
                    ViewData["StatusMessage"] = "⚠️ Email service is not configured. Please contact administrator.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ForgotPassword: {ex.Message}");
                ViewData["StatusMessage"] = "An error occurred. Please try again later.";
            }

            return View(model);
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(
            string? token = null,
            string? email = null)
        {
            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };

            return View(model);
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Token))
            {
                ModelState.AddModelError(string.Empty, "Invalid request. Please use the link from the password reset email.");
                _logger.LogWarning("ResetPassword: Missing email or token");
                return View(model);
            }

            try
            {
                var email = model.Email.Trim().ToLower();

                // Step 1: Find user by email
                var userIdResult = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Email != null && u.Email.ToLower() == email)
                    .Select(u => (int?)u.UserID)
                    .FirstOrDefaultAsync();

                if (userIdResult == null)
                {
                    _logger.LogWarning($"ResetPassword attempt for non-existent email: {email}");
                    ModelState.AddModelError(string.Empty, "Invalid email or reset token.");
                    return View(model);
                }

                // Step 2: Find the most recent unused reset token for this user
                var resetToken = await _db.ActivationTokens
                    .Where(t => t.UserID == userIdResult.Value 
                        && !t.IsUsed 
                        && t.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(t => t.ActivationTokenID)
                    .FirstOrDefaultAsync();

                if (resetToken == null)
                {
                    _logger.LogWarning($"ResetPassword: No valid token found for user {userIdResult} from email {email}");
                    ModelState.AddModelError(string.Empty, "Invalid or expired reset token. Please request a new password reset link.");
                    return View(model);
                }

                // Step 3: Verify the provided token matches the stored hash
                if (!_tokenService.VerifyToken(model.Token, resetToken.TokenHash))
                {
                    _logger.LogWarning($"ResetPassword: Invalid token verification for user {userIdResult}");
                    ModelState.AddModelError(string.Empty, "Invalid or expired reset token. Please request a new password reset link.");
                    return View(model);
                }

                // Step 4: Load the user for updating
                var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userIdResult.Value);
                
                if (user == null)
                {
                    _logger.LogError($"ResetPassword: User {userIdResult} not found after token validation");
                    ModelState.AddModelError(string.Empty, "User not found. Please try again.");
                    return View(model);
                }

                // Step 5: Hash the new password
                PasswordHasher.CreatePasswordHash(
                    model.Password!,
                    out var newHash,
                    out var newSalt);

                // Step 6: Update user password
                user.PasswordHash = newHash;
                user.PasswordSalt = newSalt;
                user.FailedLoginCount = 0;
                user.LockoutEnd = null;

                // Step 7: Mark token as used
                resetToken.IsUsed = true;

                // Step 8: Mark all other unused tokens as used (one-time use)
                var otherTokens = await _db.ActivationTokens
                    .Where(t => t.UserID == user.UserID && !t.IsUsed && t.ActivationTokenID != resetToken.ActivationTokenID)
                    .ToListAsync();

                foreach (var token in otherTokens)
                {
                    token.IsUsed = true;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Password successfully reset for user {user.UserID} ({email})");

                ViewData["StatusMessage"] = "✅ Your password has been reset successfully. You can now log in with your new password.";
                ModelState.Clear();
                model.Email = null;
                model.Token = null;
                model.Password = null;
                model.ConfirmPassword = null;
                
                return View(model);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error during password reset");
                var innerEx = dbEx.InnerException;
                var errorMessage = "Error updating password: ";
                
                if (innerEx != null)
                {
                    _logger.LogError($"Inner exception: {innerEx.Message}");
                    errorMessage += innerEx.Message;
                }
                else
                {
                    errorMessage += "Failed to save password to database.";
                }

                ModelState.AddModelError(string.Empty, errorMessage);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during password reset for email {model.Email}");
                _logger.LogError($"Exception details: {ex.Message} | {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, $"An error occurred: {ex.Message}");
                return View(model);
            }
        }

        // GET: /Account/VerifyEmail
        [HttpGet]
        public IActionResult VerifyEmail(
            string? userId = null,
            string? code = null)
        {
            var model = new VerifyEmailViewModel
            {
                Email = string.Empty,
                IsVerified = false
            };

            return View(model);
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            return View();
        }

        // POST: /Account/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userEmail = User?.FindFirst(ClaimTypes.Email)?.Value ?? "Unknown";
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _logger.LogInformation($"User logged out: {userEmail}");
                return RedirectToAction(nameof(Login), "Account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return RedirectToAction(nameof(Login), "Account");
            }
        }

        // GET: /Account/SessionExpired
        [HttpGet]
        public IActionResult SessionExpired()
        {
            return View();
        }
    }
}
