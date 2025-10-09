using System.ComponentModel.DataAnnotations;

namespace LevelForum.Data.Entities;

public class AppUser : _IdentifiableEntity
{
    [MinLength(4)]
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }

    public AppRole GlobalRole { get; set; }
    public List<AppUserTopicRole> UserTopicRoles { get; set; }
    
    public int Experience { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public string? AvatarUrl { get; set; }
}