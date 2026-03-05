using System.Security.Claims;

namespace OcufiiAPI.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.Parse(id ?? throw new InvalidOperationException("User ID not found in claims"));
        }

        public static Guid GetResellerId(this ClaimsPrincipal user)
        {
            var resellerIdClaim = user.FindFirst("reseller_id")?.Value;
            return string.IsNullOrEmpty(resellerIdClaim)
                ? Guid.Empty
                : Guid.Parse(resellerIdClaim);
        }
    }
}
