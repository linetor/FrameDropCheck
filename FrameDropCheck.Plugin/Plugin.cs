using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FrameDropCheck.Plugin.Configuration;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using FrameDropCheck.Plugin.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace FrameDropCheck.Plugin;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        _applicationPaths = applicationPaths;
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "FrameDropCheck";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("d3075c1e-6e7c-4a70-b989-170aa2f4aa3e");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }

    /// <summary>
    /// Registers plugin services (database, repositories, file logger, domain services) into the
    /// provided <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register into.</param>
    /// <param name="host">The server application host.</param>
    public static void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost host)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(host);

        // Determine plugin folder base (use assembly location for Docker-friendliness)
        var pluginAssembly = typeof(Plugin).Assembly;
        var pluginDir = Path.GetDirectoryName(pluginAssembly.Location);
        if (string.IsNullOrEmpty(pluginDir))
        {
            // Fallback to data path if location is unknown
            var appPaths = serviceCollection.BuildServiceProvider().GetRequiredService<IApplicationPaths>();
            pluginDir = Path.Combine(appPaths.DataPath, "plugins", "FrameDropCheck");
        }

        if (!Directory.Exists(pluginDir))
        {
            Directory.CreateDirectory(pluginDir);
        }

        var resolvedDbPath = Path.Combine(pluginDir, "framedrop.db");
        var resolvedLogPath = Path.Combine(pluginDir, "framedrop.log");

        // Try to get a native logger from the service collection if possible
        Microsoft.Extensions.Logging.ILogger? jellyfinLogger = null;
        try
        {
            var sp = serviceCollection.BuildServiceProvider();
            var loggerFactory = sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
            jellyfinLogger = loggerFactory?.CreateLogger("FrameDropCheck");
            jellyfinLogger?.LogInformation("FrameDropCheck services being registered. Log path: {LogPath}", resolvedLogPath);
        }
        catch
        {
            // Fallback to no native logger during registration phase
        }

        // Register file logger instance
        serviceCollection.AddSingleton<IAppLogger>(new FileLogger(resolvedLogPath, jellyfinLogger));

        // Register DB, repositories, and domain services
        serviceCollection.AddFrameDropCheckDatabase(resolvedDbPath);
    }
}
