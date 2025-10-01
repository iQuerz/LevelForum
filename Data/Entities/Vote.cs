using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class Vote : _IdentifiableEntity
{
    public ContentType TargetType { get; set; } // Post ili Comment
    public int TargetId { get; set; }           // Id posta ili komentara (nema nav. zbog generičkog tipa)

    [ForeignKey(nameof(UserId))]
    public AppUser User { get; set; } = null!;
    public int UserId { get; set; }

    public short Value { get; set; } //+1 ili -1, cuvamo kao int da lakse racunamo kada povlacimo podatke

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}