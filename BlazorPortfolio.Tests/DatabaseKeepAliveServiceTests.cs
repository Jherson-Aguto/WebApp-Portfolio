using BlazorPortfolio.Data;
using BlazorPortfolio.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BlazorPortfolio.Tests;

public class DatabaseKeepAliveServiceTests
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactoryMock;
    private readonly ILogger<DatabaseKeepAliveService> _loggerMock;

    public DatabaseKeepAliveServiceTests()
    {
        _dbContextFactoryMock = Substitute.For<IDbContextFactory<AppDbContext>>();
        _loggerMock = Substitute.For<ILogger<DatabaseKeepAliveService>>();
    }

    private IOptions<DatabaseKeepAliveOptions> CreateIOptions(DatabaseKeepAliveOptions options)
    {
        var mock = Substitute.For<IOptions<DatabaseKeepAliveOptions>>();
        mock.Value.Returns(options);
        return mock;
    }

    [Fact]
    public void DatabaseKeepAliveOptions_HasCorrectDefaults()
    {
        // Act
        var options = new DatabaseKeepAliveOptions();

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal(4, options.PingIntervalMinutes);
        Assert.Equal(30, options.ActivityWindowMinutes);
        Assert.False(options.EnableActiveHours);
        Assert.Equal(8, options.ActiveHoursStartUtc);
        Assert.Equal(22, options.ActiveHoursEndUtc);
    }

    [Fact]
    public void ShouldKeepAlive_ReturnsTrue_WhenActiveCircuitsGreaterThanZero()
    {
        // Arrange
        var options = new DatabaseKeepAliveOptions();
        var service = new DatabaseKeepAliveService(_dbContextFactoryMock, CreateIOptions(options), _loggerMock);

        // Act
        service.IncrementCircuits();

        // Assert
        Assert.True(service.ShouldKeepAlive());
        Assert.Equal(1, service.GetActiveCircuitsCount());
    }

    [Fact]
    public void ShouldKeepAlive_ReturnsTrue_WhenWithinSlidingActivityWindow()
    {
        // Arrange
        var options = new DatabaseKeepAliveOptions { ActivityWindowMinutes = 10 };
        var service = new DatabaseKeepAliveService(_dbContextFactoryMock, CreateIOptions(options), _loggerMock);

        // Act & Assert
        // Before recording activity, should be false
        Assert.False(service.ShouldKeepAlive());

        // Record activity now
        service.RecordActivity();
        
        // Assert: should be true now as UtcNow is within the 10 minute window
        Assert.True(service.ShouldKeepAlive());
    }

    [Fact]
    public void ShouldKeepAlive_ReturnsFalse_WhenOutsideSlidingActivityWindow()
    {
        // Arrange
        var options = new DatabaseKeepAliveOptions { ActivityWindowMinutes = -1 }; // Force window to be expired
        var service = new DatabaseKeepAliveService(_dbContextFactoryMock, CreateIOptions(options), _loggerMock);

        // Act
        service.RecordActivity();

        // Assert
        Assert.False(service.ShouldKeepAlive());
    }

    [Fact]
    public void ShouldKeepAlive_RespectsActiveHours_WhenEnabled()
    {
        // Arrange
        var currentUtcHour = DateTime.UtcNow.Hour;
        var startHour = currentUtcHour;
        var endHour = (currentUtcHour + 2) % 24;

        var options = new DatabaseKeepAliveOptions
        {
            EnableActiveHours = true,
            ActiveHoursStartUtc = startHour,
            ActiveHoursEndUtc = endHour,
            ActivityWindowMinutes = 0 // Disable activity window keepalive
        };
        var service = new DatabaseKeepAliveService(_dbContextFactoryMock, CreateIOptions(options), _loggerMock);

        // Act & Assert
        Assert.True(service.ShouldKeepAlive());
    }

    [Fact]
    public void ShouldKeepAlive_ReturnsFalse_WhenAllConditionsAreFalse()
    {
        // Arrange
        var options = new DatabaseKeepAliveOptions
        {
            EnableActiveHours = false,
            ActivityWindowMinutes = 0
        };
        var service = new DatabaseKeepAliveService(_dbContextFactoryMock, CreateIOptions(options), _loggerMock);

        // Act & Assert
        Assert.False(service.ShouldKeepAlive());
    }
}
