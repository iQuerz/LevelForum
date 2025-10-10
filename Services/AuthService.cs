using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LevelForum.Data;
using LevelForum.Data.Entities;
using LevelForum.Data.Services;
using BC = BCrypt.Net.BCrypt;

public sealed class AuthService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly AppUserService _users;
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _cfg;
    private readonly SafeExecutor _safe;

    public AuthService(
        IDbContextFactory<ApplicationDbContext> factory,
        AppUserService users,
        IHttpContextAccessor http,
        IConfiguration cfg,
        SafeExecutor safe)
    {
        _factory = factory;
        _users = users;
        _http = http;
        _cfg = cfg;
        _safe = safe;
    }

    public Task<(bool ok, string? error)> RegisterAsync(string username, string email, string password, CancellationToken ct = default)
        => _safe.ExecuteAsync<(bool, string?)>(async ct =>
        {
            var pepper = _cfg["Security:PasswordPepper"];
            var wf = _cfg.GetValue<int?>("Security:BcryptWorkFactor") ?? 12;

            // materijal za hash (sa pepper-om ako postoji)
            var material = pepper is null ? password : password + pepper;

            // BCrypt hash (ugradjeni random salt)
            var hash = BC.HashPassword(material, workFactor: wf);

            var user = await _users.CreateAsync(username, email, hash, AppRole.User, ct);
            await SignInAsync(user, persistent: true);
            return (true, null);
        }, "AuthService.RegisterAsync", new { username, email }, ct);

    public Task<(bool ok, string? error)> LoginAsync(string usernameOrEmail, string password, bool rememberMe, CancellationToken ct = default)
        => _safe.ExecuteAsync<(bool, string?)>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(u =>
                !u.IsDeleted && (u.Username == usernameOrEmail || u.Email == usernameOrEmail), ct);

            if (user is null) return (false, "Invalid credentials.");

            var pepper = _cfg["Security:PasswordPepper"];
            var material = pepper is null ? password : password + pepper;

            // verifikacija
            if (!BC.Verify(material, user.PasswordHash))
                return (false, "Invalid credentials.");

            await SignInAsync(user, persistent: rememberMe);
            return (true, null);
        }, "AuthService.LoginAsync", new { usernameOrEmail }, ct);

    public Task LogoutAsync()
        => _safe.ExecuteAsync(async _ =>
        {
            var ctx = _http.HttpContext ?? throw new InvalidOperationException("No HttpContext.");
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }, "AuthService.LogoutAsync", null, default);

    private async Task SignInAsync(AppUser user, bool persistent)
    {
        var ctx = _http.HttpContext ?? throw new InvalidOperationException("No HttpContext.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.GlobalRole.ToString())
        };

        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(id);

        var props = new AuthenticationProperties
        {
            IsPersistent = persistent,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
        };

        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }
}
