using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Trainer reports (Task 7): popular routines and engagement statistics
    /// for the clients assigned to the logged-in trainer, with a CSV export.
    /// The calculations live in AnalyticsService so the admin report uses the same logic.
    /// </summary>
    [Authorize(Roles = "Trainer")]
    [Route("trainer/reports")]
    public class TrainerAnalyticsController : Controller
    {
        private readonly HealthTrackContext _context;

        public TrainerAnalyticsController(HealthTrackContext context)
        {
            _context = context;
        }

        /// <summary>ClientIDs actively assigned to the logged-in trainer (null if not a trainer).</summary>
        private async Task<List<int>?> GetAssignedClientIdsAsync()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int userId = int.TryParse(claim, out var id) ? id : 0;

            var trainerId = await _context.TrainerProfiles
                .Where(t => t.UserID == userId || t.TrainerProfileID == userId)
                .Select(t => (int?)t.TrainerProfileID)
                .FirstOrDefaultAsync();

            if (trainerId == null)
                return null;

            return await _context.TrainerClientAssignments
                .Where(a => a.TrainerID == trainerId.Value && a.IsActive)
                .Select(a => a.ClientID)
                .ToListAsync();
        }

        /// <summary>GET /trainer/reports?days=30 - the report page.</summary>
        [HttpGet("")]
        public async Task<IActionResult> Reports(int days = 30)
        {
            var clientIds = await GetAssignedClientIdsAsync();
            if (clientIds == null)
                return Unauthorized();

            var report = await AnalyticsService.BuildReportAsync(_context, clientIds, days);
            ViewData["Scope"] = "your clients";
            ViewData["ExportUrl"] = $"/trainer/reports/export?days={report.Days}";
            ViewData["BaseUrl"] = "/trainer/reports";
            return View("~/Views/TrainerAnalytics/Reports.cshtml", report);
        }

        /// <summary>GET /trainer/reports/export?days=30 - downloads the report as a CSV file.</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(int days = 30)
        {
            var clientIds = await GetAssignedClientIdsAsync();
            if (clientIds == null)
                return Unauthorized();

            var report = await AnalyticsService.BuildReportAsync(_context, clientIds, days);
            var bytes = AnalyticsService.ToCsv(report, "HealthTrack - Trainer Report");
            return File(bytes, "text/csv", $"trainer_report_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }
    }
}
