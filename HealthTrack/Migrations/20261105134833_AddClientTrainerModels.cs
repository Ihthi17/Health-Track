using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <summary>
    /// Registers the workout, diet, progress, feedback and trainer-client
    /// assignment tables with Entity Framework.
    ///
    /// These tables ALREADY EXIST in HealthTrackDB, so this migration is
    /// intentionally empty: it only updates EF's model snapshot so the
    /// C# models and the database are recorded as matching.
    /// </summary>
    public partial class AddClientTrainerModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - tables already exist in the database.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - nothing was created, so nothing to remove.
        }
    }
}