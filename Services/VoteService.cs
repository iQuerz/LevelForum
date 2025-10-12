namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class VoteService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    // Koliko EXP autor dobija po jednom upvote-u
    private const int ExpPerUpvote = 100;

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

            // U isto vreme pribavi i autora targeta (da bismo mu dodelili EXP)
            int? authorId = targetType switch
            {
                ContentType.Post    => await db.Posts
                    .Where(p => p.Id == targetId && !p.IsDeleted)
                    .Select(p => (int?)p.AuthorId)
                    .FirstOrDefaultAsync(ct),

                ContentType.Comment => await db.Comments
                    .Where(c => c.Id == targetId && !c.IsDeleted)
                    .Select(c => (int?)c.AuthorId)
                    .FirstOrDefaultAsync(ct),

                _ => null
            };

            if (authorId is null)
                throw new KeyNotFoundException("Target not found.");

            // Postojeći glas korisnika (ako postoji)
            var vote = await db.Votes
                .FirstOrDefaultAsync(v => v.TargetType == targetType && v.TargetId == targetId && v.UserId == userId, ct);

            var oldVote = vote?.Value ?? 0;
            var oldUp = oldVote == 1 ? 1 : 0;
            var newUp = newVote == 1 ? 1 : 0;
            var upDelta = newUp - oldUp; // -1, 0 ili +1

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

            // EXP: dodeli/oduzmi samo ako se menja "upvote status"
            // i ako korisnik ne glasa na sopstveni sadržaj.
            if (upDelta != 0 && authorId.Value != userId)
            {
                var author = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == authorId.Value, ct);
                if (author is not null)
                {
                    author.Experience = Math.Max(0, author.Experience + upDelta * ExpPerUpvote);
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
