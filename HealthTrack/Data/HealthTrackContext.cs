
using Microsoft.EntityFrameworkCore;
using HealthTrack.Models;

namespace HealthTrack.Data
{
    public class HealthTrackContext : DbContext
    {
        public HealthTrackContext(
            DbContextOptions<HealthTrackContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<ClientProfile> ClientProfiles { get; set; } = null!;
        public DbSet<TrainerProfile> TrainerProfiles { get; set; } = null!;
        public DbSet<ActivationToken> ActivationTokens { get; set; } = null!;
        public DbSet<SystemSettings> SystemSettings { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<EmailSettings> EmailSettings { get; set; } = null!;
        public DbSet<BackupLog> BackupLogs { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        /// <summary>
        /// Auto-creates missing ClientProfile and TrainerProfile rows for existing users.
        /// Run once on app startup or in migration.
        /// </summary>
        public async Task BackfillProfilesAsync()
        {
            try
            {
                // Get roles
                var clientRole = await Roles.FirstOrDefaultAsync(r => r.RoleName == "Client");
                var trainerRole = await Roles.FirstOrDefaultAsync(r => r.RoleName == "Trainer");

                bool hasChanges = false;

                if (clientRole != null)
                {
                    // Find users with Client role but no ClientProfile
                    var clientsWithoutProfile = await Users
                        .Where(u => u.RoleID == clientRole.RoleID && !ClientProfiles.Any(c => c.UserID == u.UserID))
                        .ToListAsync();

                    foreach (var user in clientsWithoutProfile)
                    {
                        ClientProfiles.Add(new ClientProfile { UserID = user.UserID });
                        hasChanges = true;
                    }
                }

                if (trainerRole != null)
                {
                    // Find users with Trainer role but no TrainerProfile
                    var trainersWithoutProfile = await Users
                        .Where(u => u.RoleID == trainerRole.RoleID && !TrainerProfiles.Any(t => t.UserID == u.UserID))
                        .ToListAsync();

                    foreach (var user in trainersWithoutProfile)
                    {
                        TrainerProfiles.Add(new TrainerProfile { UserID = user.UserID });
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    await SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backfill profiles error: {ex.Message}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Role configuration
            modelBuilder.Entity<Role>(b =>
            {
                b.HasIndex(r => r.RoleName)
                    .IsUnique();
                
                // Many-to-many with Permissions via PermissionRole junction table
                b.HasMany(r => r.Permissions)
                    .WithMany(p => p.Roles)
                    .UsingEntity(
                        "PermissionRole",
                        l => l.HasOne(typeof(Permission)).WithMany(),
                        r => r.HasOne(typeof(Role)).WithMany(),
                        j => 
                        {
                            j.Property<int>("PermissionID");
                            j.Property<int>("RoleID");
                            j.HasKey("PermissionID", "RoleID");
                            j.ToTable("PermissionRole");
                        }
                    );
            });

            // User configuration
            modelBuilder.Entity<User>(b =>
            {
                b.HasIndex(u => u.Email)
                    .IsUnique();

                b.HasOne(u => u.Role)
                    .WithMany()
                    .HasForeignKey(u => u.RoleID)
                    .OnDelete(DeleteBehavior.SetNull);

                // Map string properties to the SQL Server columns as VARCHAR(MAX)
                b.Property(u => u.PasswordHash)
                    .HasColumnName("PasswordHashNew")
                    .HasColumnType("VARCHAR(MAX)")
                    .IsRequired();

                b.Property(u => u.PasswordSalt)
                    .HasColumnName("PasswordSaltNew")
                    .HasColumnType("VARCHAR(MAX)")
                    .IsRequired();
            });

            // Client profile configuration
            modelBuilder.Entity<ClientProfile>(b =>
            {
                b.ToTable("ClientProfiles");
                b.HasKey(c => c.ClientProfileID);
                b.Property(c => c.ClientProfileID).HasColumnName("ClientID");
                b.Property(c => c.UserID).HasColumnName("UserID");

                b.HasOne(c => c.User)
                    .WithOne()
                    .HasForeignKey<ClientProfile>(c => c.UserID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Trainer profile configuration
            modelBuilder.Entity<TrainerProfile>(b =>
            {
                b.ToTable("TrainerProfiles");
                b.HasKey(t => t.TrainerProfileID);
                b.Property(t => t.TrainerProfileID).HasColumnName("TrainerID");
                b.Property(t => t.UserID).HasColumnName("UserID");
                b.Property(t => t.PhoneNumber).HasColumnName("PhoneNumber");
                b.Property(t => t.Specialization).HasColumnName("Specialization");
                b.Property(t => t.Qualifications).HasColumnName("Qualifications");
                b.Property(t => t.IsActivated).HasColumnName("IsActivated");

                b.HasOne(t => t.User)
                    .WithOne()
                    .HasForeignKey<TrainerProfile>(t => t.UserID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AuditLog configuration - map to existing table schema
            modelBuilder.Entity<AuditLog>(b =>
            {
                b.ToTable("AuditLogs");
                b.HasKey(a => a.AuditID);
                b.Property(a => a.AuditID).HasColumnName("AuditID");
                b.Property(a => a.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
                b.Property(a => a.Details).HasColumnName("Details").HasMaxLength(500);
                b.Property(a => a.CreatedAt).HasColumnName("CreatedAt").IsRequired();
                b.Property(a => a.IPAddress).HasColumnName("IPAddress");
                b.Property(a => a.UserID).HasColumnName("UserID");
                b.Ignore(a => a.PerformedBy);
                b.Ignore(a => a.Timestamp);
            });

            // Appointment configuration
            modelBuilder.Entity<Appointment>(b =>
            {
                b.ToTable("Appointments");
                b.HasKey(a => a.AppointmentID);
                
                b.Property(a => a.ClientID).IsRequired();
                b.Property(a => a.TrainerID).IsRequired(); // DB requires NOT NULL
                b.Property(a => a.StartTime).HasColumnName("StartTime").IsRequired();
                b.Property(a => a.EndTime).HasColumnName("EndTime").IsRequired();
                b.Property(a => a.Reason).HasColumnName("Reason").HasMaxLength(500);
                b.Property(a => a.TrainerNotes).HasColumnName("TrainerNotes").HasMaxLength(500);
                b.Property(a => a.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
                b.Property(a => a.CreatedAt).HasColumnName("CreatedAt").IsRequired();

                b.HasOne(a => a.Client)
                    .WithMany()
                    .HasForeignKey(a => a.ClientID)
                    .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(a => a.Trainer)
                    .WithMany()
                    .HasForeignKey(a => a.TrainerID)
                    .OnDelete(DeleteBehavior.Restrict); // Changed from SetNull to Restrict since TrainerID is NOT NULL

                b.HasIndex(a => a.CreatedAt);
                b.HasIndex(a => a.Status);
            });

            // Notification configuration
            modelBuilder.Entity<Notification>(b =>
            {
                b.ToTable("Notifications");
                b.HasKey(n => n.NotificationID);
                
                b.Property(n => n.NotificationID).HasColumnName("NotificationID");
                b.Property(n => n.UserID).HasColumnName("UserID").IsRequired();
                b.Property(n => n.Type).HasColumnName("Type").HasMaxLength(50).IsRequired();
                b.Property(n => n.Title).HasColumnName("Title").HasMaxLength(200).IsRequired();
                b.Property(n => n.Message).HasColumnName("Message").HasMaxLength(500);
                b.Property(n => n.RemindAt).HasColumnName("RemindAt");
                b.Property(n => n.IsRead).HasColumnName("IsRead").IsRequired();
                b.Property(n => n.CreatedAt).HasColumnName("CreatedAt").IsRequired();

                b.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserID)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(n => new { n.UserID, n.IsRead, n.CreatedAt });
            });

            // ActivationToken configuration
            modelBuilder.Entity<ActivationToken>(b =>
            {
                b.ToTable("ActivationTokens");
                b.HasKey(t => t.ActivationTokenID);
                b.Property(t => t.UserID).IsRequired();
                b.Property(t => t.TokenHash)
                    .HasColumnType("VARCHAR(MAX)")
                    .IsRequired();
                b.Property(t => t.ExpiresAt).IsRequired();
                b.Property(t => t.IsUsed).IsRequired();
            });
        }
    }
}
