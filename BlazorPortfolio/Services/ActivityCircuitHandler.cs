using Microsoft.AspNetCore.Components.Server.Circuits;

namespace BlazorPortfolio.Services;

public class ActivityCircuitHandler : CircuitHandler
{
    private readonly DatabaseKeepAliveService _keepAliveService;
    private readonly ILogger<ActivityCircuitHandler> _logger;

    public ActivityCircuitHandler(DatabaseKeepAliveService keepAliveService, ILogger<ActivityCircuitHandler> logger)
    {
        _keepAliveService = keepAliveService;
        _logger = logger;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit {CircuitId} opened. Incrementing database keepalive circuit count.", circuit.Id);
        _keepAliveService.IncrementCircuits();
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit {CircuitId} closed. Decrementing database keepalive circuit count.", circuit.Id);
        _keepAliveService.DecrementCircuits();
        return base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
