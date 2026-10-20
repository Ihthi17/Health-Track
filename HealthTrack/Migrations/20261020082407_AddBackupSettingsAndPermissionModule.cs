using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupSettingsAndPermissionModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoBackupEnabled",
                table: "SystemSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BackupDayOfWeek",
                table: "SystemSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackupFrequency",
                table: "SystemSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BackupRetentionCount",
                table: "SystemSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "BackupTime",
                table: "SystemSettings",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAutoBackupAt",
                table: "SystemSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "Permissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoBackupEnabled",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "BackupDayOfWeek",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "BackupFrequency",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "BackupRetentionCount",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "BackupTime",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "LastAutoBackupAt",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "Permissions");
        }
    }
}
