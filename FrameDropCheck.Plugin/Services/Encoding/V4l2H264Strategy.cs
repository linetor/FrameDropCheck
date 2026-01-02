using System.Globalization;
using FrameDropCheck.Plugin.Configuration;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Encoder strategy using Video4Linux2 mem2mem (Raspberry Pi/Rockchip/etc).
/// </summary>
public class V4l2H264Strategy : IEncoderStrategy
{
    private readonly PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="V4l2H264Strategy"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    public V4l2H264Strategy(PluginConfiguration config)
    {
        _config = config ?? throw new System.ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public string Name => "V4L2 (Raspberry Pi/ARM)";

    /// <inheritdoc />
    public string BuildArguments(string inputPath, string outputPath, int crf, string preset, double bitrateMbps = 0)
    {
        // V4L2 mem2mem usually ignores CRF and relies on bitrate.
        long bitrate = (long)(bitrateMbps * 1_000_000);

        // If no target bitrate is set, default to a reasonable value (e.g. 4Mbps)
        // ideally we should check input bitrate but we don't have it easily here without more probing data passed in.
        // Assuming user will set TargetBitrate if they use V4L2.
        if (bitrate <= 0)
        {
             bitrate = 4_000_000;
        }

        string bitrateArg = $"-b:v {bitrate}";

        // pixel format yuv420p is often needed for compatibility
        return $"-i \"{inputPath}\" -c:v h264_v4l2m2m {bitrateArg} -pix_fmt yuv420p -c:a copy \"{outputPath}\" -y";
    }
}
