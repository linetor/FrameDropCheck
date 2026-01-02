using System;
using System.IO;
using System.Text.RegularExpressions;
using FrameDropCheck.Plugin.Infrastructure.Logging;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service to inject JavaScript into the Jellyfin web interface for enhanced monitoring.
/// </summary>
public class WebInjectionService
{
    private const string InjectionSnippetStart = "/* FrameDropCheck-Start */";
    private const string InjectionSnippetEnd = "/* FrameDropCheck-End */";
    private readonly IAppLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebInjectionService"/> class.
    /// </summary>
    /// <param name="logger">The application logger.</param>
    public WebInjectionService(IAppLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Injects the monitoring script into the Jellyfin web index.html file.
    /// </summary>
    public void InjectMonitoringScript()
    {
        try
        {
            var searchPaths = new[]
            {
                "/usr/share/jellyfin/web/index.html",
                "/jellyfin/jellyfin-web/index.html",
                "C:\\Program Files\\Jellyfin\\Server\\jellyfin-web\\index.html"
            };

            string? targetPath = null;
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    targetPath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                _logger.LogWarning("[WebInjectionService] Could not find index.html for injection.");
                return;
            }

            var content = File.ReadAllText(targetPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("[WebInjectionService] index.html is empty. Skipping injection.");
                return;
            }

            // DO NOT inject if it doesn't look like a valid HTML file starting with <!doctype
            if (!content.TrimStart().StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[WebInjectionService] index.html does not start with <!doctype. File might be corrupted. Skipping injection.");
                return;
            }

            var script = GenerateScript().Trim();
            string injectedContent;

            // Remove existing snippet if present
            if (content.Contains(InjectionSnippetStart, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[WebInjectionService] Old script found. Removing for update...");
                var regex = new Regex("<script>\\s*" + Regex.Escape(InjectionSnippetStart) + ".*?" + Regex.Escape(InjectionSnippetEnd) + "\\s*</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                content = regex.Replace(content, string.Empty);
            }

            // Always inject before </body>
            if (content.Contains("</body>", StringComparison.OrdinalIgnoreCase))
            {
                injectedContent = content.Replace("</body>", $"{script}\n</body>", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                _logger.LogWarning("[WebInjectionService] No </body> tag found. Appending to end of file.");
                injectedContent = content + "\n" + script;
            }

            // Final safety check
            if (!injectedContent.TrimStart().StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[WebInjectionService] Logic error: Resulting content would not start with <!doctype. Aborting.");
                return;
            }

            File.WriteAllText(targetPath, injectedContent);
            _logger.LogInformation("[WebInjectionService] Successfully injected/updated monitoring script at the end of index.html.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[WebInjectionService] Failed to inject monitoring script: {ex.Message}");
        }
    }

    private string GenerateScript()
    {
        return $@"
<script>
{InjectionSnippetStart}
(function() {{
    console.log('[FrameDropCheck] Client-side monitoring script loaded');

    function initMonitoring() {{
        if (window.FrameDropMonitoringStarted) return;
        window.FrameDropMonitoringStarted = true;
        console.log('[FrameDropCheck] Client-side monitoring active');

        let lastReportedDrop = -1;
        let lastMediaId = null;

        function report(mediaId, drops, currentTime) {{
            try {{
                const token = localStorage.getItem('EmbyHttpClient_Token') || (window.ApiClient ? window.ApiClient.accessToken() : '');
                let url = '/Plugins/FrameDropCheck/ReportStats';

                // Try to get full URL if ApiClient is available
                if (window.ApiClient && window.ApiClient.serverAddress) {{
                    url = window.ApiClient.serverAddress() + url;
                }} else if (window.location.origin) {{
                    url = window.location.origin + url;
                }}

                fetch(url, {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json',
                        'X-Emby-Token': token,
                        'X-MediaBrowser-Token': token
                    }},
                    body: JSON.stringify({{
                        MediaId: mediaId,
                        DroppedFrames: drops,
                        PlaybackDuration: Math.floor(currentTime),
                        ClientName: 'WebBrowser (' + navigator.userAgent.split(' ')[0] + ')',
                        UserId: '',
                        SessionId: ''
                    }})
                }})
                .then(r => {{
                    if (r.ok) console.log('[FrameDropCheck] Report successful. Drops:', drops);
                    else console.error('[FrameDropCheck] Server rejected report:', r.status);
                }})
                .catch(err => console.error('[FrameDropCheck] Network error', err));
            }} catch (e) {{ console.error('[FrameDropCheck] Report logic error', e); }}
        }}

        let debugCounter = 0;

        function findVideoElement(root) {{
            if (!root) return null;
            let v = root.querySelector('video');
            if (v) return v;

            // Deep search into Shadow DOM
            const allElements = root.querySelectorAll('*');
            for (let i = 0; i < allElements.length; i++) {{
                if (allElements[i].shadowRoot) {{
                    v = findVideoElement(allElements[i].shadowRoot);
                    if (v) return v;
                }}
            }}
            return null;
        }}

        setInterval(() => {{
            debugCounter++;

            // Use deep search instead of simple querySelector
            const video = findVideoElement(document);

            // Debug log every 20 seconds (every 4th iteration)
            if (debugCounter % 4 === 0) {{
                console.log('[FrameDropCheck] Debug - Video element:', video ? 'FOUND in ' + (video.getRootNode() === document ? 'Light DOM' : 'Shadow DOM') : 'NOT FOUND');
                if (video) {{
                    console.log('[FrameDropCheck] Debug - URL hash:', window.location.hash);
                    console.log('[FrameDropCheck] Debug - URL search:', window.location.search);
                }}
            }}

            if (!video) {{
                lastMediaId = null;
                lastReportedDrop = -1;
                return;
            }}

            // Try multiple methods to extract MediaId
            let mediaId = null;

            // Method 1: URL parameters
            const hash = window.location.hash || '';
            const search = window.location.search || '';
            const combined = hash + search;
            const urlParams = new URLSearchParams(combined.includes('?') ? combined.split('?')[1] : '');
            mediaId = urlParams.get('id') || urlParams.get('itemId');

            // Method 2: Jellyfin's playback manager
            if (!mediaId && window.playbackManager) {{
                try {{
                    const currentItem = window.playbackManager.currentItem();
                    if (currentItem && currentItem.Id) {{
                        mediaId = currentItem.Id;
                        if (debugCounter % 4 === 0) console.log('[FrameDropCheck] Debug - Got MediaId from playbackManager');
                    }}
                }} catch (e) {{}}
            }}

            // Method 3: Dashboard API
            if (!mediaId && window.Dashboard && typeof window.Dashboard.getCurrentItem === 'function') {{
                try {{
                    const item = window.Dashboard.getCurrentItem();
                    if (item && item.Id) {{
                        mediaId = item.Id;
                        if (debugCounter % 4 === 0) console.log('[FrameDropCheck] Debug - Got MediaId from Dashboard');
                    }}
                }} catch (e) {{}}
            }}

            // Method 4: ApiClient's current item
            if (!mediaId && window.ApiClient) {{
                try {{
                    if (window.ApiClient._currentPlayOptions && window.ApiClient._currentPlayOptions.items) {{
                        const items = window.ApiClient._currentPlayOptions.items;
                        if (items.length > 0 && items[0].Id) {{
                            mediaId = items[0].Id;
                            if (debugCounter % 4 === 0) console.log('[FrameDropCheck] Debug - Got MediaId from ApiClient');
                        }}
                    }}
                }} catch (e) {{}}
            }}

            // Method 5: Parse from video source URL
            if (!mediaId && video.src) {{
                try {{
                    const srcMatch = video.src.match(/\/Videos\/([a-f0-9-]+)\//i);
                    if (srcMatch && srcMatch[1]) {{
                        mediaId = srcMatch[1];
                        if (debugCounter % 4 === 0) console.log('[FrameDropCheck] Debug - Got MediaId from video.src');
                    }}
                }} catch (e) {{}}
            }}

            if (!mediaId) {{
                if (debugCounter % 4 === 0) {{
                    console.warn('[FrameDropCheck] Debug - MediaId NOT FOUND despite video element present');
                    console.log('[FrameDropCheck] Debug - video.src:', video.src);
                    console.log('[FrameDropCheck] Debug - playbackManager:', !!window.playbackManager);
                    console.log('[FrameDropCheck] Debug - ApiClient:', !!window.ApiClient);
                }}
                return;
            }}

            if (mediaId !== lastMediaId) {{
                console.log('[FrameDropCheck] Tracking:', mediaId);
                lastMediaId = mediaId;
                lastReportedDrop = -1;
            }}

            if (typeof video.getVideoPlaybackQuality !== 'function') {{
                if (debugCounter % 4 === 0) {{
                    console.warn('[FrameDropCheck] Debug - getVideoPlaybackQuality NOT SUPPORTED');
                }}
                return;
            }}

            const quality = video.getVideoPlaybackQuality();
            const currentDrops = quality.droppedVideoFrames;

            if (currentDrops > lastReportedDrop || lastReportedDrop === -1) {{
                console.log('[FrameDropCheck] Attempting report - MediaId:', mediaId, 'Drops:', currentDrops);
                lastReportedDrop = currentDrops;
                report(mediaId, currentDrops, video.currentTime);
            }}
        }}, 5000);
    }}

    // Try to init immediately and also on a delay just in case
    initMonitoring();
    setTimeout(initMonitoring, 5000);
}})();
{InjectionSnippetEnd}
</script>
";
    }
}
