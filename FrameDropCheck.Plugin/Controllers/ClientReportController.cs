using System;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FrameDropCheck.Plugin.Controllers;

/// <summary>
/// Controller for receiving client-side playback reports.
/// </summary>
[ApiController]
[Route("Plugins/FrameDropCheck/ClientReport")]
public class ClientReportController : ControllerBase
{
    private readonly IClientPlaybackStatsRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientReportController"/> class.
    /// </summary>
    /// <param name="repository">The stats repository.</param>
    public ClientReportController(IClientPlaybackStatsRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Receives a playback report from a client.
    /// </summary>
    /// <param name="report">The playback report.</param>
    /// <returns>Result of the operation.</returns>
    [HttpPost]
    public async Task<IActionResult> ReceiveReport([FromBody] ClientPlaybackStats report)
    {
        if (report == null)
        {
            return BadRequest("Report body is required.");
        }

        if (string.IsNullOrEmpty(report.MediaId))
        {
            return BadRequest("MediaId is required.");
        }

        // Ensure ID and Timestamp are set if not provided
        // Ensure ID and Timestamp are set if not provided
        if (string.IsNullOrEmpty(report.Id))
        {
            report.Id = Guid.NewGuid().ToString();
        }

        if (report.Timestamp == default)
        {
            report.Timestamp = DateTime.UtcNow;
        }

        try
        {
            await _repository.AddAsync(report).ConfigureAwait(false);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            // Log exception here (omitted for brevity, need logger injection if desired)
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
