using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class Topic : _IdentifiableEntity
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsLocked { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public bool IsBanned { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(CreatedById))]
    public AppUser? CreatedBy { get; set; } = null;
    public int? CreatedById { get; set; } = null;
    
    public List<AppUserTopicRole> UserRoles { get; set; }
}