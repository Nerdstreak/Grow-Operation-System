using System.IO.Compression;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/system")]
[Produces("application/json")]
public sealed class SystemApiController : ApiControllerBase
{
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;

    public SystemApiController(AppPaths paths, GrowRepository repository)
    {
        _paths = paths;
        _repository = repository;
    }

    [HttpGet("backend-health")]
    [ProducesResponseType(typeof(BackendHealthDto), StatusCodes.Status200OK)]
    public ActionResult<BackendHealthDto> BackendHealth()
    {
        var tents = _repository.GetTents(includeArchived: true);
        var hydroSetups = _repository.GetHydroSetups(includeArchived: true);
        var grows = _repository.GetAllGrows();

        return Ok(new BackendHealthDto(
            AppName: "Grow OS",
            BackendSchema: "backend-core.v0.5-candidate",
            CheckedAtUtc: DateTime.UtcNow,
            TentCount: tents.Count,
            HydroSetupCount: hydroSetups.Count,
            GrowCount: grows.Count,
            ZeroTentStartupSupported: true,
            Capabilities: new[]
            {
                "zero-tent-startup",
                "tent-crud",
                "hydro-setup-dwc-rdwc-only",
                "grow-requires-hydro-setup",
                "hardware-hydro-setup-link",
                "addback-hydro-setup-volume",
                "addback-log",
                "changeout-log",
                "grow-export-v1",
                "local-backup-without-secrets"
            }));
    }

    [HttpGet("release-readiness")]
    [ProducesResponseType(typeof(BackendReleaseReadinessDto), StatusCodes.Status200OK)]
    public ActionResult<BackendReleaseReadinessDto> ReleaseReadiness()
    {
        var checks = new List<ReleaseReadinessCheckDto>
        {
            new("zero_tent_startup", "pass", "Frische Datenbanken starten ohne automatisch gesetzte Zelte."),
            new("tent_aggregate", "pass", "Zelte haben CRUD, Status, technische Details und Sensor-Mappings."),
            new("hydro_setup_aggregate", "pass", "HydroSetups sind DWC/RDWC-only, zeltgebunden und archivierungsfähig."),
            new("grow_hydro_setup_link", "pass", "Neue Grows benötigen ein aktives HydroSetup und übernehmen technische Systemdaten daraus."),
            new("hardware_hydro_setup_link", "pass", "HardwareItems können direkt an HydroSetups gebunden werden."),
            new("addback_volume", "pass", "Addback nutzt zuerst das HydroSetup-Gesamtvolumen und fällt nur für Legacy-Grows zurück."),
            new("operation_logs", "pass", "Addback- und Changeout-Protokolle existieren backendseitig."),
            new("grow_export_v1", "pass", "Grow-Export v1 enthält Grow, Messungen, Zelt-/HydroSetup-Snapshot, Logs und optionale Foto-Metadaten."),
            new("local_backup", "pass", "Lokale Backups schließen HA-Config, DataProtectionKeys, Uploads und Logs aus."),
            new("restore_api", "todo", "Ein validierter Restore-Flow ist noch nicht implementiert."),
            new("migration_engine", "todo", "Explizite versionierte Migrationen fehlen noch; aktuell arbeitet das Backend mit additiven Schema-Checks."),
            new("auth_remote", "todo", "Für echten Remote-Betrieb fehlt noch eine App-eigene Auth-/Setup-Key-Schicht."),
            new("import_merge", "todo", "Import und Merge von Grow-Exports sind noch nicht implementiert.")
        };

        return Ok(new BackendReleaseReadinessDto(
            Status: "backend.v0.5-ready-not-v1.0",
            BackendSchema: "backend-core.v0.5-candidate",
            CheckedAtUtc: DateTime.UtcNow,
            Checks: checks,
            CompletedFoundations: new[]
            {
                "zero-tent-startup",
                "tent-crud",
                "hydro-setup-dwc-rdwc-only",
                "grow-requires-hydro-setup",
                "hardware-hydro-setup-link",
                "addback-hydro-setup-volume",
                "addback-log",
                "changeout-log",
                "grow-export-v1",
                "local-backup-without-secrets"
            },
            RemainingBeforeV1: new[]
            {
                "versioned-database-migrations",
                "restore-flow",
                "grow-export-import-merge",
                "remote-auth-setup-key",
                "uniform-error-format-across-all-controllers",
                "release-upgrade-test-with-existing-app-data"
            }));
    }

    [HttpPost("backup")]
    [ProducesResponseType(typeof(BackupManifestDto), StatusCodes.Status201Created)]
    public ActionResult<BackupManifestDto> CreateBackup()
    {
        var backupRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "backups");
        Directory.CreateDirectory(backupRoot);

        var fileName = $"grow-os-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        var backupPath = Path.Combine(backupRoot, fileName);
        if (System.IO.File.Exists(backupPath))
        {
            System.IO.File.Delete(backupPath);
        }

        using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
        {
            AddIfExists(archive, _paths.DatabasePath, "App_Data/grow-diary.db");
            AddIfExists(archive, _paths.DatabasePath + "-wal", "App_Data/grow-diary.db-wal");
            AddIfExists(archive, _paths.DatabasePath + "-shm", "App_Data/grow-diary.db-shm");

            var knowledgeRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "knowledge");
            if (Directory.Exists(knowledgeRoot))
            {
                foreach (var file in Directory.EnumerateFiles(knowledgeRoot, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(knowledgeRoot, file).Replace(Path.DirectorySeparatorChar, '/');
                    archive.CreateEntryFromFile(file, $"App_Data/knowledge/{relative}");
                }
            }
        }

        var info = new FileInfo(backupPath);
        var downloadUrl = $"/api/system/backup/{Uri.EscapeDataString(fileName)}";
        return Created(downloadUrl, new BackupManifestDto(
            BackupSchema: "grow-os.backup.v1",
            CreatedAtUtc: DateTime.UtcNow,
            FileName: fileName,
            SizeBytes: info.Length,
            IncludesDatabase: System.IO.File.Exists(_paths.DatabasePath),
            IncludesWal: System.IO.File.Exists(_paths.DatabasePath + "-wal"),
            IncludesKnowledgeRuntimeCopy: Directory.Exists(Path.Combine(_paths.ContentRootPath, "App_Data", "knowledge")),
            ExcludesSecrets: true,
            ExcludesHomeAssistantConfig: true,
            ExcludesDataProtectionKeys: true,
            ExcludesUploads: true,
            RestoreSupported: false,
            DownloadUrl: downloadUrl));
    }

    [HttpGet("backup/{fileName}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult DownloadBackup(string fileName)
    {
        if (!IsSafeBackupFileName(fileName))
        {
            return BadRequest(new ApiError("invalid_backup_file", "Backup-Dateiname ist ungueltig."));
        }

        var backupRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "backups");
        var backupPath = Path.Combine(backupRoot, fileName);
        var fullRoot = Path.GetFullPath(backupRoot);
        var fullPath = Path.GetFullPath(backupPath);

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
        {
            return NotFoundError("backup_not_found", "Backup wurde nicht gefunden.");
        }

        return PhysicalFile(fullPath, "application/zip", fileName);
    }

    private static bool IsSafeBackupFileName(string fileName)
        => fileName.StartsWith("grow-os-backup-", StringComparison.OrdinalIgnoreCase)
           && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
           && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
           && !fileName.Contains('/')
           && !fileName.Contains('\\')
           && fileName.Length <= 120;

    private static void AddIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!System.IO.File.Exists(sourcePath))
        {
            return;
        }

        var entry = archive.CreateEntry(entryName);
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var destination = entry.Open();
        source.CopyTo(destination);
    }
}
