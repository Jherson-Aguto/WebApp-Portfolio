using BlazorPortfolio.Data;
using BlazorPortfolio.Models;
using BlazorPortfolio.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorPortfolio.Services;

public class ContentService(
    IDbContextFactory<AppDbContext> factory, 
    EmailService email, 
    HttpClient http, 
    IMemoryCache cache,
    AdminAuthService auth,
    GitHubStorageService githubStorage)
{
    // ── Experiences ──────────────────────────────────────────────────────────
    public async Task<List<Experience>> GetExperiencesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Experiences.OrderBy(e => e.SortOrder).ToListAsync();
    }

    public async Task SaveExperienceAsync(Experience exp)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (exp.Id == 0) db.Experiences.Add(exp);
        else db.Experiences.Update(exp);
        await db.SaveChangesAsync();
    }

    public async Task DeleteExperienceAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Experiences.Where(e => e.Id == id).ExecuteDeleteAsync();
    }

    // ── Skills ───────────────────────────────────────────────────────────────
    public async Task<List<Skill>> GetSkillsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Skills.OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync();
    }

    public async Task SaveSkillAsync(Skill skill)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (skill.Id == 0) db.Skills.Add(skill);
        else db.Skills.Update(skill);
        await db.SaveChangesAsync();
    }

    public async Task DeleteSkillAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Skills.Where(s => s.Id == id).ExecuteDeleteAsync();
    }

    // ── Projects ─────────────────────────────────────────────────────────────
    public async Task<List<Project>> GetProjectsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Projects.OrderBy(p => p.SortOrder).ToListAsync();
    }

    public async Task<Project?> GetProjectBySlugAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Projects.FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<bool> IsSlugUniqueAsync(string slug, int excludeId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return !await db.Projects.AnyAsync(p => p.Slug == slug && p.Id != excludeId);
    }

    public async Task SaveProjectAsync(Project project)
    {
        await using var db = await factory.CreateDbContextAsync();
        project.UpdatedAt = DateTime.UtcNow;
        if (project.Id == 0)
        {
            project.PublishedAt = DateTime.UtcNow;
            db.Projects.Add(project);
        }
        else
        {
            db.Projects.Update(project);
        }
        await db.SaveChangesAsync();
        cache.Remove(CacheService.Keys.Projects);
    }

    public async Task DeleteProjectAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync();
        cache.Remove(CacheService.Keys.Projects);
    }

    // ── Site Profile ──────────────────────────────────────────────────────────
    public async Task<SiteProfile> GetProfileAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SiteProfiles.OrderBy(s => s.Id).FirstOrDefaultAsync()
               ?? new SiteProfile();
    }

    public async Task SaveProfileAsync(SiteProfile profile)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (profile.Id == 0) db.SiteProfiles.Add(profile);
        else db.SiteProfiles.Update(profile);
        await db.SaveChangesAsync();
    }

    // ── Hiring Messages ───────────────────────────────────────────────────────
    public async Task<List<HiringMessage>> GetMessagesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.HiringMessages.OrderByDescending(m => m.SentAt).ToListAsync();
    }

    public async Task AddMessageAsync(HiringMessage msg)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.HiringMessages.Add(msg);
        await db.SaveChangesAsync();
    }

    public async Task MarkReadAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.HiringMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }

    public async Task DeleteMessageAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.HiringMessages.Where(m => m.Id == id).ExecuteDeleteAsync();
    }

    // ── Collaboration Requests ────────────────────────────────────────────────
    public async Task<List<CollaborationRequest>> GetCollaborationRequestsAsync(CollaborationStatus? status = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.CollaborationRequests.AsQueryable();
        if (status.HasValue) q = q.Where(r => r.Status == status.Value);
        return await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task AddCollaborationRequestAsync(CollaborationRequest req)
    {
        await using var db = await factory.CreateDbContextAsync();

        // 1. Duplicate Protection (Email within last 10 mins)
        var recent = await db.CollaborationRequests
            .AnyAsync(r => r.Email == req.Email && r.CreatedAt > DateTime.UtcNow.AddMinutes(-10));
        if (recent) return;

        // 2. Input Validation & Trimming
        req.FirstName = (req.FirstName?.Trim() ?? "");
        if (req.FirstName.Length > 80) req.FirstName = req.FirstName[..80];

        req.LastName = (req.LastName?.Trim() ?? "");
        if (req.LastName.Length > 80) req.LastName = req.LastName[..80];

        req.Email = (req.Email?.Trim().ToLowerInvariant() ?? "");
        if (req.Email.Length > 160) req.Email = req.Email[..160];

        if (req.RoleTitle?.Length > 120) req.RoleTitle = req.RoleTitle[..120];
        if (req.Message?.Length > 1000) req.Message = req.Message[..1000];
        
        // 3. URL Validation & Normalization
        req.PortfolioUrl = NormalizeAndValidateUrl(req.PortfolioUrl, 500);
        req.GitHubUrl = NormalizeAndValidateUrl(req.GitHubUrl, 500);
        req.LinkedInUrl = NormalizeAndValidateUrl(req.LinkedInUrl, 500);

        req.Status = SpamDetectionService.Evaluate(req);
        db.CollaborationRequests.Add(req);
        await db.SaveChangesAsync();

        // Trigger notifications (Fire and forget to avoid blocking UI)
        _ = email.SendNetworkRequestReceivedAsync(req);
        _ = email.SendAdminNewRequestAlertAsync(req);
        
        cache.Remove("ApprovedCollaborators");
    }

    private string? NormalizeAndValidateUrl(string? url, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.Length > maxLen) trimmed = trimmed[..maxLen];

        try
        {
            if (!trimmed.Contains("://")) trimmed = "https://" + trimmed;
            var uri = new Uri(trimmed);
            
            // Safety: Only http/https
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            // Block localhost/private IPs if possible (simple check)
            var host = uri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host.StartsWith("192.168.") || host.StartsWith("10.")) return null;

            return uri.ToString();
        }
        catch { return null; }
    }

    public async Task SetCollaborationStatusAsync(int id, CollaborationStatus newStatus)
    {
        await using var db = await factory.CreateDbContextAsync();
        var req = await db.CollaborationRequests.FindAsync(id);
        if (req is null) return;

        var oldStatus = req.Status;

        // Enforce valid transitions
        bool allowed = (req.Status, newStatus) switch
        {
            (CollaborationStatus.Pending,  CollaborationStatus.Approved) => true,
            (CollaborationStatus.Pending,  CollaborationStatus.Rejected) => true,
            (CollaborationStatus.Pending,  CollaborationStatus.Flagged)  => true,
            (CollaborationStatus.Flagged,  CollaborationStatus.Approved) => true,
            (CollaborationStatus.Flagged,  CollaborationStatus.Rejected) => true,
            (CollaborationStatus.Flagged,  CollaborationStatus.Pending)  => true,
            _ => false
        };

        if (!allowed) return;

        req.Status = newStatus;
        req.ReviewedAt = DateTime.UtcNow;

        // Ensure slug exists upon approval
        if (newStatus == CollaborationStatus.Approved)
        {
            await EnsurePublicSlugAsync(db, req);
        }

        await db.SaveChangesAsync();

        // Trigger email notification if status actually changed to something final
        if (oldStatus != newStatus)
        {
            if (newStatus == CollaborationStatus.Approved) _ = email.SendDeveloperApprovedAsync(req);
            else if (newStatus == CollaborationStatus.Rejected) _ = email.SendDeveloperRejectedAsync(req);
        }

        cache.Remove("ApprovedCollaborators");
    }

    public async Task DeleteCollaborationRequestAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.CollaborationRequests.Where(r => r.Id == id).ExecuteDeleteAsync();
        cache.Remove("ApprovedCollaborators");
    }

    public async Task<List<CollaborationRequest>> GetApprovedCollaboratorsAsync()
    {
        if (cache.TryGetValue("ApprovedCollaborators", out List<CollaborationRequest>? cachedList) && cachedList != null)
        {
            return cachedList;
        }

        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CollaborationRequests
            .Where(r => r.Status == CollaborationStatus.Approved && r.IsVisible)
            .OrderBy(r => r.DisplayOrder)
            .ThenByDescending(r => r.ReviewedAt)
            .ToListAsync();

        // Backfill slugs for existing records if missing
        bool changed = false;
        foreach (var req in list.Where(r => string.IsNullOrEmpty(r.PublicSlug)))
        {
            await EnsurePublicSlugAsync(db, req);
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();

        cache.Set("ApprovedCollaborators", list, TimeSpan.FromMinutes(15));
        return list;
    }

    public async Task UpdateCollaborationRequestAsync(CollaborationRequest req)
    {
        await using var db = await factory.CreateDbContextAsync();

        // Admin-level Validation & Trimming
        req.FirstName = req.FirstName?.Trim() ?? "";
        if (req.FirstName.Length > 80) req.FirstName = req.FirstName[..80];

        req.LastName = req.LastName?.Trim() ?? "";
        if (req.LastName.Length > 80) req.LastName = req.LastName[..80];

        req.Email = req.Email?.Trim().ToLowerInvariant() ?? "";
        if (req.Email.Length > 160) req.Email = req.Email[..160];

        if (req.RoleTitle?.Length > 120) req.RoleTitle = req.RoleTitle[..120];
        if (req.Message?.Length > 1000) req.Message = req.Message[..1000];
        if (req.ReviewNote?.Length > 2000) req.ReviewNote = req.ReviewNote[..2000];
        if (req.Tags?.Length > 300) req.Tags = req.Tags[..300];

        req.PortfolioUrl = NormalizeAndValidateUrl(req.PortfolioUrl, 500);
        req.GitHubUrl = NormalizeAndValidateUrl(req.GitHubUrl, 500);
        req.LinkedInUrl = NormalizeAndValidateUrl(req.LinkedInUrl, 500);
        
        // Track status change for email
        var existing = await db.CollaborationRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == req.Id);
        var statusChangedToApproved = existing != null && existing.Status != CollaborationStatus.Approved && req.Status == CollaborationStatus.Approved;
        var statusChangedToRejected = existing != null && existing.Status != CollaborationStatus.Rejected && req.Status == CollaborationStatus.Rejected;

        // Ensure slug safety if it was changed or missing
        if (string.IsNullOrWhiteSpace(req.PublicSlug))
        {
            await EnsurePublicSlugAsync(db, req);
        }
        else
        {
            // Normalize the slug provided by admin
            req.PublicSlug = NormalizeSlug(req.PublicSlug);
            
            // Handle duplicates
            var baseSlug = req.PublicSlug;
            int counter = 1;
            while (await db.CollaborationRequests.AnyAsync(r => r.PublicSlug == req.PublicSlug && r.Id != req.Id))
            {
                counter++;
                req.PublicSlug = $"{baseSlug}-{counter}";
            }
        }

        req.UpdatedAt = DateTime.UtcNow;
        db.CollaborationRequests.Update(req);
        await db.SaveChangesAsync();

        // Trigger emails if needed
        if (statusChangedToApproved) _ = email.SendDeveloperApprovedAsync(req);
        else if (statusChangedToRejected) _ = email.SendDeveloperRejectedAsync(req);

        cache.Remove("ApprovedCollaborators");
    }

    public async Task<DeveloperProfileEnrichment?> GetEnrichmentAsync(int requestId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.DeveloperProfileEnrichments
            .FirstOrDefaultAsync(e => e.CollaborationRequestId == requestId);
    }

    public async Task<CollaborationRequest?> GetDeveloperProfileBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        await using var db = await factory.CreateDbContextAsync();
        return await db.CollaborationRequests
            .FirstOrDefaultAsync(r => r.PublicSlug == slug && 
                                     r.Status == CollaborationStatus.Approved && 
                                     r.IsVisible);
    }

    public async Task VerifyReciprocalLinkAsync(int requestId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var req = await db.CollaborationRequests.FindAsync(requestId);
        if (req == null || string.IsNullOrWhiteSpace(req.PortfolioUrl)) return;

        req.ReciprocalLinkLastCheckedAt = DateTime.UtcNow;

        try
        {
            // Security Check: Basic protocol & local check
            var uri = new Uri(req.PortfolioUrl);
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw new Exception("Invalid protocol. Only HTTP/HTTPS allowed.");
            
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                throw new Exception("Verification blocked for localhost.");

            // Fetch with safety limits
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await http.GetAsync(req.PortfolioUrl, cts.Token);
            response.EnsureSuccessStatusCode();

            // Limit read size to 1MB
            var html = await response.Content.ReadAsStringAsync(cts.Token);
            if (html.Length > 1024 * 1024) html = html[..(1024 * 1024)];

            // Patterns to look for
            var patterns = new[]
            {
                "jhersonaguto.dev",
                $"jhersonaguto.dev/network/{req.PublicSlug}"
            };

            bool found = false;
            foreach (var p in patterns)
            {
                if (html.Contains(p, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                req.ReciprocalLinkVerified = true;
                req.ReciprocalLinkVerifiedAt = DateTime.UtcNow;
                req.ReciprocalLinkCheckError = null;
            }
            else
            {
                req.ReciprocalLinkVerified = false;
                req.ReciprocalLinkCheckError = "No matching link found on portfolio page.";
            }
        }
        catch (Exception ex)
        {
            req.ReciprocalLinkVerified = false;
            req.ReciprocalLinkCheckError = $"Verification failed: {ex.Message}";
        }

        await db.SaveChangesAsync();
    }

    public async Task SaveEnrichmentAsync(DeveloperProfileEnrichment enrichment)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.DeveloperProfileEnrichments
            .FirstOrDefaultAsync(e => e.CollaborationRequestId == enrichment.CollaborationRequestId);

        if (existing == null)
        {
            db.DeveloperProfileEnrichments.Add(enrichment);
        }
        else
        {
            // Update existing record
            enrichment.Id = existing.Id; // Prevent key modification exception
            db.Entry(existing).CurrentValues.SetValues(enrichment);
        }
        await db.SaveChangesAsync();
    }

    private async Task EnsurePublicSlugAsync(AppDbContext db, CollaborationRequest req)
    {
        if (!string.IsNullOrEmpty(req.PublicSlug)) return;

        var baseSlug = NormalizeSlug($"{req.FirstName}-{req.LastName}");
        var slug = baseSlug;
        int counter = 1;

        while (await db.CollaborationRequests.AnyAsync(r => r.PublicSlug == slug && r.Id != req.Id))
        {
            counter++;
            slug = $"{baseSlug}-{counter}";
        }

        req.PublicSlug = slug;
    }

    private string NormalizeSlug(string input)
    {
        var s = input.ToLowerInvariant().Replace(" ", "-");
        return System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9\-]", "");
    }

    // ── Resumes ─────────────────────────────────────────────────────────────
    public async Task<ResumeFile?> GetActiveResumeAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ResumeFiles.FirstOrDefaultAsync(r => r.IsActive);
    }

    public async Task<List<ResumeFile>> GetAllResumesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ResumeFiles.OrderByDescending(r => r.UploadedAt).ToListAsync();
    }

    public async Task SaveResumeFileAsync(ResumeFile file)
    {
        if (!auth.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Administrator authentication is required to perform this action.");
        }

        await using var db = await factory.CreateDbContextAsync();
        
        if (file.IsActive)
        {
            // Deactivate all other resumes
            await db.ResumeFiles
                .Where(r => r.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));
        }

        if (file.Id == 0)
        {
            db.ResumeFiles.Add(file);
        }
        else
        {
            file.UpdatedAt = DateTime.UtcNow;
            db.ResumeFiles.Update(file);
        }

        await db.SaveChangesAsync();

        // Ensure SiteProfile points to "/resume"
        var profile = await db.SiteProfiles.FirstOrDefaultAsync();
        if (profile != null)
        {
            profile.ResumeUrl = "/resume";
            db.SiteProfiles.Update(profile);
            await db.SaveChangesAsync();
            cache.Remove(CacheService.Keys.Profile);
        }
    }

    public async Task SetActiveResumeAsync(int id)
    {
        if (!auth.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Administrator authentication is required to perform this action.");
        }

        await using var db = await factory.CreateDbContextAsync();

        // Deactivate all resumes
        await db.ResumeFiles
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false));

        // Activate selected
        await db.ResumeFiles
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, true));

        // Ensure SiteProfile points to "/resume"
        var profile = await db.SiteProfiles.FirstOrDefaultAsync();
        if (profile != null)
        {
            profile.ResumeUrl = "/resume";
            db.SiteProfiles.Update(profile);
            await db.SaveChangesAsync();
            cache.Remove(CacheService.Keys.Profile);
        }
    }

    public async Task DeleteResumeAsync(int id)
    {
        if (!auth.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Administrator authentication is required to perform this action.");
        }

        await using var db = await factory.CreateDbContextAsync();
        var file = await db.ResumeFiles.FindAsync(id);
        if (file == null) return;

        db.ResumeFiles.Remove(file);
        await db.SaveChangesAsync();

        if (file.IsActive)
        {
            var activeExists = await db.ResumeFiles.AnyAsync(r => r.IsActive);
            if (!activeExists)
            {
                var profile = await db.SiteProfiles.FirstOrDefaultAsync();
                if (profile != null)
                {
                    profile.ResumeUrl = null;
                    db.SiteProfiles.Update(profile);
                    await db.SaveChangesAsync();
                    cache.Remove(CacheService.Keys.Profile);
                }
            }
        }
    }

    public async Task<ResumeFile> UploadAndSaveResumeAsync(byte[] fileBytes, string originalFileName)
    {
        if (!auth.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Administrator authentication is required to perform this action.");
        }

        if (fileBytes == null || fileBytes.Length == 0)
        {
            throw new ArgumentException("Uploaded file cannot be empty.");
        }

        if (fileBytes.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("File size exceeds the 5 MB limit.");
        }

        // Generate safe versioned filename: resume-YYYY-MM-DD-guid.pdf
        var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var guidStr = Guid.NewGuid().ToString("N").Substring(0, 8);
        var safeFileName = $"resume-{dateStr}-{guidStr}.pdf";

        // Upload to GitHub via GitHubStorageService
        var fileUrl = await githubStorage.UploadResumeAsync(fileBytes, safeFileName);

        // Build storage key path (relative to repo path)
        var relativePath = $"{githubStorage.BasePath?.Trim('/') ?? "public-assets/resume"}/{safeFileName}";

        var resumeFile = new ResumeFile
        {
            OriginalFileName = originalFileName,
            StoredFileName = safeFileName,
            FileUrl = fileUrl,
            StorageKey = relativePath,
            ContentType = "application/pdf",
            FileSizeBytes = fileBytes.Length,
            IsActive = true, // mark as active by default on upload
            UploadedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save metadata and deactivate previous active ones
        await SaveResumeFileAsync(resumeFile);

        return resumeFile;
    }
}
