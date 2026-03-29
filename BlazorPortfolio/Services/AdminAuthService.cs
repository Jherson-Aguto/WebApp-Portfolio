using BlazorPortfolio.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorPortfolio.Services;

/// <summary>
/// Admin session using in-memory state per circuit.
/// HttpContext is not reliably available in Blazor Server interactive mode.
/// </summary>
public class AdminAuthService(IDbContextFactory<AppDbContext> dbFactory)
{
    private bool _isAuthenticated;

    public bool IsAuthenticated => _isAuthenticated;

    public async Task<bool> LoginAsync(string username, string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null) return false;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return false;
        _isAuthenticated = true;
        return true;
    }

    public void Logout() => _isAuthenticated = false;
}
