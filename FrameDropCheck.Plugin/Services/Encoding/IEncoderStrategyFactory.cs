using FrameDropCheck.Plugin.Configuration;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Factory for creating/retrieving the appropriate encoder strategy based on configuration.
/// </summary>
public interface IEncoderStrategyFactory
{
    /// <summary>
    /// Gets the encoder strategy based on the plugin configuration.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The matching encoder strategy.</returns>
    IEncoderStrategy GetStrategy(PluginConfiguration config);
}
