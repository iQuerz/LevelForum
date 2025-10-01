using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class Comment : _IdentifiableEntity
{
    [ForeignKey(nameof(PostId))]
    public Post Post { get; set; }
    public int PostId { get; set; }
    
    [ForeignKey(nameof(AuthorId))]
    public AppUser Author { get; set; }
    public int AuthorId { get; set; }
    
    [ForeignKey(nameof(ParentCommentId))]
    public Comment? ParentComment { get; set; }
    public int? ParentCommentId { get; set; }

    
    public string Body { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
}