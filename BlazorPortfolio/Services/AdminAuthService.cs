using BlazorPortfolio.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace BlazorPortfolio.Services;

/// <summary>
/// Admin session using in-memory state per circuit.
/// HttpContext is not reliably available in Blazor Server interactive mode.
/// </summary>
public class AdminAuthService(IDbContextFactory<AppDbContext> dbFactory)
{
    private bool _isAuthenticated;

    public bool IsAuthenticated => _isAuthenticated;

    // Static shared tracker — persists across circuits, keyed by IP or "unknown"
    private static readonly ConcurrentDictionary<string, (int Count, DateTime Window)> _attempts = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockWindow = TimeSpan.FromMinutes(1);

    public bool IsLockedOut(string key)
    {
        if (!_attempts.TryGetValue(key, out var entry)) return false;
        if (DateTime.UtcNow - entry.Window > LockWindow)
        {
            _attempts.TryRemove(key, out _);
            return false;
        }
        return entry.Count >= MaxAttempts;
    }

    public TimeSpan LockoutRemaining(string key)
    {
        if (!_attempts.TryGetValue(key, out var entry)) return TimeSpan.Zero;
        var remaining = LockWindow - (DateTime.UtcNow - entry.Window);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public async Task<bool> LoginAsync(string username, string password, string key = "unknown")
    {
        if (IsLockedOut(key)) return false;

        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);

        bool success = user is not null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        if (success)
        {
            _attempts.TryRemove(key, out _); // reset on success
            _isAuthenticated = true;
            return true;
        }

        // Record failed attempt
        _attempts.AddOrUpdate(key,
            _ => (1, DateTime.UtcNow),
            (_, old) => DateTime.UtcNow - old.Window > LockWindow
                ? (1, DateTime.UtcNow)
                : (old.Count + 1, old.Window));

        return false;
    }

    public void Logout() => _isAuthenticated = false;
}
