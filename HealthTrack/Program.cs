
using Microsoft.EntityFrameworkCore;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Register services
builder.Services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Cookie authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// SQL Server connection
builder.Services.AddDbContext<HealthTrackContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Seed roles and provision SuperAdmin
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var db = services.GetRequiredService<HealthTrackContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Apply pending EF Core migrations
        logger.LogInformation("Applying database migrations...");
        db.Database.Migrate();
        logger.LogInformation("Database migrations completed successfully.");

        // Ensure roles exist
        var requiredRoles = new[]
        {
            "SuperAdmin",
            "Admin",
            "Trainer",
            "Client"
        };

        foreach (var roleName in requiredRoles)
        {
            if (!db.Roles.Any(r => r.RoleName == roleName))
            {
                db.Roles.Add(new Role
                {
                    RoleName = roleName
                });
            }
        }

        db.SaveChanges();

        // Backfill missing ClientProfile and TrainerProfile records
        logger.LogInformation("Backfilling missing Client and Trainer profiles...");
        await db.BackfillProfilesAsync();
        logger.LogInformation("Profile backfill completed.");

        var superEmail =
            builder.Configuration["HEALTHTRACK_SUPERADMIN_EMAIL"]
            ?? builder.Configuration["SuperAdmin:Email"];

        var superPassword =
            Environment.GetEnvironmentVariable(
                "HEALTHTRACK_SUPERADMIN_PASSWORD")
            ?? builder.Configuration["SuperAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(superEmail) &&
            !string.IsNullOrWhiteSpace(superPassword))
        {
            try
            {
                // Use AsNoTracking and only select the ID to avoid NULL materialization errors
                var superEmailLower = superEmail.ToLower();
                var existingUserId = db.Users
                    .AsNoTracking()
                    .Where(u => u.Email != null && u.Email.ToLower() == superEmailLower)
                    .Select(u => (int?)u.UserID)
                    .FirstOrDefault();

                if (existingUserId == null)
                {
                    logger.LogInformation($"Creating SuperAdmin user with email: {superEmail}");
                    
                    var role = db.Roles.FirstOrDefault(
                        r => r.RoleName == "SuperAdmin");

                    if (role == null)
                    {
                        logger.LogError("SuperAdmin role not found in database!");
                        throw new InvalidOperationException("SuperAdmin role not configured.");
                    }

                    PasswordHasher.CreatePasswordHash(
                        superPassword,
                        out var hash,
                        out var salt);

                    var user = new User
                    {
                        FirstName = "Super",
                        LastName = "Admin",
                        Email = superEmail,
                        PasswordHash = hash,
                        PasswordSalt = salt,
                        IsActive = true,
                        FailedLoginCount = 0,
                        RoleID = role.RoleID,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Users.Add(user);
                    db.SaveChanges();
                    logger.LogInformation($"SuperAdmin user created successfully.");
                }
                else
                {
                    // User exists - fetch with tracking to check/update
                    var existing = db.Users.FirstOrDefault(u => u.UserID == existingUserId);
                    
                    if (existing != null && (existing.PasswordHash == null || existing.PasswordSalt == null))
                    {
                        logger.LogWarning($"Repairing SuperAdmin user with email: {superEmail}");
                        
                        PasswordHasher.CreatePasswordHash(
                            superPassword,
                            out var hash,
                            out var salt);

                        existing.PasswordHash = hash;
                        existing.PasswordSalt = salt;

                        var superAdminRole = db.Roles.FirstOrDefault(
                            r => r.RoleName == "SuperAdmin");

                        if (superAdminRole != null)
                        {
                            existing.RoleID = superAdminRole.RoleID;
                        }

                        existing.IsActive = true;
                        existing.FailedLoginCount = 0;
                        existing.LockoutEnd = null;

                        db.SaveChanges();
                        logger.LogInformation($"SuperAdmin user repaired successfully.");
                    }
                    else
                    {
                        logger.LogInformation($"SuperAdmin user with email {superEmail} already exists and is configured.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during SuperAdmin provisioning: {ex.Message}");
                // Don't throw - let application continue
            }
        }
        else
        {
            logger.LogWarning("SuperAdmin credentials not found in configuration or environment variables.");
        }
    }
    catch (Exception ex)
    {
        // Log the actual error during development.
        app.Logger.LogError(
            ex,
            "HealthTrack database migration or seeding failed.");

        if (app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}

// HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Authentication MUST run before authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

// Start automatic backup scheduler
using (var scope = app.Services.CreateScope())
{
    var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
    _ = backupService.ScheduleAutoBackupsAsync();
}

app.Run();
