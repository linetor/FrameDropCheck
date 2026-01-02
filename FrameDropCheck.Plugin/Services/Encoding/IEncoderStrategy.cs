namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Defines a strategy for generating FFmpeg encoding arguments.
/// </summary>
public interface IEncoderStrategy
{
    /// <summary>
    /// Gets the unique name of this encoder strategy (e.g., "CPU-libx264", "NVENC-hevc").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the FFmpeg arguments command string.
    /// </summary>
    /// <param name="inputPath">Path to the input file.</param>
    /// <param name="outputPath">Path to the output file.</param>
    /// <param name="crf">Target Constant Rate Factor (or equivalent quality setting).</param>
    /// <param name="preset">Target preset (e.g. medium, slow).</param>
    /// <param name="bitrateMbps">Optional target bitrate in Mbps. If 0, uses CRF.</param>
    /// <returns>The complete argument string for FFmpeg.</returns>
    string BuildArguments(string inputPath, string outputPath, int crf, string preset, double bitrateMbps = 0);
}
