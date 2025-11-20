using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace OcufiiAPI.Handler
{
    public class SameUserOrAdminHandler : AuthorizationHandler<SameUserOrAdminRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SameUserOrAdminHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SameUserOrAdminRequirement requirement)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return Task.CompletedTask;

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = context.User.IsInRole("admin");

            if (isAdmin)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            string? routeUserId = null;
            foreach (var key in new[] { "id", "userId", "userid", "UserId", "Id" })
            {
                if (httpContext.Request.RouteValues.TryGetValue(key, out var value) && value != null)
                {
                    routeUserId = value.ToString();
                    break;
                }
            }

            if (routeUserId != null &&
                Guid.TryParse(routeUserId, out var routeGuid) &&
                routeGuid.ToString() == userIdClaim)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
