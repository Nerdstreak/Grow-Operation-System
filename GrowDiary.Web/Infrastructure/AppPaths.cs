namespace GrowDiary.Web.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string contentRootPath)
    {
        ContentRootPath = contentRootPath;

        // All mutable user data lives under this root. In the Home Assistant add-on it is
        // set to /data — the persistent volume that survives updates and is included in
        // Home Assistant backups. Otherwise it defaults to the project's App_Data folder.
        var configuredDataRoot = Environment.GetEnvironmentVariable("GROWDIARY_DATA_PATH");
        DataRootPath = !string.IsNullOrWhiteSpace(configuredDataRoot)
            ? configuredDataRoot
            : Path.Combine(contentRootPath, "App_Data");

        var configuredDbPath = Environment.GetEnvironmentVariable("GROWDIARY_DB_PATH");
        DatabasePath = !string.IsNullOrWhiteSpace(configuredDbPath)
            ? configuredDbPath
            : Path.Combine(DataRootPath, "grow-diary.db");

        UploadRootPath = Path.Combine(DataRootPath, "uploads");
        SnapshotsPath = Path.Combine(DataRootPath, "snapshots");
        DataProtectionKeysPath = Path.Combine(DataRootPath, "DataProtectionKeys");
        KnowledgeDataPath = Path.Combine(DataRootPath, "knowledge");

        KnowledgeDefaultsPath = Path.Combine(contentRootPath, "wwwroot", "knowledge-defaults");

        LegacyProjectDatabasePath = Path.Combine(contentRootPath, "App_Data", "legacy-grow-diary.db");
        LegacyWpfDatabasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Grow_Diary",
            "grow.db");
    }

    public string ContentRootPath { get; }

    /// <summary>Root of all mutable user data (/data in the add-on, App_Data otherwise).</summary>
    public string DataRootPath { get; }

    public string DatabasePath { get; }
    public string UploadRootPath { get; }
    public string SnapshotsPath { get; }
    public string DataProtectionKeysPath { get; }
    public string KnowledgeDefaultsPath { get; }
    public string KnowledgeDataPath { get; }
    public string LegacyProjectDatabasePath { get; }
    public string LegacyWpfDatabasePath { get; }
}
