using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Authorization
{
    /// <summary>
    /// Custom authorization attribute to enforce permission-based access control.
    /// Usage: [HasPermission("Module.Action")] on controller actions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _requiredPermission;

        public HasPermissionAttribute(string requiredPermission)
        {
            _requiredPermission = requiredPermission;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // If user not authenticated, deny
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Try to get user ID from claims
            var userIdClaim = user.FindFirst("userId") ?? user.FindFirst("sub") ?? user.FindFirst("nameid");
            if (userIdClaim == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            try
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<HealthTrackContext>();
                
                // Get user with role
                var userRecord = await dbContext.Users
                    .Include(u => u.Role)
                    .ThenInclude(r => r!.Permissions)
                    .FirstOrDefaultAsync(u => u.UserID == userId);

                if (userRecord?.Role == null)
                {
                    context.Result = new ForbidResult();
                    return;
                }

                // Check if any of the user's roles have the required permission
                var hasPermission = userRecord.Role.Permissions
                    .Any(p => (p.Module + "." + p.PermissionName).Equals(_requiredPermission, StringComparison.OrdinalIgnoreCase));

                if (!hasPermission)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }
            catch (Exception ex)
            {
                // Log error and deny access
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<HasPermissionAttribute>>();
                logger.LogError(ex, "Error checking permission");
                context.Result = new UnauthorizedResult();
            }
        }
    }
}
