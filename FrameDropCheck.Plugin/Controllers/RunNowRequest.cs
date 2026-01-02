namespace FrameDropCheck.Plugin.Controllers
{
    /// <summary>
    /// Request DTO for triggering an immediate frame drop check.
    /// </summary>
    public class RunNowRequest
    {
        /// <summary>
        /// Gets or sets the Jellyfin media identifier to run the check for.
        /// </summary>
        public string? MediaId { get; set; }
    }
}
