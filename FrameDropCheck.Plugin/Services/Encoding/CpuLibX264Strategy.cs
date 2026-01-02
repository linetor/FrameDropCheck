using System.Globalization;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Encoder strategy that uses software encoding (libx264).
/// </summary>
public class CpuLibX264Strategy : IEncoderStrategy
{
    /// <inheritdoc />
    public string Name => "CPU (libx264)";

    /// <inheritdoc />
    public string BuildArguments(string inputPath, string outputPath, int crf, string preset, double bitrateMbps = 0)
    {
        string qualityPart = bitrateMbps > 0
            ? $"-b:v {bitrateMbps.ToString(CultureInfo.InvariantCulture)}M -maxrate {bitrateMbps.ToString(CultureInfo.InvariantCulture)}M -bufsize {(bitrateMbps * 2).ToString(CultureInfo.InvariantCulture)}M"
            : $"-crf {crf.ToString(CultureInfo.InvariantCulture)}";

        return $"-i \"{inputPath}\" -vcodec libx264 {qualityPart} -preset {preset} -acodec copy \"{outputPath}\" -y";
    }
}
