using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <inheritdoc />
    public partial class EnsureCompleteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure ClientProfiles has all required columns
            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'UserID' AND Object_ID = Object_ID(N'[dbo].[ClientProfiles]'))
                BEGIN
                    ALTER TABLE [dbo].[ClientProfiles] ADD [UserID] [int] NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = Object_ID(N'[dbo].[ClientProfiles]') AND name = N'FK_ClientProfiles_Users_UserID')
                BEGIN
                    ALTER TABLE [dbo].[ClientProfiles] ADD CONSTRAINT [FK_ClientProfiles_Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID]) ON DELETE NO ACTION;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = Object_ID(N'[dbo].[ClientProfiles]') AND name = N'IX_ClientProfiles_UserID')
                BEGIN
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_ClientProfiles_UserID] ON [dbo].[ClientProfiles]([UserID]) WHERE ([UserID] IS NOT NULL);
                END
            ");

            // Ensure TrainerProfiles has all required columns
            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'UserID' AND Object_ID = Object_ID(N'[dbo].[TrainerProfiles]'))
                BEGIN
                    ALTER TABLE [dbo].[TrainerProfiles] ADD [UserID] [int] NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = Object_ID(N'[dbo].[TrainerProfiles]') AND name = N'FK_TrainerProfiles_Users_UserID')
                BEGIN
                    ALTER TABLE [dbo].[TrainerProfiles] ADD CONSTRAINT [FK_TrainerProfiles_Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID]) ON DELETE NO ACTION;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = Object_ID(N'[dbo].[TrainerProfiles]') AND name = N'IX_TrainerProfiles_UserID')
                BEGIN
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_TrainerProfiles_UserID] ON [dbo].[TrainerProfiles]([UserID]) WHERE ([UserID] IS NOT NULL);
                END
            ");

            // Ensure Notifications table has correct columns
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'UserID' AND Object_ID = Object_ID(N'[dbo].[Notifications]'))
                    BEGIN
                        ALTER TABLE [dbo].[Notifications] ADD [UserID] [int] NULL;
                    END
                    IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = Object_ID(N'[dbo].[Notifications]') AND name = N'FK_Notifications_Users_UserID')
                    BEGIN
                        ALTER TABLE [dbo].[Notifications] ADD CONSTRAINT [FK_Notifications_Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID]) ON DELETE NO ACTION;
                    END
                END
            ");

            // Ensure AuditLogs table has correct columns
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'UserID' AND Object_ID = Object_ID(N'[dbo].[AuditLogs]'))
                    BEGIN
                        ALTER TABLE [dbo].[AuditLogs] ADD [UserID] [int] NULL;
                    END
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration adds schema - it's not reversible in a meaningful way
        }
    }
}
