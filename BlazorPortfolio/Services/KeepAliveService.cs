namespace BlazorPortfolio.Services;

public class KeepAliveService : IHostedService, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<KeepAliveService> _logger;
    private readonly string? _baseUrl;
    private readonly TimeSpan _interval;
    private Timer? _timer;

    public KeepAliveService(IConfiguration config, ILogger<KeepAliveService> logger)
        : this(config, logger, new HttpClient()) { }

    public KeepAliveService(IConfiguration config, ILogger<KeepAliveService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _http = httpClient;
        _baseUrl = config["KeepAlive:BaseUrl"];
        _interval = TimeSpan.FromMinutes(
            double.TryParse(config["KeepAlive:IntervalMinutes"], out var m) && m >= 10 ? m : 10);
    }

    public Task StartAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            _logger.LogWarning("KeepAlive:BaseUrl is not configured. Keep-alive pings are disabled.");
            return Task.CompletedTask;
        }
        _timer = new Timer(Ping, null, _interval, _interval);
        return Task.CompletedTask;
    }

    private async void Ping(object? _)
    {
        var now = DateTime.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;
        if (now.Hour < 6 || now.Hour >= 22) return;

        try
        {
            var resp = await _http.GetAsync(_baseUrl);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Keep-alive ping returned {Status}", resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keep-alive ping failed");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _http.Dispose();
    }
}
