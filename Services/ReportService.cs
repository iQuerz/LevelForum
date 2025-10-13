namespace LevelForum.Data.Services;

using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class ReportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly SafeExecutor _safe;

    public ReportService(IDbContextFactory<ApplicationDbContext> factory, SafeExecutor safe)
    {
        _factory = factory;
        _safe = safe;
    }

    public Task<Report> CreateAsync(int reporterId, ContentType targetType, int targetId, string reason, CancellationToken ct = default)
        => _safe.ExecuteAsync<Report>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var userOk = await db.AppUsers.AnyAsync(u => u.Id == reporterId && !u.IsDeleted, ct);
            if (!userOk) throw new KeyNotFoundException("Reporter not found.");

            var targetOk = targetType switch
            {
                ContentType.Post => await db.Posts.AnyAsync(p => p.Id == targetId && !p.IsDeleted, ct),
                ContentType.Comment => await db.Comments.AnyAsync(c => c.Id == targetId && !c.IsDeleted, ct),
                _ => false
            };
            if (!targetOk) throw new KeyNotFoundException("Target not found.");

            var rep = new Report
            {
                ReporterId = reporterId,
                TargetType = targetType,
                TargetId = targetId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow,
                Status = ReportStatus.Open
            };
            db.Reports.Add(rep);
            await db.SaveChangesAsync(ct);
            return rep;
        }, "ReportService.CreateAsync", new { reporterId, targetType, targetId }, ct);

    public Task<PagedResult<Report>> QueryAsync(string? status = null, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => _safe.ExecuteAsync<PagedResult<Report>>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var q = db.Reports.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                if (Enum.TryParse<ReportStatus>(status.Replace(" ", ""), ignoreCase: true, out var st))
                    q = q.Where(r => r.Status == st);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";
                q = q.Where(r => EF.Functions.Like(r.Reason, pattern));
            }

            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(r => r.CreatedAt)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

            return new PagedResult<Report> { Items = items, Total = total, Page = page, PageSize = pageSize };
        }, "ReportService.QueryAsync", new { status, search, page, pageSize }, ct);

    public Task<Report?> GetByIdAsync(int id, CancellationToken ct = default)
        => _safe.ExecuteAsync<Report?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        }, "ReportService.GetByIdAsync", new { id }, ct);

    public Task ReviewAsync(int id, int reviewerId, string? note, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var r = await db.Reports.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Report not found.");
            r.Status = ReportStatus.Open;
            r.ReviewedById = reviewerId;
            r.ReviewedAt = DateTime.UtcNow;
            r.ReviewNote = note;
            await db.SaveChangesAsync(ct);
        }, "ReportService.ReviewAsync", new { id, reviewerId }, ct);

    public Task CloseAsync(int id, int reviewerId, string? note, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var r = await db.Reports.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("Report not found.");
            r.Status = ReportStatus.Closed;
            r.ReviewedById = reviewerId;
            r.ReviewedAt = DateTime.UtcNow;
            r.ReviewNote = note;
            await db.SaveChangesAsync(ct);
        }, "ReportService.CloseAsync", new { id, reviewerId }, ct);

    public Task DeleteTargetAsync(int reportId, int reviewerId, CancellationToken ct = default)
        => _safe.ExecuteAsync(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var r = await db.Reports.FirstOrDefaultAsync(x => x.Id == reportId, ct)
                ?? throw new KeyNotFoundException("Report not found.");

            if (r.TargetType == ContentType.Post)
            {
                var p = await db.Posts.FirstOrDefaultAsync(x => x.Id == r.TargetId && !x.IsDeleted, ct);
                if (p != null) p.IsDeleted = true;
            }
            else if (r.TargetType == ContentType.Comment)
            {
                var c = await db.Comments.FirstOrDefaultAsync(x => x.Id == r.TargetId && !x.IsDeleted, ct);
                if (c != null) c.IsDeleted = true;
            }

            var related = await db.Reports
                .Where(x => x.TargetType == r.TargetType && x.TargetId == r.TargetId && x.Status != ReportStatus.Closed)
                .ToListAsync(ct);

            foreach (var rr in related)
            {
                rr.Status = ReportStatus.Closed;
                rr.ReviewedById = reviewerId;
                rr.ReviewedAt = DateTime.UtcNow;
                rr.ReviewNote = $"Target removed via report #{reportId}.";
            }

            await db.SaveChangesAsync(ct);
        }, "ReportService.DeleteTargetAsync", new { reportId, reviewerId }, ct);
    
    public Task<(int PostId, int TopicId, string Snippet)?> GetTargetInfoAsync(int reportId, CancellationToken ct = default)
        => _safe.ExecuteAsync<(int, int, string)?>(async ct =>
        {
            await using var db = await _factory.CreateDbContextAsync(ct);

            var rep = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, ct);
            if (rep is null) return null;

            if (rep.TargetType == ContentType.Post)
            {
                var p = await db.Posts
                    .AsNoTracking()
                    .Include(x => x.Topic)
                    .FirstOrDefaultAsync(x => x.Id == rep.TargetId && !x.IsDeleted, ct);
                if (p is null) return null;
                var snip = string.IsNullOrWhiteSpace(p.Body) ? p.Title : p.Body;
                snip = snip?.Length > 320 ? snip[..320] + "…" : snip ?? "";
                return (p.Id, p.TopicId, snip);
            }
            else if (rep.TargetType == ContentType.Comment)
            {
                var c = await db.Comments
                    .AsNoTracking()
                    .Include(x => x.Post).ThenInclude(p => p.Topic)
                    .FirstOrDefaultAsync(x => x.Id == rep.TargetId && !x.IsDeleted, ct);
                if (c is null || c.Post is null) return null;
                var snip = c.Body?.Length > 320 ? c.Body[..320] + "…" : c.Body ?? "";
                return (c.PostId, c.Post.TopicId, snip);
            }

            return null;
        }, "ReportService.GetTargetInfoAsync", new { reportId }, ct);
}
