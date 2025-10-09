using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class AppUserTopicRole : _IdentifiableEntity
{
    public AppRole TopicRole { get; set; }
    
    
    public AppUser AppUser { get; set; }
    public int AppUserId { get; set; }
    
    
    
    public Topic Topic { get; set; }
    public int TopicId { get; set; }
}