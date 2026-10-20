-- Clean corrupted appointment data and ensure database integrity
-- This script removes appointments with invalid foreign key references

-- First, delete appointments where ClientID doesn't exist in ClientProfiles
DELETE FROM [dbo].[Appointments]
WHERE [ClientID] NOT IN (SELECT [ClientID] FROM [dbo].[ClientProfiles]);

-- Delete appointments where TrainerID doesn't exist in TrainerProfiles
DELETE FROM [dbo].[Appointments]
WHERE [TrainerID] > 0 AND [TrainerID] NOT IN (SELECT [TrainerID] FROM [dbo].[TrainerProfiles]);

-- Verify the clean-up
SELECT COUNT(*) as AppointmentCount FROM [dbo].[Appointments];
SELECT COUNT(*) as AppointmentWithValidClient FROM [dbo].[Appointments] WHERE [ClientID] IN (SELECT [ClientID] FROM [dbo].[ClientProfiles]);
