namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class TopicFollowService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public TopicFollowService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<bool> FollowAsync(int userId, int topicId, CancellationToken ct = default)
        => _safe.ExecuteAsync<bool>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var exists = await db.TopicFollows.AnyAsync(f => f.UserId == userId && f.TopicId == topicId, ct);
            if (exists) return false;

            var okUser = await db.AppUsers.AnyAsync(u => u.Id == userId && !u.IsDeleted, ct);
            var okTopic = await db.Topics.AnyAsync(t => t.Id == topicId && !t.IsDeleted, ct);
            if (!okUser || !okTopic) throw new KeyNotFoundException("User or topic not found.");

            db.TopicFollows.Add(new TopicFollow { UserId = userId, TopicId = topicId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            return true;
        }, "TopicFollowService.FollowAsync", new { userId, topicId }, ct);

    public Task<bool> UnfollowAsync(int userId, int topicId, CancellationToken ct = default)
        => _safe.ExecuteAsync<bool>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var f = await db.TopicFollows.FirstOrDefaultAsync(x => x.UserId == userId && x.TopicId == topicId, ct);
            if (f is null) return false;
            db.TopicFollows.Remove(f);
            await db.SaveChangesAsync(ct);
            return true;
        }, "TopicFollowService.UnfollowAsync", new { userId, topicId }, ct);

    public Task<bool> IsFollowingAsync(int userId, int topicId, CancellationToken ct = default)
        => _safe.ExecuteAsync<bool>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.TopicFollows.AnyAsync(f => f.UserId == userId && f.TopicId == topicId, ct);
        }, "TopicFollowService.IsFollowingAsync", new { userId, topicId }, ct);

    public Task<IReadOnlyList<int>> GetFollowedTopicIdsAsync(int userId, CancellationToken ct = default)
        => _safe.ExecuteAsync<IReadOnlyList<int>>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var ids = await db.TopicFollows.AsNoTracking()
                .Where(f => f.UserId == userId)
                .Select(f => f.TopicId)
                .ToListAsync(ct);
            return ids;
        }, "TopicFollowService.GetFollowedTopicIdsAsync", new { userId }, ct);
}
