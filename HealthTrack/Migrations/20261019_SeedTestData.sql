-- This migration seeds test data for clients and trainers
-- Run this ONLY if you want test data in the database

-- First, check if we have any clients or trainers
SELECT 'Total Users' as RecordType, COUNT(*) as Count FROM [dbo].[Users]
UNION ALL
SELECT 'Total Roles' as RecordType, COUNT(*) as Count FROM [dbo].[Roles]
UNION ALL
SELECT 'Total Clients' as RecordType, COUNT(*) as Count FROM [dbo].[ClientProfiles]
UNION ALL
SELECT 'Total Trainers' as RecordType, COUNT(*) as Count FROM [dbo].[TrainerProfiles];

-- If no clients exist, verify ClientProfiles table structure
IF (SELECT COUNT(*) FROM [dbo].[ClientProfiles]) = 0
BEGIN
    PRINT 'No clients found. Checking ClientProfiles table...';
    SELECT * FROM [dbo].[ClientProfiles];
END

-- If no trainers exist, verify TrainerProfiles table structure
IF (SELECT COUNT(*) FROM [dbo].[TrainerProfiles]) = 0
BEGIN
    PRINT 'No trainers found. Checking TrainerProfiles table...';
    SELECT * FROM [dbo].[TrainerProfiles];
END

-- Show all users with their roles
SELECT u.UserID, u.Email, u.FirstName, u.LastName, r.RoleName, c.ClientProfileID, t.TrainerProfileID
FROM [dbo].[Users] u
LEFT JOIN [dbo].[Roles] r ON u.RoleID = r.RoleID
LEFT JOIN [dbo].[ClientProfiles] c ON u.UserID = c.UserID
LEFT JOIN [dbo].[TrainerProfiles] t ON u.UserID = t.UserID
ORDER BY u.UserID;
