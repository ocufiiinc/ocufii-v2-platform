using Microsoft.AspNetCore.Authorization;

namespace OcufiiAPI.Handler
{
    public class SameUserOrAdminRequirement : IAuthorizationRequirement
    {
    }
}
