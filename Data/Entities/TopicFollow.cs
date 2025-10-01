using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class TopicFollow : _IdentifiableEntity
{
    [ForeignKey(nameof(UserId))]
    public AppUser User { get; set; } = null!;
    public int UserId { get; set; }

    [ForeignKey(nameof(TopicId))]
    public Topic Topic { get; set; } = null!;
    public int TopicId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}