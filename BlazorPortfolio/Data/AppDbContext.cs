using BlazorPortfolio.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorPortfolio.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<HiringMessage> HiringMessages => Set<HiringMessage>();
    public DbSet<SiteProfile> SiteProfiles => Set<SiteProfile>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<CollaborationRequest> CollaborationRequests => Set<CollaborationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed some default data so the portfolio isn't empty on first run
        modelBuilder.Entity<Experience>().HasData(
            new Experience { Id = 1, Title = "Senior Developer", Company = "Acme Corp", Period = "2022 - Present", Description = "Led development of cloud-native applications.", SortOrder = 1 },
            new Experience { Id = 2, Title = "Full Stack Developer", Company = "Startup Inc", Period = "2019 - 2022", Description = "Built and maintained web applications using .NET and React.", SortOrder = 2 }
        );

        modelBuilder.Entity<Skill>().HasData(
            new Skill { Id = 1, Name = "C# / .NET", Category = "Backend", Proficiency = 90 },
            new Skill { Id = 2, Name = "Blazor", Category = "Frontend", Proficiency = 85 },
            new Skill { Id = 3, Name = "SQL / EF Core", Category = "Backend", Proficiency = 80 },
            new Skill { Id = 4, Name = "Docker", Category = "DevOps", Proficiency = 70 }
        );

        modelBuilder.Entity<Project>().HasData(
            new Project { Id = 1, Title = "Portfolio CMS", Description = "This very portfolio, built with Blazor and EF Core.", TechStack = "Blazor, EF Core, SQLite", SortOrder = 1 }
        );

        modelBuilder.Entity<SiteProfile>().HasData(
            new SiteProfile
            {
                Id = 1,
                OwnerName = "Your Name",
                Tagline = "Full Stack Developer · .NET · Blazor · Cloud",
                Eyebrow = "👋 Welcome to my portfolio",
                HeroCta = "Hire Me",
                HeroSecondaryCta = "View Projects",
                ContactBlurb = "Have an opportunity or just want to say hi? Fill out the form and I'll get back to you.",
                Email = "hello@example.com",
                Location = "Your City, Country"
            }
        );
    }
}
