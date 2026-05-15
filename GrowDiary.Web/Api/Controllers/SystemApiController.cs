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
            BackendSchema: "backend-core.v0.10-candidate",
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
                "api-contract-manifest",
                "grow-export-integrity",
                "grow-export-validation",
                "security-status",
                "local-only-admin-default",
                "admin-key-remote-guard",
                "schema-migration-status",
                "upgrade-preflight-backup"
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
            new("export_integrity", "pass", "Grow-Exports enthalten ExportId, SectionCounts und IntegrityHash."),
            new("import_validate", "pass", "Grow-Export-Dateien können serverseitig validiert werden, ohne Daten zu importieren."),
            new("security_guardrails", "pass", "Administrative System-, Settings- und Export-Endpunkte sind standardmaessig local-only; Remote-Adminzugriff ist nur mit Admin-Key oder bewusster Override-Variable moeglich."),
            new("security_status", "pass", "Das Backend stellt einen Security-Status fuer Remote-/Admin-Guardrails bereit."),
            new("migration_status", "pass", "Das Backend protokolliert angewendete Schema-Migrationen und zeigt offene Migrationen an."),
            new("upgrade_preflight", "pass", "Vor einem Update kann ein Preflight mit Datenbankstatus, Migrationstatus und validiertem Backup ausgeführt werden."),
            new("restore_api", "todo", "Ein validierter Restore-Flow ist noch nicht implementiert."),
            new("migration_engine", "partial", "Schema-Migrationen werden protokolliert; destructive Rollbacks und echte Restore-/Rollback-Automation fehlen noch."),
            new("auth_remote", "todo", "Für echten Remote-Betrieb fehlt noch eine App-eigene Auth-/Setup-Key-Schicht."),
            new("import_merge", "todo", "Import und Merge von Grow-Exports sind noch nicht implementiert.")
        };

        return Ok(new BackendReleaseReadinessDto(
            Status: "backend.v0.10-ready-not-v1.0",
            BackendSchema: "backend-core.v0.10-candidate",
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
                "api-contract-manifest",
                "grow-export-integrity",
                "grow-export-validation",
                "security-status",
                "local-only-admin-default",
                "admin-key-remote-guard",
                "schema-migration-status",
                "upgrade-preflight-backup"
            },
            RemainingBeforeV1: new[]
            {
                "destructive-restore-flow",
                "destructive-migration-rollback",
                "restore-flow",
                "grow-export-import-merge",
                "user-auth-session-management",
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
            "Grow-Exports müssen SectionCounts und IntegrityHash tragen, bevor sie importiert werden dürfen.",
            "Administrative System-, Settings- und Export-Endpunkte sind lokal/admin-geschützt.",
            "Remote-Adminzugriff ist standardmaessig blockiert und erfordert Admin-Key oder bewusste Override-Variable.",
            "Upgrade-Preflight erstellt vor riskanten Updates ein validierbares Backup.",
            "Schema-Migrationen werden in AppliedSchemaMigrations protokolliert.",
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
                    Endpoint("GET", "/api/exports/grows/{id}", "Grow exportieren.", true, "anonymize=true entfernt/neutralisiert nutzerbezogene Angaben.", "Export enthält ExportId, SectionCounts und IntegrityHash."),
                    Endpoint("POST", "/api/exports/grows/validate", "Grow-Export validieren, ohne Daten zu importieren.", true, "Prüft SchemaVersion, SectionCounts, IntegrityHash und potenzielle Secrets."),
                    Endpoint("GET", "/api/system/backend-health", "Backend-Zustand und Capabilities laden.", false),
                    Endpoint("GET", "/api/system/release-readiness", "Release-Readiness prüfen.", true),
                    Endpoint("GET", "/api/system/database-status", "Datenbankstatus und Pflichtschema prüfen.", true),
                    Endpoint("GET", "/api/system/api-manifest", "Maschinenlesbares API-Manifest laden.", true),
                    Endpoint("GET", "/api/system/security-status", "Security-Status und Remote-Admin-Guardrails laden.", true),
                    Endpoint("GET", "/api/system/migration-status", "Schema-Migrationen und offene Migrationen prüfen.", true),
                    Endpoint("POST", "/api/system/upgrade-preflight", "Update-Vorprüfung mit Datenbankstatus, Migrationstatus und validiertem Backup ausführen.", true),
                    Endpoint("POST", "/api/system/backup", "Lokales Backup ohne Secrets erstellen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}", "Backup herunterladen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}/validate", "Backup vor Restore validieren.", true)
                })
        };

        return Ok(new ApiManifestDto(
            SchemaVersion: "grow-os.api-manifest.v1",
            BackendSchema: "backend-core.v0.10-candidate",
            GeneratedAtUtc: DateTime.UtcNow,
            GlobalRules: globalRules,
            Areas: areas));
    }

    [HttpGet("security-status")]
    [ProducesResponseType(typeof(BackendSecurityStatusDto), StatusCodes.Status200OK)]
    public ActionResult<BackendSecurityStatusDto> SecurityStatus()
    {
        var warnings = new List<string>();
        if (AdminAccessPolicy.IsInsecureRemoteAdminOverrideActive())
        {
            warnings.Add("GROWDIARY_ALLOW_REMOTE_ADMIN=true ist ohne Admin-Key aktiv. Das ist nur fuer bewusst abgeschottete Testnetze gedacht.");
        }
        if (!AdminAccessPolicy.IsAdminKeyConfigured())
        {
            warnings.Add("Kein Admin-Key konfiguriert. Remote-Adminzugriff bleibt standardmaessig blockiert; nutze VPN/Tailscale/Cloudflare Access oder setze GROWDIARY_ADMIN_KEY.");
        }
        if (AdminAccessPolicy.IsAdminKeyConfigured())
        {
            warnings.Add("Admin-Key ist konfiguriert. Fuer Internet-Freigaben trotzdem HTTPS und einen externen Zugriffsschutz verwenden.");
        }

        var mode = AdminAccessPolicy.IsRemoteAdminExplicitlyAllowed()
            ? "remote-admin-override"
            : AdminAccessPolicy.IsAdminKeyConfigured()
                ? "remote-admin-key"
                : "local-only";

        return Ok(new BackendSecurityStatusDto(
            SecuritySchema: "grow-os.security.v1",
            CheckedAtUtc: DateTime.UtcNow,
            AdminAccessMode: mode,
            LocalOnlyAdminDefault: true,
            RemoteAdminExplicitlyAllowed: AdminAccessPolicy.IsRemoteAdminExplicitlyAllowed(),
            AdminKeyConfigured: AdminAccessPolicy.IsAdminKeyConfigured(),
            AdminKeyRequiredForRemoteAdmin: !AdminAccessPolicy.IsRemoteAdminExplicitlyAllowed(),
            InsecureRemoteAdminOverrideActive: AdminAccessPolicy.IsInsecureRemoteAdminOverrideActive(),
            AdminKeyHeaderName: AdminAccessPolicy.AdminKeyHeaderName,
            ProtectedRoutePrefixes: AdminAccessPolicy.ProtectedRoutePrefixes,
            RemoteAccessWarnings: warnings,
            RecommendedRemoteAccessModes: new[]
            {
                "Lokaler Zugriff im Heimnetz",
                "Tailscale oder VPN",
                "Cloudflare Tunnel mit Access/Zero-Trust-Regel",
                "Reverse Proxy mit HTTPS und externer Authentifizierung"
            },
            SecretHandling: new[]
            {
                "Home-Assistant-Token werden in API-Responses maskiert.",
                "Backups schliessen ha-config.json, DataProtectionKeys, Uploads und Logs aus.",
                "Grow-Exports pruefen potenzielle Secrets und tragen IntegrityHash.",
                "Admin-Key wird nur aus Environment gelesen und nicht in API-Responses ausgegeben."
            }));
    }

    [HttpGet("database-status")]
    [ProducesResponseType(typeof(DatabaseStatusDto), StatusCodes.Status200OK)]
    public ActionResult<DatabaseStatusDto> DatabaseStatus()
    {
        var requiredTables = new[]
        {
            "AppSettings", "Tents", "GrowSystems", "Grows", "Measurements", "HardwareItems",
            "AddbackLogs", "ChangeoutEntries", "JournalEntries", "GrowTasks", "HarvestEntries", "AppliedSchemaMigrations"
        };
        var requiredColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tents"] = new[] { "Id", "Name", "TentType", "Status", "UpdatedAtUtc" },
            ["GrowSystems"] = new[] { "Id", "TentId", "HydroStyle", "PotCount", "PotSizeLiters", "ReservoirLiters", "Status", "LayoutType", "ReservoirPosition" },
            ["Grows"] = new[] { "Id", "TentId", "SystemId", "HydroStyle", "ReservoirSize", "ContainerSize" },
            ["HardwareItems"] = new[] { "Id", "TentId", "HydroSetupId", "Name", "Category", "Status" },
            ["AddbackLogs"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "ReservoirLiters", "EcBefore", "EcTarget", "LitersAdded", "CreatedAtUtc" },
            ["ChangeoutEntries"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "VolumeChangedLiters", "Notes", "CreatedAtUtc" },
            ["AppliedSchemaMigrations"] = new[] { "Id", "Name", "RequiredForSchemaVersion", "AppliedAtUtc" }
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


    [HttpGet("migration-status")]
    [ProducesResponseType(typeof(SchemaMigrationStatusDto), StatusCodes.Status200OK)]
    public ActionResult<SchemaMigrationStatusDto> MigrationStatus()
    {
        return Ok(BuildMigrationStatus());
    }

    [HttpPost("upgrade-preflight")]
    [ProducesResponseType(typeof(UpgradePreflightDto), StatusCodes.Status200OK)]
    public ActionResult<UpgradePreflightDto> UpgradePreflight()
    {
        var databaseStatus = ExtractOk<DatabaseStatusDto>(DatabaseStatus());
        var migrationStatus = BuildMigrationStatus();
        var blockers = new List<string>();
        var warnings = new List<string>();
        BackupManifestDto? backupManifest = null;
        BackupValidationDto? backupValidation = null;

        if (!databaseStatus.DatabaseExists)
        {
            blockers.Add("Datenbankdatei existiert noch nicht.");
        }
        if (!databaseStatus.IsCurrent)
        {
            blockers.Add("Datenbankstatus ist nicht aktuell.");
        }
        if (!migrationStatus.IsCurrent)
        {
            blockers.Add("Schema-Migrationsstatus ist nicht aktuell.");
        }

        if (blockers.Count == 0)
        {
            var backupResult = CreateBackup();
            if (backupResult.Result is CreatedResult created && created.Value is BackupManifestDto manifest)
            {
                backupManifest = manifest;
                var validation = ValidateBackup(manifest.FileName);
                if (validation.Result is OkObjectResult ok && ok.Value is BackupValidationDto validationDto)
                {
                    backupValidation = validationDto;
                    if (!validationDto.IsValid)
                    {
                        blockers.Add("Preflight-Backup ist nicht valide.");
                    }
                }
                else
                {
                    blockers.Add("Preflight-Backup konnte nicht validiert werden.");
                }
            }
            else
            {
                blockers.Add("Preflight-Backup konnte nicht erstellt werden.");
            }
        }
        else
        {
            warnings.Add("Backup wurde nicht erstellt, weil der Preflight bereits Blocker gefunden hat.");
        }

        var isSafe = blockers.Count == 0 && backupManifest is not null && backupValidation?.IsValid == true;
        return Ok(new UpgradePreflightDto(
            PreflightSchema: "grow-os.upgrade-preflight.v1",
            CheckedAtUtc: DateTime.UtcNow,
            IsSafeToUpgrade: isSafe,
            DatabaseCurrent: databaseStatus.IsCurrent,
            BackupCreated: backupManifest is not null,
            BackupValid: backupValidation?.IsValid == true,
            BackupFileName: backupManifest?.FileName,
            BackupDownloadUrl: backupManifest?.DownloadUrl,
            Blockers: blockers,
            Warnings: warnings,
            DatabaseStatus: databaseStatus,
            MigrationStatus: migrationStatus,
            BackupValidation: backupValidation));
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



    private SchemaMigrationStatusDto BuildMigrationStatus()
    {
        var required = DatabaseInitializer.RequiredMigrations;
        var warnings = new List<string>();
        string? storedSchemaVersion = null;
        var tableExists = false;
        var applied = new List<AppliedSchemaMigrationDto>();

        if (!System.IO.File.Exists(_paths.DatabasePath))
        {
            return new SchemaMigrationStatusDto(
                MigrationSchema: "grow-os.schema-migrations.v1",
                CurrentSchemaVersion: DatabaseInitializer.CurrentSchemaVersion,
                StoredSchemaVersion: null,
                CheckedAtUtc: DateTime.UtcNow,
                MigrationTableExists: false,
                IsCurrent: false,
                AppliedMigrations: Array.Empty<AppliedSchemaMigrationDto>(),
                PendingMigrations: required.Select(m => new PendingSchemaMigrationDto(m.Id, m.Name, m.RequiredForSchemaVersion)).ToArray(),
                Warnings: new[] { "Datenbankdatei existiert noch nicht." });
        }

        using var connection = OpenReadConnection();
        storedSchemaVersion = ReadAppSetting(connection, DatabaseInitializer.CurrentSchemaAppSettingKey);
        tableExists = TableExists(connection, "AppliedSchemaMigrations");
        if (tableExists)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, RequiredForSchemaVersion, AppliedAtUtc FROM AppliedSchemaMigrations ORDER BY Id;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var rawAppliedAt = reader["AppliedAtUtc"]?.ToString();
                DateTime? appliedAt = DateTime.TryParse(rawAppliedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : null;
                applied.Add(new AppliedSchemaMigrationDto(
                    Id: reader["Id"]?.ToString() ?? string.Empty,
                    Name: reader["Name"]?.ToString() ?? string.Empty,
                    RequiredForSchemaVersion: reader["RequiredForSchemaVersion"]?.ToString() ?? string.Empty,
                    AppliedAtUtc: appliedAt));
            }
        }
        else
        {
            warnings.Add("AppliedSchemaMigrations-Tabelle fehlt.");
        }

        var appliedIds = applied.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = required
            .Where(m => !appliedIds.Contains(m.Id))
            .Select(m => new PendingSchemaMigrationDto(m.Id, m.Name, m.RequiredForSchemaVersion))
            .ToArray();

        if (!string.Equals(storedSchemaVersion, DatabaseInitializer.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            warnings.Add("Gespeicherte Schema-Version weicht von der erwarteten Backend-Version ab.");
        }
        if (pending.Length > 0)
        {
            warnings.Add("Es gibt offene Schema-Migrationen.");
        }

        return new SchemaMigrationStatusDto(
            MigrationSchema: "grow-os.schema-migrations.v1",
            CurrentSchemaVersion: DatabaseInitializer.CurrentSchemaVersion,
            StoredSchemaVersion: storedSchemaVersion,
            CheckedAtUtc: DateTime.UtcNow,
            MigrationTableExists: tableExists,
            IsCurrent: tableExists && pending.Length == 0 && string.Equals(storedSchemaVersion, DatabaseInitializer.CurrentSchemaVersion, StringComparison.Ordinal),
            AppliedMigrations: applied,
            PendingMigrations: pending,
            Warnings: warnings);
    }

    private static T ExtractOk<T>(ActionResult<T> actionResult)
    {
        if (actionResult.Result is OkObjectResult ok && ok.Value is T value)
        {
            return value;
        }

        throw new InvalidOperationException($"Expected OkObjectResult<{typeof(T).Name}>.");
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
