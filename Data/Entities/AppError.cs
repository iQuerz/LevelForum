namespace LevelForum.Data.Entities;

public class AppError : _IdentifiableEntity
{
    public string Source { get; set; } = string.Empty;   // npr. "PostService.CreateAsync"
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}