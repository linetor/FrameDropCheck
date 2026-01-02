using System.Globalization;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Encoder strategy using Intel QSV (Quick Sync Video).
/// </summary>
public class QsvH264Strategy : IEncoderStrategy
{
    /// <inheritdoc />
    public string Name => "QSV (h264_qsv)";

    /// <inheritdoc />
    public string BuildArguments(string inputPath, string outputPath, int crf, string preset, double bitrateMbps = 0)
    {
        // QSV often uses -global_quality for ICQ (Intelligent Constant Quality)
        // preset: veryfast, faster, fast, medium, slow, slower, veryslow

        string qualityPart = bitrateMbps > 0
            ? $"-b:v {bitrateMbps.ToString(CultureInfo.InvariantCulture)}M -maxrate {bitrateMbps.ToString(CultureInfo.InvariantCulture)}M"
            : $"-global_quality {crf.ToString(CultureInfo.InvariantCulture)}";

        return $"-i \"{inputPath}\" -c:v h264_qsv {qualityPart} -preset {preset} -c:a copy \"{outputPath}\" -y";
    }
}
