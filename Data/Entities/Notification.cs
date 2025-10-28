namespace LevelForum.Data.Entities;

public class Notification : _IdentifiableEntity
{
    public ContentType TargetType { get; set; }
    
    public int TargetId { get; set; }
    
    public DateTime Date { get; set; }
    
    public string Message { get; set; }
}