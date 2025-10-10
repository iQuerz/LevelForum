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
        
        // indeksi
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        b.Entity<AppUser>().HasIndex(u => u.Email).IsUnique();
        b.Entity<TopicFollow>().HasIndex(tf => new { tf.UserId, tf.TopicId }).IsUnique();
        b.Entity<Vote>().HasIndex(v => new { v.UserId, v.TargetType, v.TargetId }).IsUnique();

        // enumovi
        b.Entity<Vote>()
            .Property(x => x.TargetType)
            .HasConversion<string>();

        b.Entity<Report>()
            .Property(x => x.TargetType)
            .HasConversion<string>();
        b.Entity<Report>()
            .Property(x => x.Status)
            .HasConversion<string>();
        
        b.Entity<AppUser>()
            .Property(x => x.GlobalRole)
            .HasConversion<string>();
        b.Entity<AppUserTopicRole>()
            .Property(x => x.TopicRole)
            .HasConversion<string>();
        
        // Topic.CreatedBy: NE briši topike pri brisanju korisnika (razbija multiple-cascade putanju)
        b.Entity<Topic>()
            .HasOne(t => t.CreatedBy)
            .WithMany()
            .OnDelete(DeleteBehavior.NoAction);

        // AppUserTopicRole.Topic: brisanje topika briše njegove role
        b.Entity<AppUserTopicRole>()
            .HasOne(r => r.Topic)
            .WithMany(t => t.UserRoles)
            .OnDelete(DeleteBehavior.Cascade);

        // AppUserTopicRole.User: brisanje korisnika briše njegove role
        b.Entity<AppUserTopicRole>()
            .HasOne(r => r.AppUser)
            .WithMany(u => u.UserTopicRoles)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Post ↔ Author (ne briši postove kad se obriše korisnik)
        b.Entity<Post>()
            .HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.NoAction);

        // Comment ↔ Author (ne briši komentare kad se obriše korisnik)
                b.Entity<Comment>()
                    .HasOne(c => c.Author)
                    .WithMany()
                    .HasForeignKey(c => c.AuthorId)
                    .OnDelete(DeleteBehavior.NoAction);

        // Comment ↔ Post (brisanje posta briše njegove komentare)
                b.Entity<Comment>()
                    .HasOne(c => c.Post)
                    .WithMany()
                    .HasForeignKey(c => c.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

        // Comment ↔ ParentComment (bez kaskade)
                b.Entity<Comment>()
                    .HasOne(c => c.ParentComment)
                    .WithMany()
                    .HasForeignKey(c => c.ParentCommentId)
                    .OnDelete(DeleteBehavior.NoAction);

    }
}