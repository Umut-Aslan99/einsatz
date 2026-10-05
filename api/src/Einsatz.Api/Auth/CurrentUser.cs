using System.Security.Claims;
using Einsatz.Domain;

namespace Einsatz.Api.Auth;

/// <summary>The signed-in user, read from the auth cookie.</summary>
public sealed class CurrentUser(IHttpContextAccessor http)
{
    private ClaimsPrincipal Principal => http.HttpContext?.User ?? new ClaimsPrincipal();
    public Guid Id => Guid.Parse(Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
    public string Name => Principal.FindFirstValue(ClaimTypes.Name) ?? "";
    public bool IsIn(Role role) => Principal.IsInRole(role.ToString());
}
