using System.Security.Claims;

namespace BallastlaneTechStore.Store.Api.Common;

internal static class CurrentUser
{
    public static Guid Id(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing subject claim.");
        return Guid.Parse(sub);
    }
}
