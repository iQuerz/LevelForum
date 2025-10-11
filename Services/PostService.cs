namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class PostService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public PostService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<Post> CreateAsync(int topicId, int authorId, string title, string body, CancellationToken ct = default)
        => _safe.ExecuteAsync<Post>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var topic = await db.Topics.FirstOrDefaultAsync(t => t.Id == topicId && !t.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Topic not found.");
            if (topic.IsLocked) throw new InvalidOperationException("Topic is locked.");

            var author = await db.AppUsers.AnyAsync(u => u.Id == authorId && !u.IsDeleted, ct);
            if (!author) throw new KeyNotFoundException("Author not found.");

            var post = new Post
            {
                TopicId = topicId,
                AuthorId = authorId,
                Title = title,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Posts.Add(post);
            topic.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return post;
        }, "PostService.CreateAsync", new { topicId, authorId }, ct);

    public Task<Post?> GetByIdAsync(int id, int? userId = null, CancellationToken ct = default)
        => _safe.ExecuteAsync<Post?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var row = await db.Posts.AsNoTracking()
                .Include(p => p.Topic)
                .Include(p => p.Author)
                .Where(p => p.Id == id && !p.IsDeleted)
                .Select(p => new
                {
                    Post = p,
                    Score = db.Votes
                        .Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id)
                        .Select(v => (int?)v.Value)
                        .Sum() ?? 0,
                    MyVote = userId == null
                        ? 0
                        : db.Votes
                            .Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id && v.UserId == userId.Value)
                            .Select(v => v.Value)
                            .FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);

            if (row is null) return null;

            row.Post.Score = row.Score;
            row.Post.MyVote = row.MyVote;
            return row.Post;
        }, "PostService.GetByIdAsync", new { id, userId }, ct);




    public Task<PagedResult<Post>> QueryByTopicAsync(
    int topicId,
    string? titleQuery,
    string sort = "new",
    int page = 1,
    int pageSize = 20,
    int? userId = null,
    CancellationToken ct = default)
    => _safe.ExecuteAsync<PagedResult<Post>>(async ct =>
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var q = db.Posts.AsNoTracking()
            .Include(p => p.Topic)
            .Include(p => p.Author)
            .Where(p => p.TopicId == topicId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(titleQuery))
        {
            var pattern = $"%{titleQuery}%";
            q = q.Where(p => EF.Functions.Like(p.Title, pattern));
        }

        q = sort switch
        {
            "top" => q.OrderByDescending(p =>
                        db.Votes.Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id)
                                .Select(v => (int?)v.Value)
                                .Sum() ?? 0)
                     .ThenByDescending(p => p.CreatedAt),
            "active" => q.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt),
            _ => q.OrderByDescending(p => p.CreatedAt)
        };

        var total = await q.CountAsync(ct);

        var rows = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                Post = p,
                Score = db.Votes
                    .Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id)
                    .Select(v => (int?)v.Value)
                    .Sum() ?? 0,
                MyVote = userId == null
                    ? 0
                    : db.Votes
                        .Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id && v.UserId == userId.Value)
                        .Select(v => v.Value)
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows.Select(r =>
        {
            r.Post.Score = r.Score;
            r.Post.MyVote = r.MyVote;
            return r.Post;
        }).ToList();

        return new PagedResult<Post> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }, "PostService.QueryByTopicAsync", new { topicId, titleQuery, sort, page, pageSize, userId }, ct);



    public Task<PagedResult<Post>> QueryAsync(
    string? titleQuery,
    int? userId = null,
    string sort = "new",
    int page = 1,
    int pageSize = 20,
    CancellationToken ct = default)
    => _safe.ExecuteAsync<PagedResult<Post>>(async ct =>
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var followedTopicIds = userId is null
            ? new List<int>()
            : await db.TopicFollows.Where(f => f.UserId == userId)
                .Select(f => f.TopicId)
                .ToListAsync(ct);

        var q = db.Posts.AsNoTracking()
            .Include(p => p.Topic)
            .Include(p => p.Author)
            .Where(p => !p.IsDeleted && !p.Topic.IsDeleted
                                     && (!followedTopicIds.Any() || followedTopicIds.Contains(p.TopicId)));

        if (!string.IsNullOrWhiteSpace(titleQuery))
        {
            var pattern = $"%{titleQuery}%";
            q = q.Where(p => EF.Functions.Like(p.Title, pattern));
        }

        q = sort switch
        {
            "top" => q.OrderByDescending(p =>
                        db.Votes.Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id)
                                .Select(v => (int?)v.Value)
                                .Sum() ?? 0)
                     .ThenByDescending(p => p.CreatedAt),
            "active" => q.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt),
            _ => q.OrderByDescending(p => p.CreatedAt)
        };

        var total = await q.CountAsync(ct);

        var rows = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                Post = p,
                Score = db.Votes
                    .Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id)
                    .Select(v => (int?)v.Value)
                    .Sum() ?? 0,
                MyVote = userId == null
                    ? 0
                    : db.Votes
                        .Where(v => v.TargetType == ContentType.Post && v.TargetId == p.Id && v.UserId == userId.Value)
                        .Select(v => v.Value)
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows.Select(r =>
        {
            r.Post.Score = r.Score;
            r.Post.MyVote = r.MyVote;
            return r.Post;
        }).ToList();

        return new PagedResult<Post>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }, "PostService.QueryAsync", new { titleQuery, userId, sort, page, pageSize }, ct);


    
    public Task<Post> UpdateAsync(int id, string? title, string? body, CancellationToken ct = default)
        => _safe.ExecuteAsync<Post>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var p = await db.Posts.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Post not found.");
            if (!string.IsNullOrWhiteSpace(title)) p.Title = title;
            if (body != null) p.Body = body;
            p.UpdatedAt = DateTime.UtcNow;

            var t = await db.Topics.FirstAsync(x => x.Id == p.TopicId, ct);
            t.LastActivityAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return p;
        }, "PostService.UpdateAsync", new { id }, ct);

    public Task SoftDeleteAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var p = await db.Posts.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Post not found.");
            p.IsDeleted = true;

            var comments = await db.Comments.Where(c => c.PostId == id && !c.IsDeleted).ToListAsync(ct);
            foreach (var c in comments) c.IsDeleted = true;

            await db.SaveChangesAsync(ct);
        }, "PostService.SoftDeleteAsync", new { id }, ct);
}
