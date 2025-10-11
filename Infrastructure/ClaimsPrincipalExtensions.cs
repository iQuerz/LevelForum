using System.Security.Claims;
using LevelForum.Data.Entities;

namespace LevelForum.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static string Username(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public static int? UserId(this ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    
    public static string Email(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    
    public static AppRole Role(this ClaimsPrincipal principal)
        => Enum.TryParse(principal.FindFirstValue(ClaimTypes.Role), true, out AppRole role)
            ? role
            : AppRole.None;

    public static bool IsAuthenticated(this ClaimsPrincipal principal)
        => principal?.Identity?.IsAuthenticated ?? false;

    public static bool HasRole(this ClaimsPrincipal principal, AppRole role)
        => Enum.TryParse(principal?.FindFirstValue(ClaimTypes.Role), true, out AppRole r) && r == role;

    // Is the user's AppRole >= the required minimum? (relies on enum ordering)
    public static bool HasAtLeastRole(this ClaimsPrincipal principal, AppRole minimum)
        => Enum.TryParse(principal?.FindFirstValue(ClaimTypes.Role), true, out AppRole r) && r >= minimum;
}