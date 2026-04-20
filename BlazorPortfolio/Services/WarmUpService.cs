using BlazorPortfolio.Models;

namespace BlazorPortfolio.Services;

/// <summary>
/// Runs once on startup to pre-populate the shared IMemoryCache with portfolio
/// and GitHub data so the first real visitor gets a fast page load.
/// </summary>
public class WarmUpService(
    IServiceScopeFactory scopeFactory,
    ILogger<WarmUpService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        // Run in background so startup isn't blocked
        _ = Task.Run(() => WarmUpAsync(ct), ct);
        return;
    }

    private async Task WarmUpAsync(CancellationToken ct)
    {
        // Delay reduced to 2 s — enough for the DI container and DB context to be
        // fully registered, while keeping the cache hot well before the first visitor.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var svc   = scope.ServiceProvider.GetRequiredService<ContentService>();
            var cache = scope.ServiceProvider.GetRequiredService<CacheService>();
            var gh    = scope.ServiceProvider.GetRequiredService<GitHubService>();
            var cfg   = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            logger.LogInformation("WarmUp: pre-loading portfolio data into cache...");

            // Parallel DB queries — profile, experiences, skills, projects
            var profileTask     = svc.GetProfileAsync();
            var experiencesTask = svc.GetExperiencesAsync();
            var skillsTask      = svc.GetSkillsAsync();
            var projectsTask    = svc.GetProjectsAsync();
            var collabTask      = svc.GetApprovedCollaboratorsAsync();

            await Task.WhenAll(profileTask, experiencesTask, skillsTask, projectsTask, collabTask);

            var profile     = await profileTask;
            var experiences = await experiencesTask;
            var skills      = await skillsTask;
            var projects    = await projectsTask;
            var collabs     = await collabTask;

            await cache.SetAsync(CacheService.Keys.Profile,       profile);
            await cache.SetAsync(CacheService.Keys.Experiences,   experiences);
            await cache.SetAsync(CacheService.Keys.Skills,        skills);
            await cache.SetAsync(CacheService.Keys.Projects,      projects);
            await cache.SetAsync(CacheService.Keys.Collaborators, collabs);

            logger.LogInformation("WarmUp: DB data cached (profile, {E} experiences, {S} skills, {P} projects, {C} collaborators)",
                experiences.Count, skills.Count, projects.Count, collabs.Count);

            // GitHub warm-up (only if username is configured)
            var username = profile?.GitHubUsername;
            if (!string.IsNullOrWhiteSpace(username))
            {
                logger.LogInformation("WarmUp: pre-loading GitHub data for {Username}...", username);

                var ghProfileTask = gh.GetProfileAsync(username);
                var pinnedTask    = gh.GetPinnedReposAsync(username);
                var contribTask   = gh.GetContributionsAsync(username);

                await Task.WhenAll(ghProfileTask, pinnedTask, contribTask);

                var ghProfile = await ghProfileTask;
                var pinned    = await pinnedTask;
                var (weeks, total) = await contribTask;

                if (ghProfile is not null)
                    await cache.SetAsync(CacheService.Keys.GitHubProfile, ghProfile);
                await cache.SetAsync(CacheService.Keys.GitHubPinned,        pinned);
                await cache.SetAsync(CacheService.Keys.GitHubContributions, weeks);
                await cache.SetAsync(CacheService.Keys.GitHubContribTotal,  total);

                logger.LogInformation("WarmUp: GitHub data cached ({R} pinned repos, {C} contrib weeks)",
                    pinned.Count, weeks.Count);
            }

            logger.LogInformation("WarmUp: complete — cache is hot.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WarmUp: failed (non-fatal, first visitor will warm the cache)");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
