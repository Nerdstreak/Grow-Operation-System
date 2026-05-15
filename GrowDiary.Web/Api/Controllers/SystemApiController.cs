using System.IO.Compression;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Models;
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
    private readonly SystemAuditRepository _auditRepository;

    public SystemApiController(AppPaths paths, GrowRepository repository, SystemAuditRepository auditRepository)
    {
        _paths = paths;
        _repository = repository;
        _auditRepository = auditRepository;
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
            BackendSchema: "backend-core.v0.14-candidate",
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
                "upgrade-preflight-backup",
                "backup-restore-plan",
                "grow-import-plan",
                "system-audit-events",
                "uniform-api-error-format"
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
            new("restore_plan", "pass", "Backups können als Restore-Dry-Run analysiert werden, ohne Dateien zu überschreiben."),
            new("grow_import_plan", "pass", "Grow-Exports können als Import-Dry-Run analysiert werden, ohne Daten zu schreiben."),
            new("system_audit_events", "pass", "Kritische Backend-Operationen werden in einem System-Audit-Log protokolliert."),
            new("api_error_format", "pass", "API-Fehler verwenden ein einheitliches ApiError-Format mit Code, Message, FieldErrors, Status, TraceId und SchemaVersion."),
            new("restore_api", "todo", "Ein destruktiver Restore-Flow ist noch nicht implementiert; Restore-Planung ist nur Read-only."),
            new("migration_engine", "partial", "Schema-Migrationen werden protokolliert; destructive Rollbacks und echte Restore-/Rollback-Automation fehlen noch."),
            new("auth_remote", "todo", "Für echten Remote-Betrieb fehlt noch eine App-eigene Auth-/Setup-Key-Schicht."),
            new("import_merge", "todo", "Import und Merge von Grow-Exports sind noch nicht implementiert.")
        };

        var dto = new BackendReleaseReadinessDto(
            Status: "backend.v0.14-ready-not-v1.0",
            BackendSchema: "backend-core.v0.14-candidate",
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
                "upgrade-preflight-backup",
                "backup-restore-plan",
                "grow-import-plan",
                "system-audit-events",
                "uniform-api-error-format"
            },
            RemainingBeforeV1: new[]
            {
                "destructive-restore-flow",
                "destructive-migration-rollback",
                "restore-flow",
                "destructive-grow-import-execute",
                "grow-export-import-merge",
                "user-auth-session-management",
                "release-upgrade-test-with-existing-app-data"
            });

        LogSystemAudit("system", "release-readiness-read", "Release-Readiness abgefragt.", true);
        return Ok(dto);
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
            "Import-Planung ist ein Dry-Run und schreibt keine Daten in die Datenbank.",
            "Administrative System-, Settings- und Export-Endpunkte sind lokal/admin-geschützt.",
            "Remote-Adminzugriff ist standardmaessig blockiert und erfordert Admin-Key oder bewusste Override-Variable.",
            "Upgrade-Preflight erstellt vor riskanten Updates ein validierbares Backup.",
            "Restore-Planung ist ein Dry-Run und überschreibt keine Dateien.",
            "Schema-Migrationen werden in AppliedSchemaMigrations protokolliert.",
            "Kritische Backend-Operationen werden im SystemAuditEvents-Log protokolliert.",
            "API-Fehler folgen dem einheitlichen grow-os.api-error.v1 Format.",
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
                    Endpoint("POST", "/api/exports/grows/import-plan", "Grow-Export als Import-Dry-Run analysieren, ohne Daten zu schreiben.", true, "Plant neue lokale IDs, Snapshot-Behandlung und Konflikte."),
                    Endpoint("GET", "/api/system/backend-health", "Backend-Zustand und Capabilities laden.", false),
                    Endpoint("GET", "/api/system/release-readiness", "Release-Readiness prüfen.", true),
                    Endpoint("GET", "/api/system/database-status", "Datenbankstatus und Pflichtschema prüfen.", true),
                    Endpoint("GET", "/api/system/api-manifest", "Maschinenlesbares API-Manifest laden.", true),
                    Endpoint("GET", "/api/system/security-status", "Security-Status und Remote-Admin-Guardrails laden.", true),
                    Endpoint("GET", "/api/system/audit-events", "System-Audit-Events fuer kritische Backend-Operationen laden.", true),
                    Endpoint("GET", "/api/system/error-contract", "Einheitlichen API-Fehlervertrag laden.", true),
                    Endpoint("GET", "/api/system/migration-status", "Schema-Migrationen und offene Migrationen prüfen.", true),
                    Endpoint("POST", "/api/system/upgrade-preflight", "Update-Vorprüfung mit Datenbankstatus, Migrationstatus und validiertem Backup ausführen.", true),
                    Endpoint("POST", "/api/system/backup", "Lokales Backup ohne Secrets erstellen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}", "Backup herunterladen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}/validate", "Backup vor Restore validieren.", true),
                    Endpoint("POST", "/api/system/backup/{fileName}/restore-plan", "Restore-Dry-Run erzeugen, ohne Dateien zu überschreiben.", true, "Prüft Backupvalidität, Schema-Kompatibilität und betroffene Zielpfade.")
                })
        };

        var dto = new ApiManifestDto(
            SchemaVersion: "grow-os.api-manifest.v1",
            BackendSchema: "backend-core.v0.14-candidate",
            GeneratedAtUtc: DateTime.UtcNow,
            GlobalRules: globalRules,
            Areas: areas);

        LogSystemAudit("system", "api-manifest-read", "API-Manifest abgefragt.", true);
        return Ok(dto);
    }

    [HttpGet("error-contract")]
    [ProducesResponseType(typeof(ApiErrorContractDto), StatusCodes.Status200OK)]
    public ActionResult<ApiErrorContractDto> ErrorContract()
    {
        var dto = new ApiErrorContractDto(
            SchemaVersion: ApiErrorFactory.SchemaVersion,
            Format: "ApiError",
            GeneratedAtUtc: DateTime.UtcNow,
            RequiredFields: new[] { "code", "message", "schemaVersion" },
            OptionalFields: new[] { "fieldErrors", "status", "traceId" },
            StandardCodes: new[]
            {
                "validation_failed",
                "not_found",
                "invalid_export",
                "invalid_backup_file",
                "admin_access_required",
                "active_sop_exists",
                "internal_server_error"
            },
            StandardStatuses: new[] { "400", "403", "404", "409", "500" },
            Rules: new[]
            {
                "Fehlerantworten enthalten immer code, message und schemaVersion.",
                "Validierungsfehler verwenden fieldErrors als Feldname-zu-Fehlerliste Dictionary.",
                "status und traceId werden gesetzt, wenn der Fehler über die Backend-Helper erzeugt wird.",
                "Controller sollen BadRequestError, NotFoundError, ConflictError, ForbiddenError oder ValidationError verwenden.",
                "Remote-Admin-Blockaden und unerwartete Fehler verwenden dasselbe ApiError-Format."
            });

        LogSystemAudit("system", "error-contract-read", "API-Error-Contract abgefragt.", true);
        return Ok(dto);
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

        var dto = new BackendSecurityStatusDto(
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
            });

        LogSystemAudit("security", "security-status-read", "Security-Status abgefragt.", true);
        return Ok(dto);
    }

    [HttpGet("audit-events")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemAuditEventDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SystemAuditEventDto>> AuditEvents([FromQuery] int limit = 100, [FromQuery] string? eventType = null)
    {
        var events = _auditRepository.GetRecent(limit, eventType).Select(entry => entry.ToDto()).ToList();
        LogSystemAudit(
            eventType: "system",
            action: "audit-events-read",
            summary: $"System-Audit-Events abgefragt ({events.Count} Eintrag/Eintraege).",
            success: true);
        return Ok(events);
    }

    [HttpGet("database-status")]
    [ProducesResponseType(typeof(DatabaseStatusDto), StatusCodes.Status200OK)]
    public ActionResult<DatabaseStatusDto> DatabaseStatus()
    {
        var requiredTables = new[]
        {
            "AppSettings", "Tents", "GrowSystems", "Grows", "Measurements", "HardwareItems",
            "AddbackLogs", "ChangeoutEntries", "JournalEntries", "GrowTasks", "HarvestEntries", "AppliedSchemaMigrations", "SystemAuditEvents"
        };
        var requiredColumns = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tents"] = new[] { "Id", "Name", "TentType", "Status", "UpdatedAtUtc" },
            ["GrowSystems"] = new[] { "Id", "TentId", "HydroStyle", "PotCount", "PotSizeLiters", "ReservoirLiters", "Status", "LayoutType", "ReservoirPosition" },
            ["Grows"] = new[] { "Id", "TentId", "SystemId", "HydroStyle", "ReservoirSize", "ContainerSize" },
            ["HardwareItems"] = new[] { "Id", "TentId", "HydroSetupId", "Name", "Category", "Status" },
            ["AddbackLogs"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "ReservoirLiters", "EcBefore", "EcTarget", "LitersAdded", "CreatedAtUtc" },
            ["ChangeoutEntries"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "VolumeChangedLiters", "Notes", "CreatedAtUtc" },
            ["AppliedSchemaMigrations"] = new[] { "Id", "Name", "RequiredForSchemaVersion", "AppliedAtUtc" },
            ["SystemAuditEvents"] = new[] { "Id", "EventType", "Action", "Summary", "Severity", "Source", "Success", "CreatedAtUtc" }
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
        var dto = BuildMigrationStatus();
        LogSystemAudit("system", "migration-status-read", "Migration-Status abgefragt.", dto.IsCurrent, severity: dto.IsCurrent ? "info" : "warning");
        return Ok(dto);
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
        LogSystemAudit("system", "upgrade-preflight-run", isSafe ? "Upgrade-Preflight erfolgreich ausgefuehrt." : "Upgrade-Preflight mit Blockern ausgefuehrt.", isSafe, relatedFileName: backupManifest?.FileName, severity: isSafe ? "info" : "warning");
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
            return BadRequestError("invalid_backup_file", "Backup-Dateiname ist ungueltig.");
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

        var dto = new BackupValidationDto(
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
            Warnings: warnings);

        LogSystemAudit("backup", "backup-validated", dto.IsValid ? "Backup erfolgreich validiert." : "Backup-Validierung mit Warnungen/Blockern abgeschlossen.", dto.IsValid, relatedFileName: fileName, severity: dto.IsValid ? "info" : "warning");
        return Ok(dto);
    }



    [HttpPost("backup/{fileName}/restore-plan")]
    [ProducesResponseType(typeof(BackupRestorePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<BackupRestorePlanDto> RestorePlan(string fileName)
    {
        if (!IsSafeBackupFileName(fileName))
        {
            return BadRequestError("invalid_backup_file", "Backup-Dateiname ist ungueltig.");
        }

        var backupPath = ResolveBackupPath(fileName);
        if (backupPath is null || !System.IO.File.Exists(backupPath))
        {
            return NotFoundError("backup_not_found", "Backup wurde nicht gefunden.");
        }

        var files = new List<BackupRestorePlanFileDto>();
        var blockers = new List<string>();
        var warnings = new List<string>
        {
            "Restore-Plan ist ein Dry-Run. Es wurden keine Dateien geaendert.",
            "Automatischer Restore ist noch nicht unterstuetzt; vor einem echten Restore muss die App gestoppt und ein aktuelles Backup erstellt werden."
        };

        string? backupSchemaVersion = null;
        bool containsDatabase;
        bool containsWal;
        bool containsShm;
        bool containsKnowledge;
        bool containsSecrets;
        bool containsKeys;
        bool containsUploads;
        bool hasUnsafeEntries;

        using (var archive = ZipFile.OpenRead(backupPath))
        {
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToArray();
            var normalizedEntries = entries
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .ToArray();

            containsDatabase = normalizedEntries.Contains("App_Data/grow-diary.db", StringComparer.OrdinalIgnoreCase);
            containsWal = normalizedEntries.Contains("App_Data/grow-diary.db-wal", StringComparer.OrdinalIgnoreCase);
            containsShm = normalizedEntries.Contains("App_Data/grow-diary.db-shm", StringComparer.OrdinalIgnoreCase);
            containsKnowledge = normalizedEntries.Any(entry => entry.StartsWith("App_Data/knowledge/", StringComparison.OrdinalIgnoreCase));
            containsSecrets = normalizedEntries.Any(entry =>
                entry.Equals("App_Data/ha-config.json", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("token", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("secret", StringComparison.OrdinalIgnoreCase));
            containsKeys = normalizedEntries.Any(entry => entry.StartsWith("App_Data/DataProtectionKeys/", StringComparison.OrdinalIgnoreCase));
            containsUploads = normalizedEntries.Any(entry => entry.StartsWith("wwwroot/uploads/", StringComparison.OrdinalIgnoreCase));
            hasUnsafeEntries = normalizedEntries.Any(IsUnsafeZipEntryName);

            foreach (var entry in entries)
            {
                var normalized = entry.FullName.Replace('\\', '/');
                var kind = ResolveRestoreEntryKind(normalized);
                if (kind is null)
                {
                    continue;
                }

                files.Add(new BackupRestorePlanFileDto(
                    EntryName: normalized,
                    RelativeTargetPath: normalized,
                    Kind: kind,
                    SizeBytes: entry.Length,
                    WouldOverwrite: WouldOverwriteRestoreTarget(normalized)));
            }

            if (containsDatabase)
            {
                backupSchemaVersion = ReadSchemaVersionFromBackupDatabase(archive, warnings);
            }
        }

        var backupValid = containsDatabase && !containsSecrets && !containsKeys && !containsUploads && !hasUnsafeEntries;
        var schemaCompatible = containsDatabase
            && !string.IsNullOrWhiteSpace(backupSchemaVersion)
            && string.Equals(backupSchemaVersion, DatabaseInitializer.CurrentSchemaVersion, StringComparison.Ordinal);

        if (!containsDatabase)
        {
            blockers.Add("Backup enthaelt keine Hauptdatenbank.");
        }
        if (containsSecrets)
        {
            blockers.Add("Backup enthaelt potenzielle Secrets.");
        }
        if (containsKeys)
        {
            blockers.Add("Backup enthaelt DataProtectionKeys.");
        }
        if (containsUploads)
        {
            blockers.Add("Backup enthaelt Upload-Dateien und ist nicht restore-ready.");
        }
        if (hasUnsafeEntries)
        {
            blockers.Add("Backup enthaelt unsichere Pfade.");
        }
        if (containsDatabase && !schemaCompatible)
        {
            blockers.Add("Backup-Schema ist nicht mit der aktuellen Backend-Version kompatibel.");
        }

        var dto = new BackupRestorePlanDto(
            RestorePlanSchema: "grow-os.restore-plan.v1",
            FileName: fileName,
            CheckedAtUtc: DateTime.UtcNow,
            BackupValid: backupValid,
            DatabaseIncluded: containsDatabase,
            WalIncluded: containsWal,
            ShmIncluded: containsShm,
            KnowledgeIncluded: containsKnowledge,
            SchemaCompatible: schemaCompatible,
            RestoreSupported: false,
            RequiresManualStop: true,
            WouldOverwriteExistingDatabase: System.IO.File.Exists(_paths.DatabasePath),
            BackupSchemaVersion: backupSchemaVersion,
            CurrentSchemaVersion: DatabaseInitializer.CurrentSchemaVersion,
            Files: files,
            Blockers: blockers,
            Warnings: warnings);

        LogSystemAudit("backup", "restore-plan-created", blockers.Count == 0 ? "Restore-Plan erfolgreich erstellt." : "Restore-Plan mit Blockern erstellt.", blockers.Count == 0, relatedFileName: fileName, severity: blockers.Count == 0 ? "info" : "warning");
        return Ok(dto);
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
        var manifest = new BackupManifestDto(
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
            DownloadUrl: downloadUrl);

        LogSystemAudit("backup", "backup-created", $"Backup {fileName} erstellt.", true, relatedFileName: fileName);
        return Created(downloadUrl, manifest);
    }

    [HttpGet("backup/{fileName}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult DownloadBackup(string fileName)
    {
        if (!IsSafeBackupFileName(fileName))
        {
            return BadRequestError("invalid_backup_file", "Backup-Dateiname ist ungueltig.");
        }

        var fullPath = ResolveBackupPath(fileName);
        if (fullPath is null || !System.IO.File.Exists(fullPath))
        {
            return NotFoundError("backup_not_found", "Backup wurde nicht gefunden.");
        }

        LogSystemAudit("backup", "backup-downloaded", $"Backup {fileName} heruntergeladen.", true, relatedFileName: fileName);
        return PhysicalFile(fullPath, "application/zip", fileName);
    }



    private void LogSystemAudit(string eventType, string action, string summary, bool success, string severity = "info", int? relatedGrowId = null, string? relatedFileName = null)
    {
        try
        {
            _auditRepository.Add(new SystemAuditEvent
            {
                EventType = eventType,
                Action = action,
                Summary = summary,
                Severity = severity,
                Source = "system-api",
                RelatedGrowId = relatedGrowId,
                RelatedFileName = relatedFileName,
                Success = success
            });
        }
        catch
        {
            // Audit logging must never break core system endpoints.
        }
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



    private static string? ResolveRestoreEntryKind(string entryName)
    {
        if (entryName.Equals("App_Data/grow-diary.db", StringComparison.OrdinalIgnoreCase))
        {
            return "database";
        }
        if (entryName.Equals("App_Data/grow-diary.db-wal", StringComparison.OrdinalIgnoreCase))
        {
            return "database-wal";
        }
        if (entryName.Equals("App_Data/grow-diary.db-shm", StringComparison.OrdinalIgnoreCase))
        {
            return "database-shm";
        }
        if (entryName.StartsWith("App_Data/knowledge/", StringComparison.OrdinalIgnoreCase))
        {
            return "knowledge";
        }

        return null;
    }

    private bool WouldOverwriteRestoreTarget(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var targetPath = Path.GetFullPath(Path.Combine(_paths.ContentRootPath, normalized));
        var root = Path.GetFullPath(_paths.ContentRootPath);
        return targetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
               && System.IO.File.Exists(targetPath);
    }

    private static bool IsUnsafeZipEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return true;
        }

        var normalized = entryName.Replace('\\', '/');
        return normalized.StartsWith("/", StringComparison.Ordinal)
               || normalized.Contains("../", StringComparison.Ordinal)
               || normalized.Contains("/..", StringComparison.Ordinal)
               || normalized.Equals("..", StringComparison.Ordinal);
    }

    private static string? ReadSchemaVersionFromBackupDatabase(ZipArchive archive, List<string> warnings)
    {
        var entry = archive.GetEntry("App_Data/grow-diary.db");
        if (entry is null)
        {
            return null;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "GrowOSRestorePlan_" + Guid.NewGuid().ToString("N"));
        var tempDb = Path.Combine(tempRoot, "grow-diary.db");
        try
        {
            Directory.CreateDirectory(tempRoot);
            entry.ExtractToFile(tempDb, overwrite: true);
            archive.GetEntry("App_Data/grow-diary.db-wal")?.ExtractToFile(tempDb + "-wal", overwrite: true);
            archive.GetEntry("App_Data/grow-diary.db-shm")?.ExtractToFile(tempDb + "-shm", overwrite: true);
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = tempDb,
                Mode = SqliteOpenMode.ReadOnly
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            return ReadAppSetting(connection, DatabaseInitializer.CurrentSchemaAppSettingKey);
        }
        catch
        {
            warnings.Add("Backup-Datenbank konnte nicht fuer die Schema-Pruefung gelesen werden.");
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }

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
