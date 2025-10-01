using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class AppUserTopicRole : _IdentifiableEntity
{
    public AppRole TopicRole { get; set; }
    
    [ForeignKey(nameof(AppUserId))]
    public AppUser User { get; set; }
    public int AppUserId { get; set; }
    
    [ForeignKey(nameof(TopicId))]
    public Topic Topic { get; set; }
    public int TopicId { get; set; }
}