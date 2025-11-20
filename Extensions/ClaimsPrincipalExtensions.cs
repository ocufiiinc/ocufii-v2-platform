using System.Security.Claims;

namespace OcufiiAPI.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value
                     ?? throw new InvalidOperationException("User ID claim is missing");

            return Guid.Parse(id);
        }

        public static bool IsInRole(this ClaimsPrincipal user, string role)
            => user.HasClaim(ClaimTypes.Role, role);
    }
}
