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
    public DbSet<DeveloperNetworkProfileRevision> DeveloperNetworkProfileRevisions => Set<DeveloperNetworkProfileRevision>();
    public DbSet<DeveloperProfileEnrichment> DeveloperProfileEnrichments => Set<DeveloperProfileEnrichment>();
    public DbSet<ResumeFile> ResumeFiles => Set<ResumeFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Seed some default data so the portfolio isn't empty on first run
        modelBuilder.Entity<Experience>().HasData(
            new Experience { Id = 1, Title = "Software Developer", Company = "Professional Experience", Period = "2022 - Present", Description = "Developing scalable web solutions and backend systems.", SortOrder = 1 },
            new Experience { Id = 2, Title = "Full Stack Developer", Company = "Freelance / Open Source", Period = "2019 - 2022", Description = "Built and maintained high-performance web applications using .NET and modern frameworks.", SortOrder = 2 }
        );

        modelBuilder.Entity<Skill>().HasData(
            new Skill { Id = 1, Name = "C# / .NET", Category = "Backend", Proficiency = 90 },
            new Skill { Id = 2, Name = "Blazor", Category = "Frontend", Proficiency = 85 },
            new Skill { Id = 3, Name = "SQL / EF Core", Category = "Backend", Proficiency = 80 },
            new Skill { Id = 4, Name = "Docker", Category = "DevOps", Proficiency = 70 }
        );

        modelBuilder.Entity<Project>()
            .HasIndex(p => p.Slug)
            .IsUnique();

        modelBuilder.Entity<Project>().HasData(
            new Project 
            { 
                Id = 1, 
                Title = "Portfolio CMS", 
                Slug = "portfolio-cms",
                ShortDescription = "Problem: Building a personal brand that is easy to manage. Solution: A custom-built CMS and portfolio. Tech: Blazor Server, EF Core, Neon PostgreSQL. Result: A professional, dynamic portfolio.", 
                DetailedDescription = "This personal developer portfolio is a custom-built CMS application created using Blazor Server and ASP.NET Core, with Entity Framework Core mapping data to a Neon PostgreSQL instance. It allows administrators to update resumes, manage skills, configure site metadata, and read incoming messages securely.",
                ProblemStatement = "Managing personal developer details, resumes, and showcase builds across static templates often requires tedious manual code updates and lacks a dedicated dashboard.",
                SolutionOverview = "A central Blazor Server CMS application serving as the administration cockpit, storing records securely in PostgreSQL, and serving metadata dynamically.",
                KeyFeatures = "Interactive admin dashboard\nDynamic resume uploader\nSecurity logs & Spam protection\nMinimal API endpoints",
                TechStack = "Blazor, EF Core, PostgreSQL", 
                Category = "Full Stack",
                Status = "Live",
                LiveUrl = "https://jhersonaguto.dev",
                RepositoryUrl = "https://github.com/Jherson-Aguto/WebApp-Portfolio",
                IsPublished = true,
                SortOrder = 1,
                PublishedAt = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<SiteProfile>().HasData(
            new SiteProfile
            {
                Id = 1,
                OwnerName = "Jherson Aguto",
                Tagline = "Full Stack Developer · .NET · Blazor · Cloud",
                Eyebrow = "👋 Welcome to my portfolio",
                HeroCta = "Hire Me",
                HeroSecondaryCta = "View Projects",
                ContactBlurb = "Have an opportunity or just want to say hi? Fill out the form and I'll get back to you.",
                Email = "hello@jhersonaguto.dev",
                Location = "Philippines"
            }
        );
    }
}
