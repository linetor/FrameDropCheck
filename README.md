# Jellyfin FrameDropCheck Plugin 🚀

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Jellyfin](https://img.shields.io/badge/Jellyfin-10.9%2B-green.svg)](https://jellyfin.org/)

**FrameDropCheck** is a "Self-healing" media management plugin for Jellyfin. It automatically detects media files that perform poorly on your server (causing frame drops during transcoding) and proactively re-encodes them into a more compatible, high-quality format using Hardware Acceleration during off-peak hours.

## 📦 Quick Start

**Installation Guide**: See [INSTALLATION.md](INSTALLATION.md) for detailed setup instructions.

**Quick Install** (for Docker):
```bash
# Copy plugin files to Jellyfin
scp build/*.dll user@server:/path/to/jellyfin/data/plugins/FrameDropCheck/

# Restart Jellyfin
docker restart jellyfin
```

## ✨ Key Features

### 1. Proactive Health Monitoring

- **Active FFmpeg Probing**: Unlike passive monitoring, this plugin can run synthetic transcoding loads in the background to benchmark media performance without requiring user playback.
- **Real-time Detection**: Analyzes active FFmpeg transcode logs to pinpoint exactly where and why frames are being dropped.
- **Media Health Dashboard**: A built-in dashboard in the Jellyfin Configuration UI showing the health status, drop rates, and optimization history of your entire library.
- **Full Library Scan**: Utilizes Jellyfin's native `ILibraryManager` to synchronize and monitor **every video file** in your library, ensuring comprehensive coverage.

### 2. Automated Self-Healing

- **Scheduled Maintenance**: Runs heavy analysis and encoding jobs only during user-defined "Maintenance Windows" (e.g., 2 AM - 6 AM).
- **Hardware Acceleration Support**: Full support for hardware-accelerated encoding strategies:
    - **Raspberry Pi (V4L2)**: Utilizes `h264_v4l2m2m` for efficient encoding on ARM devices.
    - **VAAPI / QSV / NVENC**: Supports standard Intel/NVIDIA hardware acceleration.
- **Atomic Replacement**: Safely backups original files and replaces them with `_encoded` versions, automatically updating the Jellyfin library to reflect changes.

### 3. Technical Excellence

- **Real-time Streaming (SSE)**: Uses Server-Sent Events to stream live transcode logs and progress bars directly to your browser.
- **Persistent Inventory**: Integrated SQLite database using the Repository Pattern to track long-term media health metrics.
- **Conservative Action**: Uses a **MAX-based drop rate Policy**, meaning if a file exhibits a high drop rate *even once*, it remains flagged as "Action Required" until fixed.

## 🛠️ Configuration

The plugin provides a comprehensive settings page within Jellyfin:

- **Maintenance window**: Set the start and end time for automated tasks.
- **Drop Threshold**: Define what percentage of frame drops qualifies a file for automatic re-encoding (default: 5.0%).
- **Encoder Type**: Select the preferred hardware accelerator (CPU, VAAPI, NVENC, V4L2).
- **Target Bitrate**: Set the target bitrate for re-encoding (e.g., 5.0 Mbps).
- **Backup Path**: Choose where to store original files before replacement.

## 🚀 Installation & Build

### Prerequisites

- .NET 8.0 SDK
- FFmpeg installed on the host system (with appropriate hardware drivers if using HW accel)

### Build

```bash
dotnet build FrameDropCheck.Plugin/FrameDropCheck.Plugin.csproj -c Release
```

### Installation

1. Move the compiled `.dll` and `plugin.json` from `bin/Release/net8.0/publish` to your Jellyfin `plugins/FrameDropCheck` directory.
2. Restart Jellyfin.

---

## 📊 Technical Architecture

- **Core**: .NET 8.0 / C#
- **Database**: SQLite with Dapper (Repository Pattern)
- **UI**: Vanilla HTML/JS with Jellyfin Dashboard Integration
- **Inter-process**: FFmpeg execution via `System.Diagnostics.Process` with stderr parsing.

---

## 🔍 How Probing Works

When you click **"분석 실행" (Probe Now)** or during the nightly maintenance scan, the plugin performs a synthetic stress test using FFmpeg:

1. **Command**: Uses the currently selected **Hardware Encoder strategy** (e.g., `-c:v h264_v4l2m2m` for RPi) to simulate real-world performance accurately.
2. **Sampling**: Tests 3 specific points (10%, 50%, 90% of duration) for 10 seconds each.
3. **Metrics Captured**:
    - **Drop Rate**: The percentage of frames dropped during the test.
    - **Speed Factor**: If the conversion speed is below **1.0x**, it indicates that the server cannot transcode this file in real-time, which would cause buffering for users.

## 📊 Media Status Definitions

In the **Media Status Overview**, each file is assigned a status based on its latest analysis:

| Status | Description | Trigger |
| :--- | :--- | :--- |
| **Pending** | Default initial state. No data yet but discovered in library. | New media discovery. |
| **Healthy** | Media performs well. Drop rate is below threshold AND speed > 1.0x. | Successful probe/playback with few drops. |
| **Action Required** | Buffering likely. Drop rate exceeds threshold OR speed < 1.0x. | High drop rate during probe or playback. |
| **Optimized** | Successfully re-encoded into a high-performance format. | Managed encoding task finished. |
| **Failed** | An error occurred during the encoding process. | FFmpeg or system error during optimization. |

---
*Developed with the goal of making Jellyfin libraries truly hands-off and high-performance.*
