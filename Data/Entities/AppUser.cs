using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LevelForum.Data.Entities;

public class AppUser : _IdentifiableEntity
{
    [MinLength(4)]
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }

    public AppRole GlobalRole { get; set; }
    public List<AppUserTopicRole> UserTopicRoles { get; set; }
    
    public int Experience { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public string? AvatarUrl { get; set; }
    
    // ---------- LEVELING (logaritamski) ----------
    // Parametri za "krivinu" levela:
    // baseB > 1 usporava rast; expScale pomera pragove.
    // Primer: Level 1 ~ 100 EXP, Level 5 ~ 1000 EXP, itd.
    public const double LevelBaseB = 1.7;   // baza logaritma (što veća -> sporiji rast levela)
    public const double ExpScale   = 100.0; // koliko EXP-a je "osnova" za niske levele

    [NotMapped]
    public int Level => LevelFromExp(Experience);

    [NotMapped]
    public int ExpForCurrentLevel => ExpForLevel(Level);

    [NotMapped]
    public int ExpForNextLevel => ExpForLevel(Level + 1);

    [NotMapped]
    public double ProgressToNext // 0..1
    {
        get
        {
            var cur = ExpForCurrentLevel;
            var nxt = ExpForNextLevel;
            if (nxt <= cur) return 1;
            var p = (Experience - cur) / (double)(nxt - cur);
            return Math.Clamp(p, 0, 1);
        }
    }

    // f(exp) = floor( log_{B}(exp/ExpScale + 1) )
    public static int LevelFromExp(int exp)
    {
        if (exp <= 0) return 0;
        var lvl = Math.Log(exp / ExpScale + 1.0, LevelBaseB);
        return (int)Math.Floor(lvl);
    }

    // Inverzna: Exp(lvl) = round( (B^{lvl} - 1) * ExpScale )
    public static int ExpForLevel(int level)
    {
        if (level <= 0) return 0;
        var exp = (Math.Pow(LevelBaseB, level) - 1.0) * ExpScale;
        return (int)Math.Round(exp);
    }
}