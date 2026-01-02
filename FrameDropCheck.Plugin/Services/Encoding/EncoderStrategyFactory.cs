using System;
using System.Collections.Generic;
using System.Linq;
using FrameDropCheck.Plugin.Configuration;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Implementation of IEncoderStrategyFactory.
/// </summary>
public class EncoderStrategyFactory : IEncoderStrategyFactory
{
    private readonly IEnumerable<IEncoderStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncoderStrategyFactory"/> class.
    /// </summary>
    /// <param name="strategies">The available encoder strategies.</param>
    public EncoderStrategyFactory(IEnumerable<IEncoderStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    /// <inheritdoc />
    public IEncoderStrategy GetStrategy(PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var preferredType = config.EncoderType;

        return _strategies.FirstOrDefault(s => s.Name.Contains(preferredType, StringComparison.OrdinalIgnoreCase))
               ?? _strategies.FirstOrDefault(s => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase))
               ?? _strategies.First();
    }
}
