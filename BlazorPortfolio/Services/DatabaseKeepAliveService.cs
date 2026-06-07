using BlazorPortfolio.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorPortfolio.Services;

public class DatabaseKeepAliveService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<DatabaseKeepAliveService> _logger;
    private readonly DatabaseKeepAliveOptions _options;

    private readonly object _lock = new();
    private DateTime _lastActivityTime = DateTime.MinValue;
    private int _activeCircuits = 0;

    public DatabaseKeepAliveService(
        IDbContextFactory<AppDbContext> contextFactory,
        IOptions<DatabaseKeepAliveOptions> options,
        ILogger<DatabaseKeepAliveService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _options = options.Value;
    }

    // Secondary constructor for testing to allow passing mock/direct options directly
    public DatabaseKeepAliveService(
        IDbContextFactory<AppDbContext> contextFactory,
        DatabaseKeepAliveOptions options,
        ILogger<DatabaseKeepAliveService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _options = options;
    }

    public void RecordActivity()
    {
        lock (_lock)
        {
            _lastActivityTime = DateTime.UtcNow;
        }
    }

    public void IncrementCircuits()
    {
        lock (_lock)
        {
            _activeCircuits++;
            _lastActivityTime = DateTime.UtcNow;
        }
    }

    public void DecrementCircuits()
    {
        lock (_lock)
        {
            if (_activeCircuits > 0)
            {
                _activeCircuits--;
            }
            _lastActivityTime = DateTime.UtcNow;
        }
    }

    public int GetActiveCircuitsCount()
    {
        lock (_lock)
        {
            return _activeCircuits;
        }
    }

    public DateTime GetLastActivityTime()
    {
        lock (_lock)
        {
            return _lastActivityTime;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Database keepalive is disabled.");
            return;
        }

        _logger.LogInformation("Database keepalive service started. Interval: {Interval} mins, Activity Window: {Window} mins, Active Hours: {ActiveHoursEnabled}",
            _options.PingIntervalMinutes, _options.ActivityWindowMinutes, _options.EnableActiveHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (ShouldKeepAlive())
                {
                    _logger.LogInformation("Sending keepalive query to Neon database. Active circuits: {Circuits}", GetActiveCircuitsCount());
                    using var db = await _contextFactory.CreateDbContextAsync(stoppingToken);
                    await db.Database.ExecuteSqlRawAsync("SELECT 1;", stoppingToken);
                    _logger.LogInformation("Database keepalive query successful.");
                }
                else
                {
                    _logger.LogDebug("Database keepalive skipped (inactive).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing database keepalive query.");
            }

            var interval = TimeSpan.FromMinutes(_options.PingIntervalMinutes >= 0.1 ? _options.PingIntervalMinutes : 4);
            await Task.Delay(interval, stoppingToken);
        }
    }

    public bool ShouldKeepAlive()
    {
        int circuits;
        DateTime lastActivity;

        lock (_lock)
        {
            circuits = _activeCircuits;
            lastActivity = _lastActivityTime;
        }

        // 1. Keep warm if there are active Blazor circuits (users actively on the site)
        if (circuits > 0)
        {
            return true;
        }

        // 2. Keep warm if we are within the sliding activity window from the last page/API load
        var window = TimeSpan.FromMinutes(_options.ActivityWindowMinutes);
        if ((DateTime.UtcNow - lastActivity) < window)
        {
            return true;
        }

        // 3. Keep warm if we are inside the active hours range (when enabled)
        if (_options.EnableActiveHours)
        {
            var currentHour = DateTime.UtcNow.Hour;
            if (_options.ActiveHoursStartUtc <= _options.ActiveHoursEndUtc)
            {
                return currentHour >= _options.ActiveHoursStartUtc && currentHour < _options.ActiveHoursEndUtc;
            }
            else
            {
                // Handles overnight/cross-midnight ranges
                return currentHour >= _options.ActiveHoursStartUtc || currentHour < _options.ActiveHoursEndUtc;
            }
        }

        return false;
    }
}
