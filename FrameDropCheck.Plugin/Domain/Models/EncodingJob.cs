namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Represents an encoding job.
/// </summary>
public class EncodingJob
{
    /// <summary>
    /// Gets or sets the job identifier.
    /// </summary>
    public int JobId { get; set; }

    /// <summary>
    /// Gets or sets the referenced media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original file path.
    /// </summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backup file path.
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// Gets or sets the new file path.
    /// </summary>
    public string? NewFilePath { get; set; }

    /// <summary>
    /// Gets or sets the start time of the job.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the job.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the job status (pending/in-progress/completed/failed).
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the related media reference.
    /// </summary>
    public Media? Media { get; set; }
}
