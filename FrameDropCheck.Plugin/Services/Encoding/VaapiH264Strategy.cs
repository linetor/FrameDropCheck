using FrameDropCheck.Plugin.Configuration;

namespace FrameDropCheck.Plugin.Services.Encoding;

/// <summary>
/// Encoder strategy using VAAPI (Intel/AMD).
/// </summary>
public class VaapiH264Strategy : IEncoderStrategy
{
    private readonly PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="VaapiH264Strategy"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    public VaapiH264Strategy(PluginConfiguration config)
    {
        _config = config ?? throw new System.ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public string Name => "VAAPI (h264_vaapi)";

    /// <inheritdoc />
    public string BuildArguments(string inputPath, string outputPath, int crf, string preset, double bitrateMbps = 0)
    {
        // VAAPI usually needs format conversion/upload
        // -vf 'format=nv12,hwupload'
        // Using CQP (Constant Quantization Parameter) roughly equivalent to CRF?
        // -qp {crf} might work for some drivers.

        var devicePath = string.IsNullOrEmpty(_config.HardwareDevicePath) ? "/dev/dri/renderD128" : _config.HardwareDevicePath;

        string qualityPart = bitrateMbps > 0
            ? $"-b:v {bitrateMbps.ToString(System.Globalization.CultureInfo.InvariantCulture)}M -maxrate {bitrateMbps.ToString(System.Globalization.CultureInfo.InvariantCulture)}M"
            : $"-qp {crf.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        return $"-hwaccel vaapi -hwaccel_device {devicePath} -hwaccel_output_format vaapi " +
               $"-i \"{inputPath}\" -c:v h264_vaapi {qualityPart} -c:a copy \"{outputPath}\" -y";
    }
}
