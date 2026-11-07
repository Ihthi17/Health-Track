using HealthTrack.Data;
using HealthTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Admin reports (Task 7): popular routines and engagement statistics for ALL
    /// clients, with a CSV export. Uses the same AnalyticsService and page as the
    /// trainer report, but includes every client and shows the trainer of each plan.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [Route("superadmin/analytics")]
    public class AdminAnalyticsController : Controller
    {
        private readonly HealthTrackContext _context;

        public AdminAnalyticsController(HealthTrackContext context)
        {
            _context = context;
        }

        /// <summary>GET /superadmin/analytics?days=30 - system-wide report page.</summary>
        [HttpGet("")]
        public async Task<IActionResult> Analytics(int days = 30)
        {
            var report = await AnalyticsService.BuildReportAsync(_context, null, days);   // null = all clients

            ViewData["Layout"] = "~/Views/Shared/_SuperAdminLayout.cshtml";
            ViewData["LoadChartJs"] = true;       // the admin layout doesn't include Chart.js
            ViewData["Scope"] = "all clients";
            ViewData["ShowTrainer"] = true;
            ViewData["BaseUrl"] = "/superadmin/analytics";
            ViewData["ExportUrl"] = $"/superadmin/analytics/export?days={report.Days}";
            return View("~/Views/TrainerAnalytics/Reports.cshtml", report);
        }

        /// <summary>GET /superadmin/analytics/export?days=30 - downloads the report as CSV.</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(int days = 30)
        {
            var report = await AnalyticsService.BuildReportAsync(_context, null, days);
            var bytes = AnalyticsService.ToCsv(report, "HealthTrack - System Analytics Report");
            return File(bytes, "text/csv", $"system_analytics_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }
    }
}
