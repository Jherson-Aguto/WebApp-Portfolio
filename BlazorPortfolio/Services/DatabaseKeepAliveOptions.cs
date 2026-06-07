namespace BlazorPortfolio.Services;

public class DatabaseKeepAliveOptions
{
    public bool Enabled { get; set; } = true;
    public double PingIntervalMinutes { get; set; } = 4;
    public double ActivityWindowMinutes { get; set; } = 30;
    public bool EnableActiveHours { get; set; } = false;
    public int ActiveHoursStartUtc { get; set; } = 8;
    public int ActiveHoursEndUtc { get; set; } = 22;
}
