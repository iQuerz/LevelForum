using Microsoft.EntityFrameworkCore;

namespace LevelForum.Data.Entities;

[PrimaryKey(nameof(Id))]
public class _IdentifiableEntity
{
    public int Id { get; set; }
}