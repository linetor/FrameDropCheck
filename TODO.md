# 🚀 FrameDropCheck Future Roadmap & TODO

This document outlines future enhancements and ideas to make the FrameDropCheck plugin even more powerful and autonomous.

## 🛠️ Performance & Core Logic

- [x] **Hardware Acceleration Support**: Enable NVENC (Nvidia), VAAPI (Intel/AMD), or QuickSync support for the encoding engine to speed up optimization.
- [ ] **Multi-threaded Scanning**: Allow the `MediaOptimizationTask` to probe multiple files simultaneously (if CPU headroom allows).
- [ ] **Dynamic Windowing**: Automatically adjust the maintenance window based on historical server usage patterns.
- [ ] **Codec-Specific presets**: Allow users to define different CRF/Presets for H.264 vs H.265 files.

## 📊 Analytics & Integration

- [x] **Client-Side Data Integration**: Collect frame drop data directly from Jellyfin client apps (e.g., Android TV, Tizen) to identify network-related versus server-related drops.
- [ ] **Optimization ROI Report**: Show a report of "Estimated CPU Savings" after optimizing a library (comparing transcode speed before and after).
- [ ] **Discord/Webhook Notifications**: Send a summary of optimized files to a Discord/Slack channel after the maintenance window ends.

## 🌐 UI/UX Enhancements

- [ ] **Live Transcode Graph**: A real-time graph showing frame rate stability during a "Run Now" probe.
- [ ] **Bulk Actions**: Allow users to manually select multiple files from the Health Dashboard for immediate optimization.
- [ ] **Conflict Resolver**: Better UI for managing cases where a backup already exists or a file replacement fails due to permissions.

## 📂 File Management

- [ ] **Cloud Backup Support**: Option to move original high-resolution files to S3/Cloud Storage instead of a local backup folder.
- [ ] **Metadata Preservation**: Ensure all internal metadata (subtitles, multiple audio tracks, chapters) are perfectly preserved during the re-encoding process.

## 🎯 Optimal Encoding Strategy

- [ ] **Automated Best-Value Discovery**: Beyond just detecting drops, implement logic to find the highest quality settings that remain stable.
  - [ ] **Pi-Hardware Compatibility**: Ensure video codecs are always compatible with Raspberry Pi hardware acceleration (e.g., H.264/HEVC with internal GPU support).
  - [ ] **Target Resolution (FHD)**: Target 1920x1080 as the primary resolution for optimized files.
  - [ ] **Maximized Quality Tiers**: Iterate through Bitrate, Range Type, and Audio Bitrate to find the highest possible values that do not trigger frame drops.

---
*Feel free to contribute or suggest new ideas via Issues!*
