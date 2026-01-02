using MediaBrowser.Model.Plugins;

namespace FrameDropCheck.Plugin.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        // 기본값 설정
        FfmpegPath = "/usr/lib/jellyfin-ffmpeg/ffmpeg";
        MaintenanceStartTime = "02:00";
        MaintenanceEndTime = "06:00";
        DropThreshold = 5.0; // 5% frame drops
        TargetCrf = 23;      // Standard quality
        BackupPath = "/media/backup/framedrop";
    }

    /// <summary>
    /// Gets or sets FFmpeg 실행 파일의 경로.
    /// </summary>
    public string FfmpegPath { get; set; }

    /// <summary>
    /// Gets or sets 미디어 스캔 및 최적화 시작 시간 (HH:mm).
    /// </summary>
    public string MaintenanceStartTime { get; set; }

    /// <summary>
    /// Gets or sets 미디어 스캔 및 최적화 종료 시간 (HH:mm).
    /// </summary>
    public string MaintenanceEndTime { get; set; }

    /// <summary>
    /// Gets or sets 자동 인코딩을 트리거할 프레임 드롭 임계값 (%).
    /// </summary>
    public double DropThreshold { get; set; }

    /// <summary>
    /// Gets or sets 인코딩 시 사용할 대상 품질 (CRF).
    /// </summary>
    public int TargetCrf { get; set; }

    /// <summary>
    /// Gets or sets 인코딩 시 사용할 대상 비트레이트 (Mbps, 0이면 CRF 사용).
    /// </summary>
    public double TargetBitrate { get; set; }

    /// <summary>
    /// Gets or sets 프레임 드롭이 발생한 미디어 파일의 백업 경로.
    /// </summary>
    public string BackupPath { get; set; }

    /// <summary>
    /// Gets or sets the preferred encoder type (e.g., "CPU", "NVENC", "VAAPI", "QSV", "AMF").
    /// </summary>
    public string EncoderType { get; set; } = "CPU";

    /// <summary>
    /// Gets or sets the hardware device path (e.g., /dev/dri/renderD128 for VAAPI).
    /// </summary>
    public string HardwareDevicePath { get; set; } = string.Empty;
}
