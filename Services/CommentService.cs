namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class CommentService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public CommentService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<Comment> CreateAsync(int postId, int authorId, string body, int? parentCommentId = null, CancellationToken ct = default)
        => _safe.ExecuteAsync<Comment>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var post = await db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Post not found.");
            var topic = await db.Topics.FirstAsync(t => t.Id == post.TopicId, ct);
            if (topic.IsLocked) throw new InvalidOperationException("Topic is locked.");

            var authorExists = await db.AppUsers.AnyAsync(u => u.Id == authorId && !u.IsDeleted, ct);
            if (!authorExists) throw new KeyNotFoundException("Author not found.");

            if (parentCommentId.HasValue)
            {
                var parent = await db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentCommentId && !c.IsDeleted, ct)
                    ?? throw new KeyNotFoundException("Parent comment not found.");
                if (parent.PostId != postId) throw new InvalidOperationException("Parent must belong to the same post.");
                if (parent.ParentCommentId.HasValue) throw new InvalidOperationException("Only one nested level is allowed.");
            }

            var cmt = new Comment
            {
                PostId = postId,
                AuthorId = authorId,
                ParentCommentId = parentCommentId,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Comments.Add(cmt);
            if (post.AuthorId != authorId) // ne treba mi notif za samog sebe
                db.Notifications.Add(
                    Notification.ForPostComment(postId, post.AuthorId, post.Title, body));
            topic.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return cmt;
        }, "CommentService.CreateAsync", new { postId, authorId, parentCommentId }, ct);

    public Task<Comment> ReplyAsync(int parentCommentId, int authorId, string body, CancellationToken ct = default)
        => _safe.ExecuteAsync<Comment>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var parent = await db.Comments
                             .AsNoTracking()
                             .FirstOrDefaultAsync(c => c.Id == parentCommentId && !c.IsDeleted, ct)
                         ?? throw new KeyNotFoundException("Parent comment not found.");

            var post = await db.Posts
                           .AsNoTracking()
                           .FirstOrDefaultAsync(p => p.Id == parent.PostId && !p.IsDeleted, ct)
                       ?? throw new KeyNotFoundException("Post not found.");

            var topic = await db.Topics.FirstAsync(t => t.Id == post.TopicId, ct);
            if (topic.IsLocked) throw new InvalidOperationException("Topic is locked.");

            var authorExists = await db.AppUsers.AnyAsync(u => u.Id == authorId && !u.IsDeleted, ct);
            if (!authorExists) throw new KeyNotFoundException("Author not found.");

            // dozvoljena je samo 1 nivo u dubinu
            if (parent.ParentCommentId.HasValue)
                throw new InvalidOperationException("Only one nested level is allowed.");

            var reply = new Comment
            {
                PostId = parent.PostId,
                AuthorId = authorId,
                ParentCommentId = parentCommentId,
                Body = body,
                CreatedAt = DateTime.UtcNow
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Comments.Add(reply);
            if (parent.AuthorId != authorId) //ne treba mi za samog sebe
                db.Notifications.Add(
                    Notification.ForCommentReply(parentCommentId, parent.AuthorId, body));

            topic.LastActivityAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return reply;
        }, "CommentService.ReplyAsync", new { parentCommentId, authorId }, ct);

    
    public Task<Comment?> GetByIdAsync(int id, int? userId = null, CancellationToken ct = default)
        => _safe.ExecuteAsync<Comment?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var row = await db.Comments.AsNoTracking()
                .Where(c => c.Id == id && !c.IsDeleted)
                .Select(c => new
                {
                    Comment = c,
                    Score = db.Votes
                        .Where(v => v.TargetType == ContentType.Comment && v.TargetId == c.Id)
                        .Select(v => (int?)v.Value)
                        .Sum() ?? 0,
                    MyVote = userId == null
                        ? 0
                        : db.Votes
                            .Where(v => v.TargetType == ContentType.Comment && v.TargetId == c.Id && v.UserId == userId.Value)
                            .Select(v => v.Value)
                            .FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);

            if (row is null) return null;

            row.Comment.Score = row.Score;
            row.Comment.MyVote = row.MyVote;
            return row.Comment;
        }, "CommentService.GetByIdAsync", new { id, userId }, ct);


    public Task<IReadOnlyList<Comment>> GetFlatForPostAsync(
        int postId,
        int take = 200,
        int skip = 0,
        int? userId = null,
        CancellationToken ct = default)
        => _safe.ExecuteAsync<IReadOnlyList<Comment>>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var q = db.Comments.AsNoTracking()
                .Include(c => c.Author)
                .Where(c => c.PostId == postId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt);

            var rows = await q
                .Skip(skip)
                .Take(take)
                .Select(c => new
                {
                    Comment = c,
                    Score = db.Votes
                        .Where(v => v.TargetType == ContentType.Comment && v.TargetId == c.Id)
                        .Select(v => (int?)v.Value)
                        .Sum() ?? 0,
                    MyVote = userId == null
                        ? 0
                        : db.Votes
                            .Where(v => v.TargetType == ContentType.Comment && v.TargetId == c.Id && v.UserId == userId.Value)
                            .Select(v => v.Value)
                            .FirstOrDefault()
                })
                .ToListAsync(ct);

            foreach (var r in rows)
            {
                r.Comment.Score = r.Score;
                r.Comment.MyVote = r.MyVote;
            }

            return rows.Select(r => r.Comment).ToList();
        }, "CommentService.GetFlatForPostAsync", new { postId, take, skip, userId }, ct);


    public Task<IReadOnlyList<Comment>> GetChildrenAsync(int parentId, int? userId = null, CancellationToken ct = default)
        => _safe.ExecuteAsync<IReadOnlyList<Comment>>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var q = db.Comments.AsNoTracking()
                .Include(c => c.Author)
                .Where(c => c.ParentCommentId == parentId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt);

            var rows = await q
                .Select(c => new
                {
                    Comment = c,
                    Score = db.Votes
                        .Where(v => v.TargetType == ContentType.Comment && v.TargetId == c.Id)
                        .Select(v => (int?)v.Value)
                        .Sum() ?? 0,
                    MyVote = userId == null
                        ? 0
                        : db.Votes
                            .Where(v => v.TargetType == ContentType.Comment && v.TargetId == c.Id && v.UserId == userId.Value)
                            .Select(v => (int)v.Value)
                            .FirstOrDefault()
                })
                .ToListAsync(ct);

            foreach (var r in rows)
            {
                r.Comment.Score = r.Score;
                r.Comment.MyVote = r.MyVote;
            }

            return rows.Select(r => r.Comment).ToList();
        }, "CommentService.GetChildrenAsync", new { parentId, userId }, ct);



    public Task<Comment> UpdateAsync(int id, string body, CancellationToken ct = default)
        => _safe.ExecuteAsync<Comment>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var c = await db.Comments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Comment not found.");
            c.Body = body;
            c.UpdatedAt = DateTime.UtcNow;

            var post = await db.Posts.AsNoTracking().FirstAsync(p => p.Id == c.PostId, ct);
            var topic = await db.Topics.FirstAsync(t => t.Id == post.TopicId, ct);
            topic.LastActivityAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return c;
        }, "CommentService.UpdateAsync", new { id }, ct);

    public Task SoftDeleteAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var c = await db.Comments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Comment not found.");
            c.IsDeleted = true;

            var children = await db.Comments.Where(x => x.ParentCommentId == id && !x.IsDeleted).ToListAsync(ct);
            foreach (var ch in children) ch.IsDeleted = true;

            await db.SaveChangesAsync(ct);
        }, "CommentService.SoftDeleteAsync", new { id }, ct);
}
