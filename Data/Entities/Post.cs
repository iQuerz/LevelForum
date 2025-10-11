using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class Post : _IdentifiableEntity
{
    [ForeignKey(nameof(TopicId))]
    public Topic Topic { get; set; }
    public int TopicId { get; set; }
    
    [ForeignKey(nameof(AuthorId))]
    public AppUser Author { get; set; }
    public int AuthorId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    [NotMapped] public int Score { get; set; }
    [NotMapped] public int MyVote { get; set; }
}