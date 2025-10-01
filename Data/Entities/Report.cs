using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class Report : _IdentifiableEntity
{
    public ContentType TargetType { get; set; } // Post/Comment
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; } // odgovor na prijavu
    public ReportStatus Status { get; set; } = ReportStatus.Open;

    
    [ForeignKey(nameof(ReporterId))]
    public AppUser Reporter { get; set; } = null!;
    public int ReporterId { get; set; }


    [ForeignKey(nameof(ReviewedById))]
    public AppUser? ReviewedBy { get; set; }
    public int? ReviewedById { get; set; }

}