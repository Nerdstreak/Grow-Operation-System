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

        BackupsPath = Path.Combine(DataRootPath, "backups");
        HaConfigPath = Path.Combine(DataRootPath, "ha-config.json");
    }

    /// <summary>Der Ordner, in dem die Sicherungen liegen.</summary>
    /// <remarks>
    /// <para><b>Der Anlass (01.09.2026).</b> Sechs Stellen rechneten
    /// <c>ContentRootPath/App_Data/backups</c> von Hand aus — auf diesem
    /// Rechner derselbe Ordner wie <see cref="DataRootPath"/>, im Add-on
    /// <b>nicht</b>: dort ist ContentRoot <c>/app</c> und DataRoot
    /// <c>/data</c>. Nur <c>/data</c> ist als Volume deklariert und in den
    /// Sicherungen von Home Assistant enthalten.</para>
    ///
    /// <para><b>Was das kostete.</b> Jede Sicherung landete in der
    /// Schreibschicht des Containers und war beim nächsten Add-on-Update weg
    /// — ohne eine Meldung. Auch die Sicherheitskopie, die vor einem Import
    /// angelegt wird: der Rückweg, wenn der Import schiefgeht.</para>
    ///
    /// <para>Dieselbe Klasse wie bei den Fotos, die deshalb nie gelöscht
    /// wurden. Gehalten von <c>KeinPfadWirdVonHandGebautTests</c>.</para>
    /// </remarks>
    public string BackupsPath { get; }

    /// <summary>Die hinterlegte Home-Assistant-Konfiguration.</summary>
    public string HaConfigPath { get; }

    public string ContentRootPath { get; }

    /// <summary>Root of all mutable user data (/data in the add-on, App_Data otherwise).</summary>
    public string DataRootPath { get; }

    public string DatabasePath { get; }
    public string UploadRootPath { get; }
    public string SnapshotsPath { get; }
    public string DataProtectionKeysPath { get; }
    public string KnowledgeDefaultsPath { get; }
    public string KnowledgeDataPath { get; }
}
