namespace LevelForum.Data.Entities;

public class Notification : _IdentifiableEntity
{
    public ContentType TargetType { get; set; }
    
    public int TargetId { get; set; }
    
    public int UserTargetId { get; set; }
    
    public DateTime Date { get; set; }
    
    public string Message { get; set; }
    
    /// <summary>
    /// Notifikacija autoru posta kada neko ostavi root komentar.
    /// </summary>
    public static Notification ForPostComment(
        int postId,
        int postAuthorId,
        string postTitle,
        string commentBody)
        => new()
        {
            TargetType = ContentType.Post,
            TargetId = postId,
            UserTargetId = postAuthorId,
            Date = DateTime.UtcNow,
            Message = $"💬 New comment on your post \"{postTitle}\": {Preview(commentBody)}"
        };

    /// <summary>
    /// Notifikacija autoru komentara kada neko odgovori na njega.
    /// </summary>
    public static Notification ForCommentReply(
        int parentCommentId,
        int parentAuthorId,
        string replyBody)
        => new()
        {
            TargetType = ContentType.Comment,
            TargetId = parentCommentId,
            UserTargetId = parentAuthorId,
            Date = DateTime.UtcNow,
            Message = $"↩️ New reply to your comment: {Preview(replyBody)}"
        };

    /// <summary>
    /// Pomoćna za bilo koji custom slučaj.
    /// </summary>
    public static Notification Custom(
        ContentType targetType,
        int targetId,
        int userTargetId,
        string message)
        => new()
        {
            TargetType = targetType,
            TargetId = targetId,
            UserTargetId = userTargetId,
            Date = DateTime.UtcNow,
            Message = message
        };

    private static string Preview(string? body, int maxLen = 40)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        body = body.Trim();
        return body.Length <= maxLen ? body : body[..maxLen] + "…";
    }
}