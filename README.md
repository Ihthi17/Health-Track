# HealthTrack - Admin Dashboard System

A comprehensive fitness and health management platform built with ASP.NET Core MVC, providing administrators with full system oversight, user management, appointment tracking, and automated backup capabilities.

## Table of Contents
- [System Overview](#system-overview)
- [Prerequisites](#prerequisites)
- [Installation Guide](#installation-guide)
- [Quick Start](#quick-start)
- [Default Credentials](#default-credentials)
- [Project Structure](#project-structure)
- [Features](#features)
- [Database Setup](#database-setup)
- [Configuration](#configuration)
- [Troubleshooting](#troubleshooting)

---

## System Overview

**HealthTrack** is an enterprise-level health and fitness management system designed for fitness studios, personal trainers, and health consultants to manage clients, appointments, health metrics, and generate reports.

**Technology Stack:**
- **Framework:** ASP.NET Core 10.0
- **Database:** SQL Server
- **Frontend:** Bootstrap 5, Chart.js
- **Architecture:** MVC (Model-View-Controller)
- **ORM:** Entity Framework Core

---

## Prerequisites

Before installing HealthTrack, ensure you have the following installed:

1. **Visual Studio 2022** (or later)
   - Download: https://visualstudio.microsoft.com/downloads/
   - Workload: ASP.NET and web development

2. **.NET 10.0 SDK** (or compatible version)
   - Download: https://dotnet.microsoft.com/en-us/download/dotnet
   - Verify: `dotnet --version`

3. **SQL Server** (2019 or later)
   - SQL Server Express: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - SQL Server Management Studio (SSMS): https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms

4. **Git** (for cloning the repository)
   - Download: https://git-scm.com/downloads

---

## Installation Guide

### Step 1: Clone the Repository

```bash
git clone https://github.com/your-org/HealthTrack.git
cd HealthTrack/HealthTrack
```

### Step 2: Open in Visual Studio

1. Open Visual Studio 2022
2. Select **File → Open → Project/Solution**
3. Navigate to the cloned repository
4. Select `HealthTrack.sln`
5. Click **Open**

### Step 3: Restore NuGet Packages

Visual Studio will automatically restore packages. If not:

```bash
# In Package Manager Console
dotnet restore
```

### Step 4: Configure the Database Connection

1. Open `appsettings.json` in the project root
2. Update the connection string with your SQL Server details:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=HealthTrackDB;User Id=sa;Password=YOUR_PASSWORD;Trusted_Connection=true;Encrypt=false;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Example for Local SQL Server:**
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HealthTrackDB;Trusted_Connection=true;"
```

**Example for Named Instance:**
```json
"DefaultConnection": "Server=YOUR_COMPUTER\\SQLEXPRESS;Database=HealthTrackDB;User Id=sa;Password=YourPassword;Encrypt=false;"
```

### Step 5: Apply Database Migrations

Run the following commands in the **Package Manager Console** (Tools → NuGet Package Manager → Package Manager Console):

```powershell
# Update the database with all migrations
dotnet ef database update

# Or in Package Manager Console:
Update-Database
```

This will:
- Create the `HealthTrackDB` database
- Create all required tables
- Seed initial data (roles, permissions, admin user)

### Step 6: Build the Project

```bash
dotnet build
```

Or use Visual Studio: **Build → Build Solution** (Ctrl + Shift + B)

### Step 7: Run the Application

```bash
dotnet run
```

Or use Visual Studio: **Debug → Start Debugging** (F5)

The application will start at: **http://localhost:5293**

---

## Quick Start

### 1. Access the Admin Dashboard

1. Open your browser and navigate to: `http://localhost:5293`
2. You will be redirected to the login page
3. Login with the default **Super Admin** credentials (see below)

### 2. Complete Initial Setup

After first login:
1. Navigate to **Settings → System Configuration**
2. Update system information:
   - System Name
   - System Logo (optional)
   - Email Settings (SMTP)
   - Backup Settings
3. Click **Save Settings**

### 3. Create Additional Admin Users

1. Go to **User Management → Admins**
2. Click **Add New Admin**
3. Fill in user details
4. Assign roles and permissions
5. Click **Create User**

---

## Default Credentials

### Super Admin Account

| Field | Value |
|-------|-------|
| **Email** | `admin@healthtrack.local` |
| **Password** | `Admin@123456` |
| **Role** | Super Admin |
| **Permissions** | All system permissions |

**⚠️ IMPORTANT:** Change this password immediately after first login for security purposes.

### How to Change Password

1. Click your profile icon (top-right corner)
2. Select **Change Password**
3. Enter current password: `Admin@123456`
4. Enter new password (minimum 8 characters, must include uppercase, lowercase, numbers, and special characters)
5. Confirm new password
6. Click **Update**

---

## Project Structure

```
HealthTrack/
│
├── Controllers/
│   ├── AccountController.cs          # Login, registration, password reset
│   ├── SuperAdminController.cs       # Admin dashboard and system settings
│   ├── UserManagementController.cs   # User CRUD operations
│   ├── AppointmentController.cs      # Appointment management
│   └── ReportController.cs           # Reports and analytics
│
├── Models/
│   ├── User.cs                       # User entity
│   ├── ClientProfile.cs              # Client profile data
│   ├── TrainerProfile.cs             # Trainer profile data
│   ├── Appointment.cs                # Appointment scheduling
│   ├── Role.cs                       # User roles
│   ├── Permission.cs                 # Permission definitions
│   ├── SystemSettings.cs             # System configuration
│   └── AuditLog.cs                   # Activity logging
│
├── Data/
│   ├── HealthTrackContext.cs         # EF Core DbContext
│   └── SeedData.cs                   # Initial data population
│
├── Services/
│   ├── BackupService.cs              # Automated database backup
│   ├── NotificationService.cs        # Email notifications
│   ├── AppointmentService.cs         # Appointment logic
│   └── ReportService.cs              # Report generation
│
├── ViewModels/
│   ├── SuperAdminDashboardViewModel.cs  # Dashboard data
│   ├── AuthViewModels.cs             # Auth-related VMs
│   └── ReportViewModel.cs            # Report VMs
│
├── Views/
│   ├── Shared/
│   │   ├── _SuperAdminLayout.cshtml  # Admin layout template
│   │   ├── _Navbar.cshtml            # Navigation bar
│   │   └── _Footer.cshtml            # Footer
│   ├── SuperAdmin/
│   │   ├── Index.cshtml              # Dashboard
│   │   ├── Backup.cshtml             # Backup settings
│   │   └── Settings.cshtml           # System settings
│   ├── Account/
│   │   ├── Login.cshtml              # Login page
│   │   └── ChangePassword.cshtml     # Password change
│   └── UserManagement/
│       ├── Index.cshtml              # User list
│       └── Create.cshtml             # Create user form
│
├── Migrations/
│   ├── 20261020082407_AddBackupSettingsAndPermissionModule.cs
│   ├── 20261020092400_EnsureCompleteSchema.cs
│   └── HealthTrackContextModelSnapshot.cs
│
├── wwwroot/
│   ├── css/
│   │   ├── site.css                  # Site styles
│   │   └── dashboard.css             # Dashboard specific styles
│   ├── js/
│   │   └── site.js                   # Site scripts
│   └── images/                       # Static images
│
├── appsettings.json                  # Configuration file
├── appsettings.Development.json      # Development settings
├── Program.cs                        # Application entry point
└── HealthTrack.csproj               # Project file

```

---

## Features

### 1. Admin Dashboard
- **Real-time Statistics:** Total users, clients, trainers, appointments
- **Interactive Charts:** Appointment status distribution, user roles breakdown
- **Recent Activity:** Last 5 appointments, notifications, audit logs
- **Quick Actions:** Links to key management pages

### 2. User Management
- Create, read, update, delete users
- Assign roles: Client, Trainer, Admin, Super Admin
- Manage permissions per role
- Activate/deactivate users
- Password reset functionality

### 3. Appointment Management
- Schedule appointments between clients and trainers
- Track appointment status: Pending, Approved, Completed, Cancelled
- Trainer notes and documentation
- Automatic email notifications

### 4. Automated Backup System
- Scheduled database backups (hourly, daily, weekly, monthly)
- Automatic backup retention policies
- Manual backup triggers
- Backup history and restoration logs
- Configurable backup retention count

### 5. System Settings
- Email configuration (SMTP settings)
- Maintenance mode toggle
- System branding (logo, name)
- Backup scheduling configuration
- Timezone management

### 6. Audit Logging
- Track all system activities
- User actions: login, logout, create, update, delete
- IP address logging
- Detailed action descriptions
- Comprehensive search and filtering

### 7. Role-Based Access Control (RBAC)
- Granular permission system
- Module-based permissions
- Role assignment and management
- Permission enforcement across the application

---

## Database Setup

### Automatic Setup (Recommended)

When you run `dotnet ef database update`, the system automatically:
1. Creates the `HealthTrackDB` database
2. Creates all required tables
3. Creates default roles: Client, Trainer, Admin, Super Admin
4. Creates default permissions
5. Seeds the super admin user
6. Creates system settings entries

### Manual Database Creation

If you prefer to create the database manually:

```sql
-- Create database
CREATE DATABASE HealthTrackDB;

-- Use the database
USE HealthTrackDB;

-- Run migrations via CLI
dotnet ef database update
```

### Database Backup and Restore

**Backup from Command Line:**
```bash
# In the project root
dotnet ef database update --verbose
```

**Backup from SSMS:**
1. Open SQL Server Management Studio
2. Right-click `HealthTrackDB` → Tasks → Back Up
3. Select backup location and click OK

**Restore from SSMS:**
1. Right-click Databases → Restore Database
2. Select device and choose backup file
3. Click OK

---

## Configuration

### Email Settings (appsettings.json)

```json
{
  "EmailSettings": {
    "SMTPServer": "smtp.gmail.com",
    "SMTPPort": 587,
    "SMTPUsername": "your-email@gmail.com",
    "SMTPPassword": "your-app-password",
    "FromEmail": "noreply@healthtrack.local",
    "FromName": "HealthTrack System",
    "EnableSSL": true
  }
}
```

### Backup Settings

Configure via **SuperAdmin Dashboard → Settings → Backup Configuration:**
- **Enable Auto Backup:** Toggle backup scheduling
- **Backup Time:** Time of day for scheduled backups (HH:MM format)
- **Backup Frequency:** Hourly, Daily, Weekly, Monthly
- **Backup Day of Week:** For weekly backups (0=Sunday, 6=Saturday)
- **Retention Count:** Number of backups to keep

### Logging Configuration

Edit `appsettings.json` to adjust log levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

---

## Troubleshooting

### Issue 1: Database Connection Failed

**Error:** `A network-related or instance-specific error occurred while establishing a connection to SQL Server`

**Solution:**
1. Verify SQL Server is running
2. Check connection string in `appsettings.json`
3. Confirm server name and credentials
4. Test connection in SSMS first

```bash
# Verify SQL Server is running
sqlcmd -S YOUR_SERVER -U sa -P YOUR_PASSWORD
```

### Issue 2: Migration Failed

**Error:** `The migration 'XXXXX' has already been applied to the database`

**Solution:**
```bash
# Remove last migration (if not yet applied to DB)
dotnet ef migrations remove

# Or revert to specific migration
dotnet ef database update PREVIOUS_MIGRATION_NAME
```

### Issue 3: Port Already in Use

**Error:** `System.IO.IOException: Failed to bind to address http://127.0.0.1:5293`

**Solution:**
```bash
# Change port in launchSettings.json
# Or kill the process using the port:
netstat -ano | findstr :5293
taskkill /PID <PID> /F
```

### Issue 4: NuGet Packages Not Restoring

**Error:** `One or more packages are incompatible with TargetFramework`

**Solution:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore --force

# Rebuild
dotnet build
```

### Issue 5: Login Not Working

**Error:** `Invalid credentials` or `Account not found`

**Solution:**
1. Verify the super admin user was created during migration
2. Check database contains Users table with admin@healthtrack.local
3. Reset password in SSMS:
   ```sql
   USE HealthTrackDB;
   SELECT * FROM Users WHERE Email = 'admin@healthtrack.local';
   ```

### Issue 6: Charts Not Displaying

**Error:** Charts appear empty or with error

**Solution:**
1. Verify Chart.js CDN is accessible
2. Check browser console for JavaScript errors (F12)
3. Ensure database has appointment data
4. Verify ViewModel properties are populated

---

## Development Workflow

### Adding a New Feature

1. **Create Model**
   ```bash
   # Add entity to Models/
   ```

2. **Create Migration**
   ```bash
   dotnet ef migrations add DescriptiveNameOfChange
   ```

3. **Apply Migration**
   ```bash
   dotnet ef database update
   ```

4. **Create Controller**
   ```bash
   # Add controller to Controllers/
   ```

5. **Create Views**
   ```bash
   # Add Razor views to Views/ControllerName/
   ```

6. **Test Locally**
   ```bash
   dotnet run
   # Navigate to http://localhost:5293
   ```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test Tests/HealthTrack.Tests.csproj

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## Deployment

### Publish to Production

```bash
# Build release version
dotnet publish -c Release -o ./publish

# Create IIS website pointing to publish folder
# Configure application pool to use .NET Core Hosting Bundle
```

### Azure Deployment

```bash
# Install Azure CLI
# Login to Azure
az login

# Create resource group
az group create --name HealthTrackRG --location eastus

# Deploy to App Service
az webapp up --name healthtrack --resource-group HealthTrackRG --runtime "DOTNET|10.0"
```

---

## Support & Documentation

- **Official Docs:** https://docs.microsoft.com/en-us/aspnet/core/
- **Entity Framework Core:** https://docs.microsoft.com/en-us/ef/core/
- **Bootstrap 5:** https://getbootstrap.com/docs/5.0/
- **Chart.js:** https://www.chartjs.org/docs/latest/

---

## Security Best Practices

1. **Change Default Credentials Immediately**
   - Change super admin password on first login
   - Never share credentials

2. **Enable HTTPS**
   - Always use HTTPS in production
   - Obtain SSL certificate

3. **Regular Backups**
   - Enable automatic backups
   - Test restore procedures regularly
   - Store backups securely

4. **Audit Logs**
   - Monitor audit logs regularly
   - Archive logs for compliance
   - Set up alerts for suspicious activity

5. **Updates**
   - Keep .NET SDK updated
   - Update NuGet packages regularly
   - Monitor security advisories

---

## License

This project is proprietary software. All rights reserved.

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-10-20 | Initial release - Admin Dashboard, User Management, Appointments, Backup System |

---

## Changelog

### v1.0.0 (2026-10-20)
- ✅ Admin Dashboard with real-time statistics
- ✅ User Management (CRUD operations)
- ✅ Appointment Scheduling
- ✅ Automated Database Backup System
- ✅ Role-Based Access Control
- ✅ Audit Logging
- ✅ System Settings Management
- ✅ Email Notifications

---

**Last Updated:** October 20, 2026  
**Maintained By:** HealthTrack Development Team

For issues or questions, please contact: support@healthtrack.local
