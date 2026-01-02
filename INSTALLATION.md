# Installation Guide

This guide will help you install and configure the FrameDropCheck plugin for your Jellyfin server.

## Prerequisites

- Jellyfin Server 10.9.0 or higher
- .NET 8.0 Runtime (usually included with Jellyfin)
- FFmpeg installed on the server
- SSH access to your Jellyfin server (for manual installation)
- Basic knowledge of command line operations

## Installation Methods

### Method 1: From Jellyfin Repository (Recommended - When Available)

1. Open Jellyfin Dashboard
2. Navigate to **Plugins → Catalog**
3. Search for "FrameDropCheck"
4. Click **Install**
5. Restart Jellyfin

### Method 2: Manual Installation

#### Step 1: Build the Plugin

On your development machine:

```bash
git clone https://github.com/yourusername/FrameDropCheck.git
cd FrameDropCheck
dotnet publish FrameDropCheck.Plugin/FrameDropCheck.Plugin.csproj --configuration Release
```

#### Step 2: Locate Build Artifacts

The compiled DLL will be in:
```
FrameDropCheck.Plugin/bin/Release/net8.0/publish/
```

#### Step 3: Deploy to Jellyfin Server

**Find your Jellyfin data directory:**

| OS | Default Path |
|----|--------------|
| Linux | `/var/lib/jellyfin` or `/config` (Docker) |
| Windows | `C:\ProgramData\Jellyfin\Server` |
| macOS | `/var/lib/jellyfin` |

**Create plugin directory:**

```bash
# SSH into your server
ssh user@your-jellyfin-server

# Create plugin directory
mkdir -p /path/to/jellyfin/data/plugins/FrameDropCheck
```

**Copy files to server:**

```bash
# From your local machine
scp FrameDropCheck.Plugin/bin/Release/net8.0/publish/FrameDropCheck.Plugin.dll \
    FrameDropCheck.Plugin/bin/Release/net8.0/publish/Dapper.dll \
    FrameDropCheck.Plugin/plugin.json \
    user@your-server:/path/to/jellyfin/data/plugins/FrameDropCheck/
```

#### Step 4: Fix Permissions

**Important:** The plugin needs write access to create its database and log files.

**For Docker installations:**

```bash
# Find the UID/GID that Jellyfin runs as (usually 1000:1000)
docker exec jellyfin id

# Set correct permissions
chmod -R 755 /path/to/jellyfin/data/plugins/FrameDropCheck
chown -R 1000:1000 /path/to/jellyfin/data/plugins/FrameDropCheck
```

**For native installations:**

```bash
# Set permissions for the jellyfin user
chown -R jellyfin:jellyfin /path/to/jellyfin/data/plugins/FrameDropCheck
chmod -R 755 /path/to/jellyfin/data/plugins/FrameDropCheck
```

#### Step 5: Restart Jellyfin

**Docker:**
```bash
docker restart jellyfin
```

**Systemd:**
```bash
sudo systemctl restart jellyfin
```

**Windows:**
```powershell
Restart-Service Jellyfin
```

#### Step 6: Verify Installation

1. Open Jellyfin Dashboard
2. Navigate to **Plugins → My Plugins**
3. You should see "FrameDropCheck" listed with status "Active"
4. If you see "Deleted" status, check the logs for errors

## Troubleshooting

### Plugin shows "Deleted" status

**Cause:** Usually a permission issue or missing dependencies.

**Solution:**

1. Check Jellyfin logs:
   ```bash
   # Docker
   docker logs jellyfin --tail 100
   
   # Systemd
   journalctl -u jellyfin --tail 100
   ```

2. Look for errors related to FrameDropCheck

3. Common issues:
   - **Permission denied**: Fix permissions as shown in Step 4
   - **Missing Dapper.dll**: Ensure `Dapper.dll` was copied
   - **DLL version mismatch**: Rebuild the plugin against your Jellyfin version

### Database creation fails

**Cause:** Insufficient permissions to write to plugin directory.

**Solution:**

```bash
# On server
cd /path/to/jellyfin/data/plugins/FrameDropCheck
touch test.db
# If this fails, you have permission issues

# Fix:
chmod 777 /path/to/jellyfin/data/plugins/FrameDropCheck
# Or
chown -R $(docker exec jellyfin id -un) /path/to/jellyfin/data/plugins/FrameDropCheck
```

### FFmpeg not found

**Cause:** Plugin cannot locate FFmpeg executable.

**Solution:**

The plugin uses Jellyfin's FFmpeg path. Ensure FFmpeg is configured in:
- Dashboard → Playback → FFmpeg path

### Special Permissions Setup (Advanced)

If you have complex permission requirements (e.g., media files owned by different users):

**Option 1: Use ACLs**

```bash
# Grant jellyfin user read access to media
setfacl -R -m u:jellyfin:rx /path/to/media
setfacl -R -d -m u:jellyfin:rx /path/to/media
```

**Option 2: Add jellyfin to media group**

```bash
# Add jellyfin user to your media group
usermod -aG mediagroup jellyfin

# Restart Jellyfin
systemctl restart jellyfin
```

**Option 3: Docker volume permissions**

```yaml
# In docker-compose.yml
services:
  jellyfin:
    volumes:
      - /path/to/media:/media:ro  # Read-only is fine for this plugin
      - /path/to/config:/config
    user: "1000:1000"  # Match your media file ownership
```

## Configuration

After installation, configure the plugin:

1. Navigate to **Plugins → FrameDropCheck**
2. Set your preferences:
   - **Drop Threshold (%)**: Default 0.1% (frames dropped threshold)
   - **Maintenance Window**: When to run encoding tasks
   - **Hardware Acceleration**: Choose encoder (CPU, VAAPI, NVENC, QSV, V4L2)
   - **Encoding Preset**: Balance between speed and quality

## Verification

Test the plugin is working:

1. Go to plugin configuration page
2. Click **"Scan for frame drop issues"**
3. Check the logs:
   ```bash
   cat /path/to/jellyfin/data/plugins/FrameDropCheck/framedrop.log
   ```
4. You should see library synchronization and media scanning logs

## Uninstallation

1. Delete plugin directory:
   ```bash
   rm -rf /path/to/jellyfin/data/plugins/FrameDropCheck
   ```
2. Restart Jellyfin
3. (Optional) Remove injected web scripts:
   ```bash
   # The plugin cleans this up on uninstall, but you can verify:
   grep -i "framedropcheck" /path/to/jellyfin/web/index.html
   ```

## Getting Help

- Check the [DATABASE_QUERIES.md](DATABASE_QUERIES.md) for debugging
- Review plugin logs in `/path/to/jellyfin/data/plugins/FrameDropCheck/framedrop.log`
- Open an issue on GitHub with:
  - Jellyfin version
  - OS and installation method (Docker/native)
  - Relevant log excerpts
  - Steps to reproduce

## Security Notes

- This plugin requires:
  - Read access to your media files (to probe them)
  - Write access to media directories (to save encoded files)
  - Execute permissions for FFmpeg
- Encoded files are created with `[encoded]` suffix
- Original files can be automatically replaced (optional, disabled by default)
- The plugin database contains media paths and statistics (no authentication tokens)
