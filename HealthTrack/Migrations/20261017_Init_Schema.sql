-- Idempotent schema creation script for HealthTrack
-- Review before running. This script uses IF NOT EXISTS checks to avoid recreating objects.

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'dbo')
BEGIN
	EXEC('CREATE SCHEMA dbo');
END

-- Roles
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
-- HealthTrack Database Schema Initialization
-- This script creates or validates the database schema to ensure it matches the EF Core model configuration

-- Roles
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[Roles]
	(
		[RoleID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[RoleName] NVARCHAR(100) NOT NULL,
		[Description] NVARCHAR(MAX) NULL
	);
	CREATE UNIQUE INDEX IX_Roles_RoleName ON [dbo].[Roles]([RoleName]);
END

-- Users - CORRECTED SCHEMA TO MATCH EF CORE MODEL
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[Users]
	(
		[UserID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[FirstName] NVARCHAR(100) NOT NULL,
		[LastName] NVARCHAR(100) NOT NULL,
		[Email] NVARCHAR(256) NOT NULL,
		[PasswordHashNew] VARCHAR(MAX) NOT NULL,
		[PasswordSaltNew] VARCHAR(MAX) NOT NULL,
		[IsActive] BIT NOT NULL DEFAULT(1),
		[FailedLoginCount] INT NOT NULL DEFAULT(0),
		[LockoutEnd] DATETIME2 NULL,
		[LastLoginAt] DATETIME2 NULL,
		[CreatedAt] DATETIME2 NOT NULL DEFAULT(GETUTCDATE()),
		[RoleID] INT NULL
	);
	CREATE UNIQUE INDEX IX_Users_Email ON [dbo].[Users]([Email]);
	ALTER TABLE [dbo].[Users]
		ADD CONSTRAINT FK_Users_Roles_RoleID FOREIGN KEY ([RoleID]) REFERENCES [dbo].[Roles]([RoleID]) ON DELETE SET NULL;
END
ELSE
BEGIN
	-- If Users table exists, ensure PasswordHashNew and PasswordSaltNew columns exist and are VARCHAR(MAX)
	IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordHashNew')
	BEGIN
		ALTER TABLE [dbo].[Users] ADD [PasswordHashNew] VARCHAR(MAX) NULL;
	END
	IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordSaltNew')
	BEGIN
		ALTER TABLE [dbo].[Users] ADD [PasswordSaltNew] VARCHAR(MAX) NULL;
	END
	-- Check if columns are VARBINARY and convert to VARCHAR(MAX) if needed
	IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordHashNew' AND system_type_id = 165)
	BEGIN
		-- Drop constraint if exists and recreate column as VARCHAR(MAX)
		ALTER TABLE [dbo].[Users] DROP COLUMN [PasswordHashNew];
		ALTER TABLE [dbo].[Users] ADD [PasswordHashNew] VARCHAR(MAX) NULL;
	END
	IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordSaltNew' AND system_type_id = 165)
	BEGIN
		-- Drop constraint if exists and recreate column as VARCHAR(MAX)
		ALTER TABLE [dbo].[Users] DROP COLUMN [PasswordSaltNew];
		ALTER TABLE [dbo].[Users] ADD [PasswordSaltNew] VARCHAR(MAX) NULL;
	END
END

-- ClientProfiles
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ClientProfiles]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[ClientProfiles]
	(
		[ClientProfileID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[UserID] INT NOT NULL,
		[DateOfBirth] DATETIME2 NULL,
		[Gender] NVARCHAR(50) NULL
	);
	CREATE UNIQUE INDEX IX_ClientProfiles_UserID ON [dbo].[ClientProfiles]([UserID]);
	ALTER TABLE [dbo].[ClientProfiles] ADD CONSTRAINT FK_ClientProfiles_Users_UserID FOREIGN KEY([UserID]) REFERENCES [dbo].[Users]([UserID]) ON DELETE CASCADE;
END

-- TrainerProfiles
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TrainerProfiles]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[TrainerProfiles]
	(
		[TrainerProfileID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[UserID] INT NOT NULL,
		[PhoneNumber] NVARCHAR(50) NULL,
		[Specialization] NVARCHAR(200) NULL,
		[Qualifications] NVARCHAR(MAX) NULL,
		[IsActivated] BIT NOT NULL DEFAULT(0)
	);
	CREATE UNIQUE INDEX IX_TrainerProfiles_UserID ON [dbo].[TrainerProfiles]([UserID]);
	ALTER TABLE [dbo].[TrainerProfiles] ADD CONSTRAINT FK_TrainerProfiles_Users_UserID FOREIGN KEY([UserID]) REFERENCES [dbo].[Users]([UserID]) ON DELETE CASCADE;
END

-- ActivationTokens
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ActivationTokens]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[ActivationTokens]
	(
		[ActivationTokenID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[UserID] INT NOT NULL,
		[TokenHash] VARBINARY(512) NOT NULL,
		[ExpiresAt] DATETIME2 NOT NULL,
		[IsUsed] BIT NOT NULL DEFAULT(0)
	);
	ALTER TABLE [dbo].[ActivationTokens] ADD CONSTRAINT FK_ActivationTokens_Users_UserID FOREIGN KEY([UserID]) REFERENCES [dbo].[Users]([UserID]) ON DELETE CASCADE;
END

-- Seed standard roles if missing
IF NOT EXISTS (SELECT * FROM [dbo].[Roles] WHERE [RoleName] = 'SuperAdmin')
	INSERT INTO [dbo].[Roles]([RoleName]) VALUES('SuperAdmin');
IF NOT EXISTS (SELECT * FROM [dbo].[Roles] WHERE [RoleName] = 'Admin')
	INSERT INTO [dbo].[Roles]([RoleName]) VALUES('Admin');
IF NOT EXISTS (SELECT * FROM [dbo].[Roles] WHERE [RoleName] = 'Trainer')
	INSERT INTO [dbo].[Roles]([RoleName]) VALUES('Trainer');
IF NOT EXISTS (SELECT * FROM [dbo].[Roles] WHERE [RoleName] = 'Client')
	INSERT INTO [dbo].[Roles]([RoleName]) VALUES('Client');

-- Note: Do not create or modify users here. Provisioning of initial SuperAdmin is handled by application startup using secure environment variables or secrets.

