using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// A background service that runs once on startup to ensure the web injection is applied.
/// </summary>
public class WebInjectionStartupService : IHostedService
{
    private readonly WebInjectionService _injectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebInjectionStartupService"/> class.
    /// </summary>
    /// <param name="injectionService">The injection service.</param>
    public WebInjectionStartupService(WebInjectionService injectionService)
    {
        _injectionService = injectionService;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Run the injection logic
        _injectionService.InjectMonitoringScript();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
