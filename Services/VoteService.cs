namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class VoteService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public VoteService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<int> ToggleVoteAsync(ContentType targetType, int targetId, int userId, int newVote, CancellationToken ct = default)
        => _safe.ExecuteAsync<int>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            if (newVote is not (-1 or 0 or 1))
                throw new ArgumentOutOfRangeException(nameof(newVote), "Vote must be -1, 0 or +1.");

            var targetExists = targetType switch
            {
                ContentType.Post => await db.Posts.AnyAsync(p => p.Id == targetId && !p.IsDeleted, ct),
                ContentType.Comment => await db.Comments.AnyAsync(c => c.Id == targetId && !c.IsDeleted, ct),
                _ => false
            };
            if (!targetExists) throw new KeyNotFoundException("Target not found.");

            var vote = await db.Votes.FirstOrDefaultAsync(v => v.TargetType == targetType && v.TargetId == targetId && v.UserId == userId, ct);
            if (vote is null)
            {
                if (newVote != 0)
                {
                    db.Votes.Add(new Vote
                    {
                        TargetType = targetType,
                        TargetId = targetId,
                        UserId = userId,
                        Value = (short)newVote,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                if (newVote == 0)
                {
                    db.Votes.Remove(vote);
                }
                else
                {
                    vote.Value = (short)newVote;
                }
            }

            await db.SaveChangesAsync(ct);
            var sum = await db.Votes
                .Where(v => v.TargetType == targetType && v.TargetId == targetId)
                .Select(v => (int?)v.Value)
                .SumAsync(ct);
            return sum ?? 0;
        }, "VoteService.ToggleVoteAsync", new { targetType, targetId, userId, newVote }, ct);

    public Task<int> GetScoreAsync(ContentType targetType, int targetId, CancellationToken ct = default)
        => _safe.ExecuteAsync<int>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var sum = await db.Votes
                .Where(v => v.TargetType == targetType && v.TargetId == targetId)
                .Select(v => (int?)v.Value)
                .SumAsync(ct);
            return sum ?? 0;
        }, "VoteService.GetScoreAsync", new { targetType, targetId }, ct);

    public Task<int?> GetUserVoteAsync(ContentType targetType, int targetId, int userId, CancellationToken ct = default)
        => _safe.ExecuteAsync<int?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var v = await db.Votes.AsNoTracking()
                .FirstOrDefaultAsync(v => v.TargetType == targetType && v.TargetId == targetId && v.UserId == userId, ct);
            return v is null ? null : v.Value;
        }, "VoteService.GetUserVoteAsync", new { targetType, targetId, userId }, ct);
}
