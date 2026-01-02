using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FrameDropCheck.Plugin;

/// <summary>
/// Registers plugin services with the Jellyfin application host.
/// </summary>
public class PluginServiceRegistrar : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Call the static registration logic
        Plugin.RegisterServices(serviceCollection, applicationHost);
    }
}
