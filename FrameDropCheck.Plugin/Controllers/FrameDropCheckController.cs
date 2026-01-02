using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrameDropCheck.Plugin.Controllers
{
    /// <summary>
    /// API controller that exposes endpoints for plugin actions such as running
    /// an immediate frame drop check from the configuration UI.
    /// </summary>
    [ApiController]
    [Route("/Plugins/FrameDropCheck")]
    public class FrameDropCheckController : ControllerBase
    {
        private readonly IFrameDropCheckManager _manager;
        private readonly Infrastructure.Repositories.IMediaRepository _mediaRepo;
        private readonly Infrastructure.Repositories.IClientPlaybackStatsRepository _clientStatsRepo;
        private readonly IOptimizationAnalyzer _analyzer;
        private readonly IJellyfinApiService _jellyfinApi;
        private readonly Infrastructure.Logging.IAppLogger _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameDropCheckController"/> class.
        /// </summary>
        /// <param name="manager">The frame drop check manager.</param>
        /// <param name="mediaRepo">The media repository.</param>
        /// <param name="clientStatsRepo">The client playback statistics repository.</param>
        /// <param name="analyzer">The optimization analyzer.</param>
        /// <param name="jellyfinApi">The Jellyfin API service.</param>
        /// <param name="logger">The application logger.</param>
        public FrameDropCheckController(
            IFrameDropCheckManager manager,
            Infrastructure.Repositories.IMediaRepository mediaRepo,
            Infrastructure.Repositories.IClientPlaybackStatsRepository clientStatsRepo,
            IOptimizationAnalyzer analyzer,
            IJellyfinApiService jellyfinApi,
            Infrastructure.Logging.IAppLogger logger)
        {
            Console.WriteLine("[FrameDropCheck] Controller Constructor called.");
            _manager = manager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mediaRepo = mediaRepo;
            _clientStatsRepo = clientStatsRepo;
            _analyzer = analyzer;
            _jellyfinApi = jellyfinApi;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>
        /// DEBUG: Intercepts package info request to fix "Error loading plugin details" in dashboard.
        /// </summary>
        /// <returns>Fake package info.</returns>
        [HttpGet("/Packages/FrameDropCheck")]
        public IActionResult GetPackageInfo()
        {
            try
            {
                _logger.LogInformation("[API] Intercepting /Packages/FrameDropCheck request to fix dashboard UI.");
                return Ok(new
                {
                    name = "FrameDropCheck",
                    guid = "d3075c1e-6e7c-4a70-b989-170aa2f4aa3e",
                    versions = new[]
                    {
                        new
                        {
                            version = "1.0.0.0",
                            changelog = "Initial version",
                            targetAbi = "10.11.0.0",
                            sourceUrl = string.Empty,
                            checksum = string.Empty,
                            timestamp = DateTime.UtcNow
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[API] Error serving package info: {ex.Message}");
                return NotFound();
            }
        }

        /// <summary>
        /// Serves the plugin configuration page.
        /// </summary>
        /// <returns>HTML content of the configuration page.</returns>
        [HttpGet("ConfigPage")]
        public IActionResult GetConfigPage()
        {
            try
            {
                // Try multiple possible paths
                var basePath = Plugin.Instance?.DataFolderPath ?? string.Empty;

                // Remove ".Plugin" suffix if present
                if (basePath.EndsWith(".Plugin", StringComparison.OrdinalIgnoreCase))
                {
                    basePath = basePath.Substring(0, basePath.Length - 7);
                }

                var configPagePath = Path.Combine(basePath, "Configuration", "configPage.html");

                _logger.LogInformation($"[API] Attempting to load ConfigPage from: {configPagePath}");

                if (!System.IO.File.Exists(configPagePath))
                {
                    _logger.LogWarning($"[API] ConfigPage not found at: {configPagePath}");
                    return NotFound($"Configuration page not found at: {configPagePath}");
                }

                var html = System.IO.File.ReadAllText(configPagePath);
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[API] Error serving ConfigPage: {ex.Message}");
                return StatusCode(500, $"Error loading configuration page: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the health status of all media items in the database.
        /// </summary>
        /// <returns>JSON array of media health info.</returns>
        [HttpGet("MediaHealth")]
        public async Task<IActionResult> GetMediaHealth()
        {
            try
            {
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                _logger.LogInformation("[API] GetMediaHealth called");

                var mediaList = await _mediaRepo.GetAllAsync().ConfigureAwait(false);
                var clientStats = await _clientStatsRepo.GetAllAsync().ConfigureAwait(false);

                _logger.LogInformation($"[API] Retrieved {mediaList.Count()} media items and {clientStats.Count()} client stats records.");

                // Group by Name to merge duplicates (different IDs but same content)
                var groupedMedia = mediaList.GroupBy(m => string.IsNullOrWhiteSpace(m.Name) ? m.MediaId : m.Name);

                var result = groupedMedia.Select(group =>
                {
                    // Use the most recently scanned item as the representative, or just the first if none scanned
                    var representative = group.OrderByDescending(m => m.LastScanned).FirstOrDefault() ?? group.First();

                    // Collect all stats associated with ANY of the MediaIds in this group
                    var groupMediaIds = group.Select(m => m.MediaId.ToUpperInvariant()).ToHashSet();
                    var groupStats = clientStats.Where(s => groupMediaIds.Contains(s.MediaId.ToUpperInvariant())).ToList();

                    double? clientDropRate = null;
                    int clientPlayCount = groupStats.Count;

                    if (clientPlayCount > 0)
                    {
                        double totalDropped = groupStats.Sum(s => s.FramesDropped);
                        double totalDuration = groupStats.Sum(s => s.PlaybackDuration);
                        if (totalDuration > 0)
                        {
                            clientDropRate = (totalDropped / (totalDuration * 24)) * 100.0;
                        }
                    }

                    return new Api.Models.MediaHealthViewModel
                    {
                        MediaId = representative.MediaId,
                        Name = representative.Name,
                        OptimizationStatus = representative.OptimizationStatus,
                        ProbeDropRate = representative.AverageDropRate,
                        ProbeSpeed = representative.LastScanSpeed,
                        LastScanned = representative.LastScanned,
                        ClientDropRate = clientDropRate,
                        ClientPlayCount = clientPlayCount,
                        CompressionRatio = representative.CompressionRatio
                    };
                });

                _logger.LogInformation($"[API] GetMediaHealth returning {result.Count()} items.");
                return new JsonResult(result, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[API] Error in GetMediaHealth: {ex.Message}", ex);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets summary statistics for all media.
        /// </summary>
        /// <returns>JSON object with statistics.</returns>
        [HttpGet("SummaryStats")]
        public async Task<IActionResult> GetSummaryStats()
        {
            try
            {
                var allMedia = await _mediaRepo.GetAllAsync().ConfigureAwait(false);
                var mediaList = allMedia.ToList();
                var total = mediaList.Count;
                var scanned = mediaList.Count(m => m.LastScanned != null);
                var pending = total - scanned;

                return new JsonResult(
                    new
                    {
                        total,
                        scanned,
                        pending
                    },
                    _jsonOptions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Gets an optimization recommendation for a specific media item.
        /// </summary>
        /// <param name="mediaId">The media identifier.</param>
        /// <returns>Optimization recommendation.</returns>
        [HttpGet("Optimization/{mediaId}")]
        public async Task<IActionResult> GetOptimizationRecommendation(string mediaId)
        {
             if (string.IsNullOrEmpty(mediaId))
             {
                 return BadRequest("MediaId required");
             }

             try
             {
                 // Use JellyfinAPI to try and find media even if not in local DB yet
                 var media = await _jellyfinApi.GetMediaAsync(mediaId).ConfigureAwait(false);

                 if (media == null)
                 {
                     return NotFound("Media not found in Jellyfin or Database");
                 }

                 // Check if we need to auto-import into local DB
                 var localMedia = await _mediaRepo.GetByIdAsync(mediaId).ConfigureAwait(false);
                 if (localMedia == null)
                 {
                     try
                     {
                        await _mediaRepo.AddAsync(media).ConfigureAwait(false);
                     }
                     catch (Exception ex)
                     {
                         // Ignore unique constraint violations (race condition or casing issue)
                         if (ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                             ex.Message.Contains("Error 19", StringComparison.OrdinalIgnoreCase))
                         {
                             localMedia = await _mediaRepo.GetByIdAsync(mediaId).ConfigureAwait(false);
                             if (localMedia != null)
                             {
                                 media = localMedia;
                             }
                         }
                         else
                         {
                             throw; // Rethrow other errors
                         }
                     }
                 }
                 else
                 {
                     media = localMedia;
                 }

                 var recommendation = await _analyzer.AnalyzeMediaAsync(media).ConfigureAwait(false);
                 return new JsonResult(recommendation, _jsonOptions);
             }
             catch (Exception ex)
             {
                 return BadRequest(new { success = false, message = ex.Message });
             }
        }

        /// <summary>
        /// Triggers a historical log analysis check for the provided media id.
        /// </summary>
        /// <param name="req">Request containing a <c>MediaId</c>.</param>
        /// <returns>JSON success/failure payload.</returns>
        [HttpPost("RunNow")]
        public IActionResult RunNow([FromBody] RunNowRequest? req)
        {
            Console.WriteLine($"[FrameDropCheck] RunNow (Log Analysis) Endpoint Hit. MediaId: {req?.MediaId}");
            if (req == null || string.IsNullOrWhiteSpace(req.MediaId))
            {
                return BadRequest(new { success = false, message = "mediaId is required in request body" });
            }

            try
            {
                _manager.StartCheck(req.MediaId);
                return Ok(new { success = true, checkId = req.MediaId });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Triggers an immediate active synthetic probe for the provided media id.
        /// </summary>
        /// <param name="req">Request containing a <c>MediaId</c>.</param>
        /// <returns>JSON success/failure payload.</returns>
        [HttpPost("ProbeNow")]
        public IActionResult ProbeNow([FromBody] RunNowRequest? req)
        {
            Console.WriteLine($"[FrameDropCheck] ProbeNow (Active Probe) Endpoint Hit. MediaId: {req?.MediaId}");
            if (req == null || string.IsNullOrWhiteSpace(req.MediaId))
            {
                return BadRequest(new { success = false, message = "mediaId is required in request body" });
            }

            try
            {
                _manager.StartProbe(req.MediaId);
                return Ok(new { success = true, checkId = req.MediaId });
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Streams real-time log messages from an ongoing check using Server-Sent Events (SSE).
        /// </summary>
        /// <param name="mediaId">The media id being checked.</param>
        /// <returns>Server-sent event stream of log messages.</returns>
        [HttpGet("Stream/{mediaId}")]
        public async Task StreamLogs(string mediaId)
        {
            Console.WriteLine($"[FrameDropCheck] StreamLogs Endpoint Hit for {mediaId}");

            // Normalize mediaId
            if (Guid.TryParse(mediaId, out var guid))
            {
                mediaId = guid.ToString();
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            var cts = new CancellationTokenSource();

            var logHandler = new EventHandler<LogMessageEventArgs>((sender, e) =>
            {
                if (e.MediaId != mediaId)
                {
                    return;
                }

                try
                {
                    var sseData = $"data: {JsonSerializer.Serialize(new { mediaId = e.MediaId, level = e.Level, message = e.Message, timestamp = e.Timestamp }, _jsonOptions)}\n\n";
                    Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseData), 0, sseData.Length, cts.Token).GetAwaiter().GetResult();
                    Response.Body.FlushAsync(cts.Token).GetAwaiter().GetResult();
                }
                catch
                {
                    /* Subscriber disconnected */
                }
            });

            var statusHandler = new EventHandler<CheckStatusEventArgs>((sender, e) =>
            {
                if (e.MediaId != mediaId)
                {
                    return;
                }

                try
                {
                    var sseData = $"data: {JsonSerializer.Serialize(new { type = "status", mediaId = e.MediaId, checkId = e.CheckId, status = e.Status, droppedFrames = e.DroppedFrames, totalFrames = e.TotalFrames, timestamp = e.Timestamp }, _jsonOptions)}\n\n";
                    Response.Body.WriteAsync(Encoding.UTF8.GetBytes(sseData), 0, sseData.Length, cts.Token).GetAwaiter().GetResult();
                    Response.Body.FlushAsync(cts.Token).GetAwaiter().GetResult();
                }
                catch
                {
                    /* Subscriber disconnected */
                }
            });

            try
            {
                _manager.LogMessageEmitted += logHandler;
                _manager.StatusChanged += statusHandler;

                // Keep the stream open as long as the check is running or until timeout
                while (!cts.IsCancellationRequested)
                {
                    if (!_manager.IsRunning(mediaId))
                    {
                        // Wait longer to ensure final logs and status are flushed to the client
                        await Task.Delay(5000, cts.Token).ConfigureAwait(false);
                        break;
                    }
                    await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _manager.LogMessageEmitted -= logHandler;
                _manager.StatusChanged -= statusHandler;
                cts.Dispose();
            }
        }

        /// <summary>
        /// Reports client-side playback statistics.
        /// </summary>
        /// <param name="json">The Raw JSON element to handle loose parsing.</param>
        /// <returns>JSON success/failure payload.</returns>
        [HttpPost("ReportStats")]
        [Consumes("application/json")]
        public async Task<IActionResult> ReportStats([FromBody] JsonElement json)
        {
            var req = new Api.Models.ReportStatsRequest();

            JsonElement? GetProp(string name)
            {
                if (json.TryGetProperty(name, out var p))
                {
                    return p;
                }

                if (json.TryGetProperty(char.ToLowerInvariant(name[0]) + name.Substring(1), out p))
                {
                    return p;
                }

                if (json.TryGetProperty(char.ToUpperInvariant(name[0]) + name.Substring(1), out p))
                {
                    return p;
                }
                return null;
            }

            try
            {
                var pMediaId = GetProp("MediaId");
                if (pMediaId.HasValue)
                {
                    req.MediaId = pMediaId.Value.GetString() ?? string.Empty;
                }

                var pDrops = GetProp("DroppedFrames");
                if (pDrops.HasValue)
                {
                    req.DroppedFrames = pDrops.Value.GetInt32();
                }

                var pDuration = GetProp("PlaybackDuration");
                if (pDuration.HasValue)
                {
                    if (pDuration.Value.TryGetDouble(out var d))
                    {
                        req.PlaybackDuration = d;
                    }
                    else if (pDuration.Value.TryGetInt32(out var i))
                    {
                        req.PlaybackDuration = i;
                    }
                }

                var pClient = GetProp("ClientName");
                if (pClient.HasValue)
                {
                    req.ClientName = pClient.Value.GetString() ?? string.Empty;
                }

                var pUser = GetProp("UserId");
                if (pUser.HasValue)
                {
                    req.UserId = pUser.Value.GetString() ?? string.Empty;
                }

                var pSession = GetProp("SessionId");
                if (pSession.HasValue)
                {
                    req.SessionId = pSession.Value.GetString();
                }
            }
            catch (Exception parseEx)
            {
                _logger.LogWarning($"[API] Failed to parse ReportStats JSON: {parseEx.Message}");
                return BadRequest(new { success = false, message = "Invalid JSON format" });
            }

            if (string.IsNullOrWhiteSpace(req.MediaId))
            {
                return BadRequest(new { success = false, message = "MediaId required" });
            }

            try
            {
                _logger.LogInformation($"[API] ReportStats received for MediaId: {req.MediaId}, Drops: {req.DroppedFrames}, Duration: {req.PlaybackDuration}s");

                // First, ensure the Media record exists
                try
                {
                    var existingMedia = await _mediaRepo.GetByIdAsync(req.MediaId).ConfigureAwait(false);
                    if (existingMedia == null)
                    {
                        _logger.LogInformation($"[API] Media {req.MediaId} not found in database, creating placeholder...");

                        var mediaName = "Unknown Media (Client Report)";

                        try
                        {
                            // GetMediaAsync handles DB lookup and internal API fallback
                            // However, GetByIdAsync above already checked DB and returned null.
                            // So GetMediaAsync will likely try internal API immediately (or retry DB).
                            // But InternalJellyfinApiService first checks DB via repository.
                            // To force internal API lookup, we might need to rely on the fact that DB is empty.

                            var item = await _jellyfinApi.GetMediaAsync(req.MediaId).ConfigureAwait(false);
                            if (item != null && !string.IsNullOrEmpty(item.Name))
                            {
                                mediaName = item.Name;
                                _logger.LogInformation($"[API] Resolved media name via Jellyfin API: {mediaName}");
                            }
                        }
                        catch (Exception apiEx)
                        {
                                _logger.LogWarning($"[API] Failed to resolve media name for {req.MediaId}: {apiEx.Message}");
                        }

                        // Create placeholder Media record
                        var media = new Domain.Models.Media
                        {
                            MediaId = req.MediaId,
                            Name = mediaName,
                            Path = string.Empty,
                            CreatedAt = DateTime.UtcNow,
                            OptimizationStatus = "Pending"
                        };

                        await _mediaRepo.AddAsync(media).ConfigureAwait(false);
                        _logger.LogInformation($"[API] Created placeholder Media for {req.MediaId}");
                    }
                    else if (existingMedia.Name.StartsWith("Unknown Media", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"[API] Existing media {req.MediaId} has placeholder name. Attempting to resolve real name...");
                        try
                        {
                            var item = await _jellyfinApi.ResolveMediaFromJellyfinAsync(req.MediaId).ConfigureAwait(false);
                            if (item != null && !string.IsNullOrEmpty(item.Name) && item.Name != existingMedia.Name)
                            {
                                existingMedia.Name = item.Name;
                                await _mediaRepo.UpdateAsync(existingMedia).ConfigureAwait(false);
                                _logger.LogInformation($"[API] Updated media name to: {item.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"[API] Failed to update media name: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[API] Could not ensure Media exists: {ex.Message}. Attempting to save stats anyway...");
                }

                var stats = new Domain.Models.ClientPlaybackStats
                {
                    MediaId = req.MediaId,
                    FramesDropped = req.DroppedFrames,
                    PlaybackDuration = req.PlaybackDuration,
                    ClientName = req.ClientName,
                    UserId = req.UserId,
                    JellyfinSessionId = req.SessionId,
                    Timestamp = DateTime.UtcNow,
                    ValidationResult = "ClientReported"
                };

                await _clientStatsRepo.UpsertAsync(stats).ConfigureAwait(false);
                _logger.LogInformation($"[API] Successfully saved client stats for MediaId: {req.MediaId}");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[API] Error in ReportStats: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cancels an ongoing check.
        /// </summary>
        /// <param name="req">Request containing the media identifier to cancel.</param>
        /// <returns>JSON success/failure payload.</returns>
        [HttpPost("Cancel")]
        public IActionResult CancelCheck([FromBody] RunNowRequest? req)
        {
            var mId = req?.MediaId;
            if (string.IsNullOrEmpty(mId))
            {
                return BadRequest(new { success = false, message = "MediaId required" });
            }

            _manager.CancelCheck(mId);
            return Ok(new { success = true, message = "Check cancellation requested" });
        }
    }
}
