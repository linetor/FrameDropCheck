namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Represents a backup cleanup log entry.
/// </summary>
public class BackupCleanupLog
{
    /// <summary>
    /// Gets or sets the cleanup identifier.
    /// </summary>
    public int CleanupId { get; set; }

    /// <summary>
    /// Gets or sets the referenced media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deleted backup file path.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cleanup time.
    /// </summary>
    public DateTime? CleanupTime { get; set; }

    /// <summary>
    /// Gets or sets the related media reference.
    /// </summary>
    public Media? Media { get; set; }
}
