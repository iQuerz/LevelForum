namespace LevelForum.Data.Services;

using System.Text.Json;
using LevelForum.Data.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class SafeExecutor
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public SafeExecutor(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, string operation, object? context = null, CancellationToken ct = default)
    {
        try
        {
            await action(ct);
        }
        catch (Exception ex)
        {
            await LogAsync(ex, operation, context, ct);
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, string operation, object? context = null, CancellationToken ct = default)
    {
        try
        {
            return await action(ct);
        }
        catch (Exception ex)
        {
            await LogAsync(ex, operation, context, ct);
            throw;
        }
    }

    private async Task LogAsync(Exception ex, string op, object? ctx, CancellationToken ct)
    {
        try
        {
            await using var _context = await _factory.CreateDbContextAsync(ct);
            _context.AppErrors.Add(new AppError
            {
                Source = op,
                Message = ex.Message,
                StackTrace = ex.StackTrace,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            // nikada ne bacaj iz logger-a
        }
    }
}