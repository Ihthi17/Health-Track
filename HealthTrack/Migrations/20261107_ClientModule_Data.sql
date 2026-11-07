/* =====================================================================
   HealthTrack - Client module & client/trainer connection: data script
   Author: Umaira (client module)
   Date:   2026-11-07

   Run this ONCE in SSMS on HealthTrackDB after pulling the client-module
   branch. It is safe to run more than once.

   1. Repairs ClientProfiles / TrainerProfiles rows whose UserID was left
      empty by the old registration / admin create-user code.
      (ClientID and TrainerID are foreign keys to Users.UserID, so the
       correct UserID is the same number.)
   2. Adds a starter exercise library used by trainer workout plans and
      client workout logging.
   ===================================================================== */

USE HealthTrackDB;
GO

-- 1) Repair profile links ------------------------------------------------
UPDATE ClientProfiles  SET UserID = ClientID  WHERE UserID IS NULL;
UPDATE TrainerProfiles SET UserID = TrainerID WHERE UserID IS NULL;
GO

-- 2) Starter exercise library -------------------------------------------
-- CaloriesPerMinute is an average estimate for a ~70 kg adult.
INSERT INTO Exercises (ExerciseName, Category, CaloriesPerMinute, Description)
SELECT v.ExerciseName, v.Category, v.CaloriesPerMinute, v.Description
FROM (VALUES
    ('Running',          'Cardio',      11.0, 'Steady-pace running, outdoors or treadmill'),
    ('Brisk Walking',    'Cardio',       5.0, 'Fast-paced walking'),
    ('Cycling',          'Cardio',       8.5, 'Stationary bike or outdoor cycling'),
    ('Jump Rope',        'Cardio',      12.0, 'Skipping rope at a steady rhythm'),
    ('Swimming',         'Cardio',       9.0, 'Freestyle laps at moderate pace'),
    ('Push-ups',         'Strength',     7.0, 'Bodyweight chest and arm exercise'),
    ('Squats',           'Strength',     6.0, 'Bodyweight or weighted leg exercise'),
    ('Lunges',           'Strength',     6.0, 'Alternating forward lunges'),
    ('Deadlift',         'Strength',     6.5, 'Barbell hip-hinge lift'),
    ('Bench Press',      'Strength',     5.5, 'Barbell chest press'),
    ('Pull-ups',         'Strength',     8.0, 'Bodyweight back and arm exercise'),
    ('Plank',            'Core',         4.0, 'Hold a straight body position on forearms'),
    ('Crunches',         'Core',         5.0, 'Abdominal curl exercise'),
    ('Yoga',             'Flexibility',  3.0, 'General yoga flow'),
    ('Stretching',       'Flexibility',  2.5, 'Full-body stretching routine')
) AS v(ExerciseName, Category, CaloriesPerMinute, Description)
WHERE NOT EXISTS (SELECT 1 FROM Exercises e WHERE e.ExerciseName = v.ExerciseName);
GO
