-- Migration: Fix Password Columns to match EF Core Model
-- This script updates existing databases where password columns are named incorrectly

-- Check if old columns exist and migrate data to new columns if needed
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordHash' AND name != 'PasswordHashNew')
BEGIN
    -- If PasswordHashNew doesn't exist, create it
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordHashNew')
    BEGIN
        ALTER TABLE [dbo].[Users] ADD [PasswordHashNew] VARBINARY(32) NULL;
    END
    
    -- If PasswordSaltNew doesn't exist, create it
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordSaltNew')
    BEGIN
        ALTER TABLE [dbo].[Users] ADD [PasswordSaltNew] VARBINARY(16) NULL;
    END
    
    -- Note: Manual migration of existing password data required
    -- Old passwords were likely stored as base64 strings and need to be re-hashed
    -- This is a breaking change - users will need to reset passwords
END

-- Update foreign key constraints to CASCADE delete if they don't already
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ClientProfiles_Users_UserID')
BEGIN
    -- Check if delete action is SET NULL and change to CASCADE
    DECLARE @FKName NVARCHAR(128) = 'FK_ClientProfiles_Users_UserID'
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = @FKName AND DELETE_RULE = 'SET NULL')
    BEGIN
        ALTER TABLE [dbo].[ClientProfiles] DROP CONSTRAINT FK_ClientProfiles_Users_UserID;
        ALTER TABLE [dbo].[ClientProfiles] ADD CONSTRAINT FK_ClientProfiles_Users_UserID 
            FOREIGN KEY([UserID]) REFERENCES [dbo].[Users]([UserID]) ON DELETE CASCADE;
    END
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TrainerProfiles_Users_UserID')
BEGIN
    DECLARE @FKName NVARCHAR(128) = 'FK_TrainerProfiles_Users_UserID'
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = @FKName AND DELETE_RULE = 'SET NULL')
    BEGIN
        ALTER TABLE [dbo].[TrainerProfiles] DROP CONSTRAINT FK_TrainerProfiles_Users_UserID;
        ALTER TABLE [dbo].[TrainerProfiles] ADD CONSTRAINT FK_TrainerProfiles_Users_UserID 
            FOREIGN KEY([UserID]) REFERENCES [dbo].[Users]([UserID]) ON DELETE CASCADE;
    END
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ActivationTokens_Users_UserID')
BEGIN
    DECLARE @FKName NVARCHAR(128) = 'FK_ActivationTokens_Users_UserID'
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = @FKName AND DELETE_RULE = 'SET NULL')
    BEGIN
        ALTER TABLE [dbo].[ActivationTokens] DROP CONSTRAINT FK_ActivationTokens_Users_UserID;
        ALTER TABLE [dbo].[ActivationTokens] ADD CONSTRAINT FK_ActivationTokens_Users_UserID 
            FOREIGN KEY([UserID]) REFERENCES [dbo].[Users]([UserID]) ON DELETE CASCADE;
    END
END
