using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VsrSalesEngineWebApi.Security;

/// <summary>
/// Prohibits guest-mode tokens from accessing mutating, real-send, or sensitive operational endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class DisallowGuestAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var isGuest = user.FindFirst("is_guest")?.Value;

        if (string.Equals(isGuest, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new
            {
                message = "Guest mode has read-only/demonstration access. Real sending, integration modifications, or privileged actions are prohibited.",
                code = "GUEST_ACCESS_DENIED"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}

