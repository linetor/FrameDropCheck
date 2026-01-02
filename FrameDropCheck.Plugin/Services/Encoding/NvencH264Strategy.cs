using System.Globalization;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Encoder strategy using NVIDIA NVENC.
/// </summary>
public class NvencH264Strategy : IEncoderStrategy
{
    /// <inheritdoc />
    public string Name => "NVENC (h264_nvenc)";

    /// <inheritdoc />
    public string BuildArguments(string inputPath, string outputPath, int crf, string preset, double bitrateMbps = 0)
    {
        // NVENC uses -cq for Constant Quality in VBR mode
        // preset: p1 (fastest) to p7 (slowest). Mapping 'slower' to 'p6'.
        // -rc vbr (Variable Bitrate)

        string nvencPreset = preset switch
        {
            "fast" => "p2",
            "medium" => "p4",
            "slow" => "p6",
            "slower" => "p7",
            _ => "p4"
        };

        string qualityPart = bitrateMbps > 0
            ? $"-b:v {bitrateMbps.ToString(CultureInfo.InvariantCulture)}M -maxrate {bitrateMbps.ToString(CultureInfo.InvariantCulture)}M -rc cbr"
            : $"-rc vbr -cq {crf.ToString(CultureInfo.InvariantCulture)}";

        return $"-i \"{inputPath}\" -c:v h264_nvenc {qualityPart} -preset {nvencPreset} -c:a copy \"{outputPath}\" -y";
    }
}
