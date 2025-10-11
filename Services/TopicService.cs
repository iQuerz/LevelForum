namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class TopicService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public TopicService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<Topic> CreateAsync(string title, string? description, int createdByUserId, CancellationToken ct = default)
        => _safe.ExecuteAsync<Topic>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == createdByUserId && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Creator not found.");

            var topic = new Topic
            {
                Title = title,
                Description = description,
                CreatedById = createdByUserId,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Topics.Add(topic);
            await db.SaveChangesAsync(ct);

            db.AppUserTopicRoles.Add(new AppUserTopicRole
            {
                AppUserId = createdByUserId,
                TopicId = topic.Id,
                TopicRole = AppRole.Owner
            });
            db.TopicFollows.Add(new TopicFollow
            {
                UserId = createdByUserId,
                TopicId = topic.Id,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return topic;
        }, "TopicService.CreateAsync", new { title, createdByUserId }, ct);

    public Task<Topic?> GetByIdAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync<Topic?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Topics.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        }, "TopicService.GetByIdAsync", new { id }, ct);

    public Task<PagedResult<Topic>> SearchAsync(string? titleQuery, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => _safe.ExecuteAsync<PagedResult<Topic>>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var q = db.Topics.AsNoTracking().Where(t => !t.IsDeleted);
            if (!string.IsNullOrWhiteSpace(titleQuery))
            {
                var pattern = $"%{titleQuery}%";
                q = q.Where(t => EF.Functions.Like(t.Title, pattern));
            }

            var total = await q.CountAsync(ct);
            var items = await q.OrderBy(t => t.Title)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

            return new PagedResult<Topic> { Items = items, Total = total, Page = page, PageSize = pageSize };
        }, "TopicService.SearchAsync", new { titleQuery, page, pageSize }, ct);

    public Task<List<Topic>> GetFollowingAsync(int userId, CancellationToken ct = default)
    => _safe.ExecuteAsync<List<Topic>>(async ct =>
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        // Sve teme koje user prati, ali samo aktivne (neobrisane / nebanovane)
        var items = await db.TopicFollows
            .AsNoTracking()
            .Where(tf => tf.UserId == userId &&
                         !tf.Topic.IsDeleted &&
                         !tf.Topic.IsBanned)
            .OrderByDescending(tf => tf.Topic.LastActivityAt)
            .ThenBy(tf => tf.Topic.Title)
            .Select(tf => tf.Topic)
            .ToListAsync(ct);

        return items;
    }, "TopicService.GetFollowingAsync", new { userId }, ct);

public Task<List<Topic>> GetForSidebarAsync(int? userId = null, CancellationToken ct = default)
    => _safe.ExecuteAsync<List<Topic>>(async ct =>
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        const int TAKE = 15; // koliko prikazujemo u sidebaru

        // Osnovni skup: samo aktivne teme
        var topicsQ = db.Topics
            .AsNoTracking()
            .Where(t => !t.IsDeleted && !t.IsBanned);

        // Popularnost = broj follower-a (TopicFollows.Count za dati Topic)
        var popularityQ =
            from t in topicsQ
            join tf in db.TopicFollows.AsNoTracking() on t.Id equals tf.TopicId into g
            select new
            {
                Topic = t,
                Followers = g.Count()
            };

        if (userId is int uid)
        {
            // ako je prosleđen userId, predlagati teme koje user NE prati
            var followedIdsQ = db.TopicFollows
                .AsNoTracking()
                .Where(tf => tf.UserId == uid)
                .Select(tf => tf.TopicId);

            var suggestions = await popularityQ
                .Where(x => !followedIdsQ.Contains(x.Topic.Id))
                .OrderByDescending(x => x.Followers)
                .ThenByDescending(x => x.Topic.LastActivityAt)
                .ThenBy(x => x.Topic.Title)
                .Take(TAKE)
                .Select(x => x.Topic)
                .ToListAsync(ct);

            return suggestions;
        }
        else
        {
            // bez user-a: čisto najpopularnije teme
            var top = await popularityQ
                .OrderByDescending(x => x.Followers)
                .ThenByDescending(x => x.Topic.LastActivityAt)
                .ThenBy(x => x.Topic.Title)
                .Take(TAKE)
                .Select(x => x.Topic)
                .ToListAsync(ct);

            return top;
        }
    }, "TopicService.GetForSidebarAsync", new { userId }, ct);

    
    public Task<Topic> UpdateAsync(int id, string? title, string? description, CancellationToken ct = default)
        => _safe.ExecuteAsync<Topic>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var t = await db.Topics.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Topic not found.");
            if (!string.IsNullOrWhiteSpace(title)) t.Title = title;
            if (description != null) t.Description = description;
            await db.SaveChangesAsync(ct);
            return t;
        }, "TopicService.UpdateAsync", new { id }, ct);

    public Task LockAsync(int id, bool locked = true, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var t = await db.Topics.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Topic not found.");
            t.IsLocked = locked;
            await db.SaveChangesAsync(ct);
        }, "TopicService.LockAsync", new { id, locked }, ct);

    public Task SoftDeleteAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var t = await db.Topics.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Topic not found.");
            t.IsDeleted = true;
            await db.SaveChangesAsync(ct);
        }, "TopicService.SoftDeleteAsync", new { id }, ct);
}
