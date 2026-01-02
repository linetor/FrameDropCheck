using System.Data;
using Microsoft.Data.Sqlite;

namespace FrameDropCheck.Plugin.Infrastructure.Data;

/// <summary>
/// Represents the SQLite database context used by the plugin.
/// </summary>
public sealed class FrameDropCheckDbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameDropCheckDbContext"/> class.
    /// </summary>
    /// <param name="dbPath">The database file path.</param>
    public FrameDropCheckDbContext(string dbPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        InitializeDatabase();
    }

    /// <summary>
    /// Gets the database connection.
    /// </summary>
    public IDbConnection Connection => _connection;

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Media (
                MediaId TEXT PRIMARY KEY,
                Path TEXT NOT NULL,
                Name TEXT NOT NULL,
                Duration INTEGER,
                Size BIGINT,
                LastModified DATETIME,
                LastScanned DATETIME,
                IsProcessed BOOLEAN,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                OptimizationStatus TEXT DEFAULT 'Pending',
                AverageFrameDrops REAL,
                Codec TEXT,
                Resolution TEXT,
                Bitrate BIGINT,
                OriginalBitrate BIGINT,
                CompressionRatio REAL,
                LastScanSpeed REAL
            );

            CREATE TABLE IF NOT EXISTS FrameDropCheck (
                CheckId INTEGER PRIMARY KEY AUTOINCREMENT,
                MediaId TEXT NOT NULL,
                CheckStartTime DATETIME,
                CheckEndTime DATETIME,
                HasFrameDrop BOOLEAN,
                FrameDropCount INTEGER,
                TotalFrameCount INTEGER,
                LogAnalysisResult TEXT,
                PlaybackAnalysisResult TEXT,
                Status TEXT,
                FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
            );

            CREATE TABLE IF NOT EXISTS FrameDropDetail (
                DetailId INTEGER PRIMARY KEY AUTOINCREMENT,
                CheckId INTEGER NOT NULL,
                Timestamp DATETIME,
                DropType TEXT,
                TimeOffset INTEGER,
                Description TEXT,
                FOREIGN KEY (CheckId) REFERENCES FrameDropCheck(CheckId)
            );

            CREATE TABLE IF NOT EXISTS EncodingJob (
                JobId INTEGER PRIMARY KEY AUTOINCREMENT,
                MediaId TEXT NOT NULL,
                OriginalPath TEXT NOT NULL,
                BackupPath TEXT,
                NewFilePath TEXT,
                StartTime DATETIME,
                EndTime DATETIME,
                Status TEXT,
                ErrorMessage TEXT,
                FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
            );

            CREATE TABLE IF NOT EXISTS BackupCleanupLog (
                CleanupId INTEGER PRIMARY KEY AUTOINCREMENT,
                MediaId TEXT NOT NULL,
                BackupPath TEXT NOT NULL,
                CleanupTime DATETIME,
                FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
            );

            CREATE TABLE IF NOT EXISTS ClientPlaybackStats (
                Id TEXT PRIMARY KEY,
                MediaId TEXT NOT NULL,
                UserId TEXT,
                ClientName TEXT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FramesDropped INTEGER,
                PlaybackDuration REAL,
                ValidationResult TEXT,
                JellyfinSessionId TEXT,
                FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
            );
        ";
        command.ExecuteNonQuery();

        // Migration: Add missing columns if they don't exist
        AddColumnIfNotExists("Media", "OptimizationStatus", "TEXT DEFAULT 'Pending'");
        AddColumnIfNotExists("Media", "AverageFrameDrops", "REAL");
        AddColumnIfNotExists("Media", "AverageDropRate", "REAL");
        AddColumnIfNotExists("Media", "Codec", "TEXT");
        AddColumnIfNotExists("Media", "Resolution", "TEXT");
        AddColumnIfNotExists("Media", "Bitrate", "BIGINT");
        AddColumnIfNotExists("Media", "OriginalBitrate", "BIGINT");
        AddColumnIfNotExists("Media", "CompressionRatio", "REAL");
        AddColumnIfNotExists("Media", "LastScanSpeed", "REAL");
        AddColumnIfNotExists("FrameDropCheck", "TotalFrameCount", "INTEGER");
        AddColumnIfNotExists("ClientPlaybackStats", "JellyfinSessionId", "TEXT");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Internal migration logic with controlled inputs.")]
    private void AddColumnIfNotExists(string tableName, string columnName, string columnType)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pragma_table_info(@tableName) WHERE name = @columnName;";
        command.Parameters.Add(new SqliteParameter("@tableName", tableName));
        command.Parameters.Add(new SqliteParameter("@columnName", columnName));

        var exists = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;

        if (!exists)
        {
            // ALTER TABLE does not support parameters for table/column names in SQLite.
            // Since this is internal migration logic, we use string interpolation.
            command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
            command.Parameters.Clear();
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs resource cleanup.
    /// </summary>
    /// <param name="disposing">True to release managed resources; otherwise false.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _connection?.Dispose();
        }

        _disposed = true;
    }
}
