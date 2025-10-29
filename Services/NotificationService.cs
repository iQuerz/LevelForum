using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LevelForum.Data.Services;

public class NotificationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public NotificationService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<List<Notification>> GetUserNotificationsAsync(int userId, CancellationToken ct = default)
        => _safe.ExecuteAsync<List<Notification>>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var since = DateTime.UtcNow.AddDays(-7); // gledamo samo poslednjih nedelju dana da ne preuzmemo previse notifs iz baze

            var list = await db.Notifications
                .AsNoTracking()
                .Where(n => n.UserTargetId == userId && n.Date >= since)
                .OrderByDescending(n => n.Date)
                .ToListAsync(ct);

            return list;
        }, "NotificationService.GetUserNotificationsAsync", new { userId }, ct);

}