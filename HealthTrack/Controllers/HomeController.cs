using HealthTrack.Models;
using HealthTrack.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace HealthTrack.Controllers
{
    public class HomeController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(HealthTrackContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Get statistics from database
                var totalUsers = await _context.Users.CountAsync();
                var totalClients = await _context.ClientProfiles.CountAsync();
                var totalRoles = await _context.Roles.CountAsync();
                var recentAuditLogs = await _context.AuditLogs
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .ToListAsync();

                // Pass data to view
                ViewBag.TotalUsers = totalUsers;
                ViewBag.TotalClients = totalClients;
                ViewBag.TotalRoles = totalRoles;
                ViewBag.RecentLogs = recentAuditLogs;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                ViewBag.TotalUsers = 0;
                ViewBag.TotalClients = 0;
                ViewBag.TotalRoles = 0;
                ViewBag.RecentLogs = new List<AuditLog>();
                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
