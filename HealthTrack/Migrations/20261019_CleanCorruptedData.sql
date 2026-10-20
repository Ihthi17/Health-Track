-- Clean corrupted user data and ensure database integrity
-- This script removes any users with NULL required fields

-- First, delete any records with NULL PasswordHashNew or PasswordSaltNew
DELETE FROM [dbo].[Users] 
WHERE [PasswordHashNew] IS NULL OR [PasswordSaltNew] IS NULL;

-- Delete any users where RoleID is NULL (unassigned role)
-- Optional: only if you want to enforce role assignment
-- DELETE FROM [dbo].[Users] WHERE [RoleID] IS NULL;

-- Verify the clean-up
SELECT COUNT(*) as UserCount FROM [dbo].[Users];
SELECT COUNT(*) as RoleCount FROM [dbo].[Roles];

-- Optional: If you want to completely reset the database
-- UNCOMMENT BELOW TO RESET EVERYTHING

/*
-- Drop all tables
DROP TABLE IF EXISTS [dbo].[ActivationTokens];
DROP TABLE IF EXISTS [dbo].[ClientProfiles];
DROP TABLE IF EXISTS [dbo].[TrainerProfiles];
DROP TABLE IF EXISTS [dbo].[Users];
DROP TABLE IF EXISTS [dbo].[Roles];

-- The application will recreate them on startup via EF Core migrations
*/
