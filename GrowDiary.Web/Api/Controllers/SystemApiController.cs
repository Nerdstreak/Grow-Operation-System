using System.IO.Compression;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using Microsoft.Data.Sqlite;
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
            BackendSchema: "backend-core.v0.7-candidate",
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
                "local-backup-without-secrets",
                "database-status",
                "backup-validation",
                "api-contract-manifest"
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
            new("database_status", "pass", "Das Backend stellt einen Datenbank-Status mit Schema-Version und Pflichttabellen-/Spaltencheck bereit."),
            new("backup_validation", "pass", "Backups können vor einem Restore auf Struktur und ausgeschlossene Secrets geprüft werden."),
            new("api_contract_manifest", "pass", "Das Backend liefert ein maschinenlesbares API-Manifest für Kernbereiche, Endpunkte und Produktregeln."),
            new("restore_api", "todo", "Ein validierter Restore-Flow ist noch nicht implementiert."),
            new("migration_engine", "todo", "Explizite versionierte Migrationen fehlen noch; aktuell arbeitet das Backend mit additiven Schema-Checks."),
            new("auth_remote", "todo", "Für echten Remote-Betrieb fehlt noch eine App-eigene Auth-/Setup-Key-Schicht."),
            new("import_merge", "todo", "Import und Merge von Grow-Exports sind noch nicht implementiert.")
        };

        return Ok(new BackendReleaseReadinessDto(
            Status: "backend.v0.7-ready-not-v1.0",
            BackendSchema: "backend-core.v0.7-candidate",
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
                "local-backup-without-secrets",
                "database-status",
                "backup-validation",
                "api-contract-manifest"
            },
            RemainingBeforeV1: new[]
            {
                "destructive-restore-flow",
                "versioned-database-migrations",
                "restore-flow",
                "grow-export-import-merge",
                "remote-auth-setup-key",
                "uniform-error-format-across-all-controllers",
                "release-upgrade-test-with-existing-app-data"
            }));
    }

    [HttpGet("api-manifest")]
    [ProducesResponseType(typeof(ApiManifestDto), StatusCodes.Status200OK)]
    public ActionResult<ApiManifestDto> ApiManifest()
    {
        var globalRules = new[]
        {
            "Frische Datenbanken starten ohne automatisch gesetzte Zelte.",
            "Neue Grows benötigen ein aktives HydroSetup.",
            "HydroSetups sind im MVP DWC/RDWC-only.",
            "Secrets wie Home-Assistant-Tokens dürfen nicht in API-Responses, Exports oder Backups erscheinen.",
            "Administrative System-Endpunkte sind lokal/admin-geschützt.",
            "Runtime-Daten aus App_Data werden nicht als Source-Artefakte behandelt."
        };

        var areas = new[]
        {
            new ApiAreaDto(
                Key: "tents",
                Title: "Zelte",
                Description: "Physische Räume für Klima, Licht, Sensoren und Systemzuordnung.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/settings/tents", "Zelte listen, optional inklusive archivierter Zelte.", true, "includeArchived=true lädt archivierte Zelte mit."),
                    Endpoint("GET", "/api/settings/tents/{id}", "Ein einzelnes Zelt mit technischen Details laden.", true),
                    Endpoint("POST", "/api/settings/tents", "Zelt anlegen.", true, "Name und TentType müssen gültig sein."),
                    Endpoint("PUT", "/api/settings/tents/{id}", "Zelt bearbeiten.", true, "Sensor-Mappings werden validiert."),
                    Endpoint("POST", "/api/settings/tents/{id}/archive", "Zelt archivieren.", true),
                    Endpoint("DELETE", "/api/settings/tents/{id}", "Zelt löschen oder bei Abhängigkeiten archivieren.", true)
                }),
            new ApiAreaDto(
                Key: "hydro-setups",
                Title: "HydroSetups",
                Description: "Technische DWC/RDWC-Systeme mit Volumen, Layout und Technikmerkmalen.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/hydro-setups", "HydroSetups listen.", false, "Standardmäßig nur aktive HydroSetups.", "includeArchived=true lädt archivierte HydroSetups mit."),
                    Endpoint("GET", "/api/hydro-setups?tentId={id}", "HydroSetups nach Zelt filtern.", false),
                    Endpoint("GET", "/api/hydro-setups/{id}", "Ein HydroSetup laden.", false),
                    Endpoint("POST", "/api/hydro-setups", "HydroSetup anlegen.", false, "Nur DWC oder RDWC erlaubt.", "TentId muss existieren.", "RDWC benötigt mindestens zwei Sites und eine Tankposition."),
                    Endpoint("PUT", "/api/hydro-setups/{id}", "HydroSetup bearbeiten.", false),
                    Endpoint("POST", "/api/hydro-setups/{id}/archive", "HydroSetup archivieren.", false)
                }),
            new ApiAreaDto(
                Key: "grows",
                Title: "Grows",
                Description: "Konkrete Pflanzenläufe, die an Zelt und HydroSetup hängen.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/grows", "Grows listen.", false),
                    Endpoint("GET", "/api/grows/{id}", "Grow-Details laden.", false),
                    Endpoint("POST", "/api/grows", "Neuen Grow erstellen.", false, "SystemId/HydroSetup ist für neue Grows Pflicht.", "HydroSetup muss aktiv sein und zum Zelt passen."),
                    Endpoint("PUT", "/api/grows/{id}", "Grow bearbeiten.", false, "Bestehende Legacy-Grows bleiben updatefähig."),
                    Endpoint("DELETE", "/api/grows/{id}", "Grow archivieren/löschen gemäß Repository-Regel.", false)
                }),
            new ApiAreaDto(
                Key: "operations",
                Title: "Addback, Changeout und Messungen",
                Description: "Betriebsprotokolle und Messdaten für DWC/RDWC-Grows.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/grows/{id}/addback", "Addback-Kontext laden.", false, "Volumen wird zuerst aus HydroSetup berechnet."),
                    Endpoint("POST", "/api/grows/{id}/addback/calculate", "Addback berechnen.", false),
                    Endpoint("GET", "/api/grows/{id}/addback/logs", "Addback-Protokoll laden.", false),
                    Endpoint("POST", "/api/grows/{id}/addback/logs", "Addback-Protokolleintrag speichern.", false),
                    Endpoint("GET", "/api/grows/{id}/changeouts", "Changeout-Protokoll laden.", false),
                    Endpoint("POST", "/api/grows/{id}/changeouts", "Changeout-Protokolleintrag speichern.", false),
                    Endpoint("GET", "/api/grows/{growId}/measurements", "Messwerte eines Grows laden.", false),
                    Endpoint("POST", "/api/grows/{growId}/measurements", "Messwert anlegen.", false)
                }),
            new ApiAreaDto(
                Key: "hardware",
                Title: "Hardware",
                Description: "Inventar, Sensoren, Wartung und Kalibrierung.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/hardware-items", "Hardware listen und filtern.", false, "Filter nach TentId, GrowId, SetupId oder HydroSetupId möglich."),
                    Endpoint("POST", "/api/hardware-items", "Hardware anlegen.", false, "HydroSetupId muss existieren, wenn gesetzt."),
                    Endpoint("PUT", "/api/hardware-items/{id}", "Hardware bearbeiten.", false),
                    Endpoint("GET", "/api/maintenance-events", "Wartungen listen.", false),
                    Endpoint("GET", "/api/calibration-events", "Kalibrierungen listen.", false),
                    Endpoint("GET", "/api/risk-events", "Risiken listen.", false)
                }),
            new ApiAreaDto(
                Key: "export-backup-system",
                Title: "Export, Backup und System",
                Description: "Produktnahe Systemendpunkte für Export, Backup, Schema und Release-Readiness.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/exports/grows/{id}", "Grow exportieren.", false, "anonymize=true entfernt/neutralisiert nutzerbezogene Angaben."),
                    Endpoint("GET", "/api/system/backend-health", "Backend-Zustand und Capabilities laden.", false),
                    Endpoint("GET", "/api/system/release-readiness", "Release-Readiness prüfen.", true),
                    Endpoint("GET", "/api/system/database-status", "Datenbankstatus und Pflichtschema prüfen.", true),
                    Endpoint("GET", "/api/system/api-manifest", "Maschinenlesbares API-Manifest laden.", true),
                    Endpoint("POST", "/api/system/backup", "Lokales Backup ohne Secrets erstellen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}", "Backup herunterladen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}/validate", "Backup vor Restore validieren.", true)
                })
        };

        return Ok(new ApiManifestDto(
            SchemaVersion: "grow-os.api-manifest.v1",
            BackendSchema: "backend-core.v0.7-candidate",
            GeneratedAtUtc: DateTime.UtcNow,
            GlobalRules: globalRules,
            Areas: areas));
    }

    [HttpGet("database-status")]
    [ProducesResponseType(typeof(DatabaseStatusDto), StatusCodes.Status200OK)]
    public ActionResult<DatabaseStatusDto> DatabaseStatus()
    {
        var requiredTables = new[]
        {
            "AppSettings", "Tents", "GrowSystems", "Grows", "Measurements", "HardwareItems",
            "AddbackLogs", "ChangeoutEntries", "JournalEntries", "GrowTasks", "HarvestEntries"
        };
        var requiredColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tents"] = new[] { "Id", "Name", "TentType", "Status", "UpdatedAtUtc" },
            ["GrowSystems"] = new[] { "Id", "TentId", "HydroStyle", "PotCount", "PotSizeLiters", "ReservoirLiters", "Status", "LayoutType", "ReservoirPosition" },
            ["Grows"] = new[] { "Id", "TentId", "SystemId", "HydroStyle", "ReservoirSize", "ContainerSize" },
            ["HardwareItems"] = new[] { "Id", "TentId", "HydroSetupId", "Name", "Category", "Status" },
            ["AddbackLogs"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "ReservoirLiters", "EcBefore", "EcTarget", "LitersAdded", "CreatedAtUtc" },
            ["ChangeoutEntries"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "VolumeChangedLiters", "Notes", "CreatedAtUtc" }
        };

        var presentTables = new List<string>();
        var missingTables = new List<string>();
        var presentColumns = new List<string>();
        var missingColumns = new List<string>();
        var warnings = new List<string>();
        string? storedSchemaVersion = null;

        if (!System.IO.File.Exists(_paths.DatabasePath))
        {
            return Ok(new DatabaseStatusDto(
                ExpectedSchemaVersion: DatabaseInitializer.CurrentSchemaVersion,
                StoredSchemaVersion: null,
                CheckedAtUtc: DateTime.UtcNow,
                DatabaseExists: false,
                IsCurrent: false,
                RequiredTablesPresent: Array.Empty<string>(),
                MissingRequiredTables: requiredTables,
                RequiredColumnsPresent: Array.Empty<string>(),
                MissingRequiredColumns: requiredColumns.SelectMany(pair => pair.Value.Select(column => $"{pair.Key}.{column}")).ToArray(),
                Warnings: new[] { "Datenbankdatei existiert noch nicht." }));
        }

        using var connection = OpenReadConnection();
        storedSchemaVersion = ReadAppSetting(connection, DatabaseInitializer.CurrentSchemaAppSettingKey);
        foreach (var table in requiredTables)
        {
            if (TableExists(connection, table))
            {
                presentTables.Add(table);
            }
            else
            {
                missingTables.Add(table);
            }
        }

        foreach (var pair in requiredColumns)
        {
            foreach (var column in pair.Value)
            {
                var qualified = $"{pair.Key}.{column}";
                if (ColumnExists(connection, pair.Key, column))
                {
                    presentColumns.Add(qualified);
                }
                else
                {
                    missingColumns.Add(qualified);
                }
            }
        }

        if (!string.Equals(storedSchemaVersion, DatabaseInitializer.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            warnings.Add("Gespeicherte Schema-Version weicht von der erwarteten Backend-Version ab.");
        }

        return Ok(new DatabaseStatusDto(
            ExpectedSchemaVersion: DatabaseInitializer.CurrentSchemaVersion,
            StoredSchemaVersion: storedSchemaVersion,
            CheckedAtUtc: DateTime.UtcNow,
            DatabaseExists: true,
            IsCurrent: missingTables.Count == 0
                && missingColumns.Count == 0
                && string.Equals(storedSchemaVersion, DatabaseInitializer.CurrentSchemaVersion, StringComparison.Ordinal),
            RequiredTablesPresent: presentTables,
            MissingRequiredTables: missingTables,
            RequiredColumnsPresent: presentColumns,
            MissingRequiredColumns: missingColumns,
            Warnings: warnings));
    }

    [HttpGet("backup/{fileName}/validate")]
    [ProducesResponseType(typeof(BackupValidationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<BackupValidationDto> ValidateBackup(string fileName)
    {
        if (!IsSafeBackupFileName(fileName))
        {
            return BadRequest(new ApiError("invalid_backup_file", "Backup-Dateiname ist ungueltig."));
        }

        var backupPath = ResolveBackupPath(fileName);
        if (backupPath is null || !System.IO.File.Exists(backupPath))
        {
            return NotFoundError("backup_not_found", "Backup wurde nicht gefunden.");
        }

        var warnings = new List<string>();
        using var archive = ZipFile.OpenRead(backupPath);
        var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        var containsDatabase = entries.Contains("App_Data/grow-diary.db", StringComparer.OrdinalIgnoreCase);
        var containsWal = entries.Contains("App_Data/grow-diary.db-wal", StringComparer.OrdinalIgnoreCase);
        var containsSecrets = entries.Any(entry =>
            entry.Equals("App_Data/ha-config.json", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("token", StringComparison.OrdinalIgnoreCase)
            || entry.Contains("secret", StringComparison.OrdinalIgnoreCase));
        var containsKeys = entries.Any(entry => entry.StartsWith("App_Data/DataProtectionKeys/", StringComparison.OrdinalIgnoreCase));
        var containsUploads = entries.Any(entry => entry.StartsWith("wwwroot/uploads/", StringComparison.OrdinalIgnoreCase));

        if (!containsDatabase)
        {
            warnings.Add("Backup enthält keine Hauptdatenbank.");
        }
        if (containsSecrets)
        {
            warnings.Add("Backup enthält potenzielle Secrets.");
        }
        if (containsKeys)
        {
            warnings.Add("Backup enthält DataProtectionKeys.");
        }
        if (containsUploads)
        {
            warnings.Add("Backup enthält Upload-Dateien.");
        }

        return Ok(new BackupValidationDto(
            BackupSchema: "grow-os.backup.v1",
            FileName: fileName,
            CheckedAtUtc: DateTime.UtcNow,
            Exists: true,
            IsValid: containsDatabase && !containsSecrets && !containsKeys && !containsUploads,
            ContainsDatabase: containsDatabase,
            ContainsWal: containsWal,
            ContainsSecrets: containsSecrets,
            ContainsDataProtectionKeys: containsKeys,
            ContainsUploads: containsUploads,
            EntryCount: entries.Count,
            Warnings: warnings));
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

        var fullPath = ResolveBackupPath(fileName);
        if (fullPath is null || !System.IO.File.Exists(fullPath))
        {
            return NotFoundError("backup_not_found", "Backup wurde nicht gefunden.");
        }

        return PhysicalFile(fullPath, "application/zip", fileName);
    }


    private string? ResolveBackupPath(string fileName)
    {
        var backupRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "backups");
        var backupPath = Path.Combine(backupRoot, fileName);
        var fullRoot = Path.GetFullPath(backupRoot);
        var fullPath = Path.GetFullPath(backupPath);

        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
    }


    private static ApiEndpointDto Endpoint(string method, string path, string purpose, bool localAdminOnly, params string[] rules)
        => new(
            Method: method,
            Path: path,
            Purpose: purpose,
            LocalAdminOnly: localAdminOnly,
            Rules: rules);

    private SqliteConnection OpenReadConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string? ReadAppSetting(SqliteConnection connection, string key)
    {
        if (!TableExists(connection, "AppSettings"))
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar()?.ToString();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        if (!TableExists(connection, tableName))
        {
            return false;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
