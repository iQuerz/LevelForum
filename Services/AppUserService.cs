namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

public sealed class AppUserService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public AppUserService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<AppUser> CreateAsync(string username, string email, string passwordHash, AppRole role = AppRole.User, CancellationToken ct = default)
        => _safe.ExecuteAsync<AppUser>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            if (await db.AppUsers.AnyAsync(u => u.Username == username, ct))
                throw new InvalidOperationException("Username already taken.");
            if (await db.AppUsers.AnyAsync(u => u.Email == email, ct))
                throw new InvalidOperationException("Email already in use.");

            var user = new AppUser
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                GlobalRole = role,
                Experience = 0,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(ct);
            return user;
        }, "AppUserService.CreateAsync", new { username, email }, ct);

    public Task<AppUser?> GetByIdAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync<AppUser?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);
        }, "AppUserService.GetByIdAsync", new { id }, ct);

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _safe.ExecuteAsync<AppUser?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted, ct);
        }, "AppUserService.GetByUsernameAsync", new { username }, ct);

    public Task<AppUser> UpdateAsync(int id, string? email = null, string? avatarUrl = null, CancellationToken ct = default)
        => _safe.ExecuteAsync<AppUser>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("User not found.");

            if (!string.IsNullOrWhiteSpace(email))
            {
                var exists = await db.AppUsers.AnyAsync(u => u.Email == email && u.Id != id, ct);
                if (exists) throw new InvalidOperationException("Email already in use.");
                user.Email = email;
            }
            if (avatarUrl != null) user.AvatarUrl = avatarUrl;

            await db.SaveChangesAsync(ct);
            return user;
        }, "AppUserService.UpdateAsync", new { id, email, avatarUrl }, ct);

    public Task ChangeUsernameAsync(int id, string newUsername, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("User not found.");

            // jednostavna validacija; dodatno možeš dodati regex po želji
            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 4)
                throw new InvalidOperationException("Username must be at least 4 characters.");

            if (await db.AppUsers.AnyAsync(u => u.Username == newUsername && u.Id != id, ct))
                throw new InvalidOperationException("Username already taken.");

            user.Username = newUsername.Trim();
            await db.SaveChangesAsync(ct);
        }, "AppUserService.ChangeUsernameAsync", new { id, newUsername }, ct);

    public Task SetPasswordHashAsync(int id, string newPasswordHash, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("User not found.");
            user.PasswordHash = newPasswordHash;
            await db.SaveChangesAsync(ct);
        }, "AppUserService.SetPasswordHashAsync", new { id, newPasswordHash }, ct);

    public Task AddExperienceAsync(int id, int delta, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("User not found.");
            user.Experience += delta;
            await db.SaveChangesAsync(ct);
        }, "AppUserService.AddExperienceAsync", new { id, delta }, ct);

    public Task SoftDeleteAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("User not found.");
            user.IsDeleted = true;
            await db.SaveChangesAsync(ct);
        }, "AppUserService.SoftDeleteAsync", new { id }, ct);
}
