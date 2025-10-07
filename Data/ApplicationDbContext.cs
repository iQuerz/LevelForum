using LevelForum.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LevelForum.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppUserTopicRole> AppUserTopicRoles => Set<AppUserTopicRole>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<TopicFollow> TopicFollows => Set<TopicFollow>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AppError> AppErrors => Set<AppError>();
    
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Vote
        b.Entity<Vote>()
            .Property(x => x.TargetType)
            .HasConversion<string>();

        // Report
        b.Entity<Report>()
            .Property(x => x.TargetType)
            .HasConversion<string>();
        b.Entity<Report>()
            .Property(x => x.Status)
            .HasConversion<string>();
    }
}