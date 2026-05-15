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
            BackendSchema: "backend-core.v0.18-candidate",
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
                "backup-restore-execute",
                "grow-import-plan",
                "system-audit-events",
                "uniform-api-error-format",
                "legacy-mvc-endpoint-containment",
                "remote-product-api-guard",
                "schema-migration-plan",
                "safe-migration-engine-foundation",
                "grow-run-snapshots"
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
            new("legacy_mvc_containment", "pass", "Alte MVC-Backup-/Export-/Kamera-/Mutationsrouten umgehen die neuen Backup-, Export- und Security-Regeln nicht mehr."),
            new("remote_product_api_guard", "pass", "Produkt-APIs sind bei Remote-Zugriff ebenfalls lokal/admin-geschuetzt; Mobile/PWA muss fuer echten Remote-Betrieb einen sicheren Zugriffskanal nutzen."),
            new("restore_api", "pass", "Backups koennen nach Preflight, Safety-Backup und Integritaetscheck kontrolliert wiederhergestellt werden."),
            new("migration_engine_foundation", "pass", "Migrationen besitzen einen maschinenlesbaren Plan, Backup-Pflicht und Destructive-Guardrails als Fundament."),
            new("grow_snapshots", "pass", "Neue Grows speichern unveränderliche Zelt- und HydroSetup-Snapshots für stabile Vergleiche und Exporte."),
            new("migration_engine", "partial", "Schema-Migrationen werden protokolliert; destructive Rollbacks und echte Restore-/Rollback-Automation fehlen noch."),
            new("auth_remote", "todo", "Für echten Remote-Betrieb fehlt noch eine App-eigene Auth-/Setup-Key-Schicht."),
            new("import_merge", "todo", "Import und Merge von Grow-Exports sind noch nicht implementiert.")
        };

        var dto = new BackendReleaseReadinessDto(
            Status: "backend.v0.19-ready-not-v1.0",
            BackendSchema: "backend-core.v0.18-candidate",
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
                "backup-restore-execute",
                "grow-import-plan",
                "system-audit-events",
                "uniform-api-error-format",
                "legacy-mvc-endpoint-containment",
                "remote-product-api-guard",
                "schema-migration-plan",
                "safe-migration-engine-foundation",
                "grow-run-snapshots"
            },
            RemainingBeforeV1: new[]
            {
                "destructive-migration-rollback",
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
            "Produkt-APIs sind fuer Remote-Zugriff ebenfalls lokal/admin-geschützt.",
            "Remote-Adminzugriff ist standardmaessig blockiert und erfordert Admin-Key oder bewusste Override-Variable.",
            "Upgrade-Preflight erstellt vor riskanten Updates ein validierbares Backup.",
            "Restore erfordert einen gueltigen Restore-Plan, Schema-Kompatibilitaet, Safety-Backup und Integritaetscheck.",
            "Schema-Migrationen werden in AppliedSchemaMigrations protokolliert.",
            "Migrationen liefern einen Dry-Run-Plan mit Backup-Pflicht und Destructive-Guardrails, bevor riskante Updates umgesetzt werden.",
            "Kritische Backend-Operationen werden im SystemAuditEvents-Log protokolliert.",
            "API-Fehler folgen dem einheitlichen grow-os.api-error.v1 Format.",
            "Runtime-Daten aus App_Data werden nicht als Source-Artefakte behandelt.",
            "Legacy-MVC-Endpunkte duerfen Backup, Export, Security oder Produktregeln nicht umgehen."
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
                    Endpoint("GET", "/api/hydro-setups", "HydroSetups listen.", true, "Standardmäßig nur aktive HydroSetups.", "includeArchived=true lädt archivierte HydroSetups mit."),
                    Endpoint("GET", "/api/hydro-setups?tentId={id}", "HydroSetups nach Zelt filtern.", true),
                    Endpoint("GET", "/api/hydro-setups/{id}", "Ein HydroSetup laden.", true),
                    Endpoint("POST", "/api/hydro-setups", "HydroSetup anlegen.", true, "Nur DWC oder RDWC erlaubt.", "TentId muss existieren.", "RDWC benötigt mindestens zwei Sites und eine Tankposition."),
                    Endpoint("PUT", "/api/hydro-setups/{id}", "HydroSetup bearbeiten.", true),
                    Endpoint("POST", "/api/hydro-setups/{id}/archive", "HydroSetup archivieren.", true)
                }),
            new ApiAreaDto(
                Key: "grows",
                Title: "Grows",
                Description: "Konkrete Pflanzenläufe, die an Zelt und HydroSetup hängen.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/grows", "Grows listen.", true),
                    Endpoint("GET", "/api/grows/{id}", "Grow-Details laden.", true),
                    Endpoint("POST", "/api/grows", "Neuen Grow erstellen.", true, "SystemId/HydroSetup ist für neue Grows Pflicht.", "HydroSetup muss aktiv sein und zum Zelt passen."),
                    Endpoint("PUT", "/api/grows/{id}", "Grow bearbeiten.", true, "Bestehende Legacy-Grows bleiben updatefähig."),
                    Endpoint("DELETE", "/api/grows/{id}", "Grow archivieren/löschen gemäß Repository-Regel.", true)
                }),
            new ApiAreaDto(
                Key: "operations",
                Title: "Addback, Changeout und Messungen",
                Description: "Betriebsprotokolle und Messdaten für DWC/RDWC-Grows.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/grows/{id}/addback", "Addback-Kontext laden.", true, "Volumen wird zuerst aus HydroSetup berechnet."),
                    Endpoint("POST", "/api/grows/{id}/addback/calculate", "Addback berechnen.", true),
                    Endpoint("GET", "/api/grows/{id}/addback/logs", "Addback-Protokoll laden.", true),
                    Endpoint("POST", "/api/grows/{id}/addback/logs", "Addback-Protokolleintrag speichern.", true),
                    Endpoint("GET", "/api/grows/{id}/changeouts", "Changeout-Protokoll laden.", true),
                    Endpoint("POST", "/api/grows/{id}/changeouts", "Changeout-Protokolleintrag speichern.", true),
                    Endpoint("GET", "/api/grows/{growId}/measurements", "Messwerte eines Grows laden.", true),
                    Endpoint("POST", "/api/grows/{growId}/measurements", "Messwert anlegen.", true)
                }),
            new ApiAreaDto(
                Key: "hardware",
                Title: "Hardware",
                Description: "Inventar, Sensoren, Wartung und Kalibrierung.",
                Endpoints: new[]
                {
                    Endpoint("GET", "/api/hardware-items", "Hardware listen und filtern.", true, "Filter nach TentId, GrowId, SetupId oder HydroSetupId möglich."),
                    Endpoint("POST", "/api/hardware-items", "Hardware anlegen.", true, "HydroSetupId muss existieren, wenn gesetzt."),
                    Endpoint("PUT", "/api/hardware-items/{id}", "Hardware bearbeiten.", true),
                    Endpoint("GET", "/api/maintenance-events", "Wartungen listen.", true),
                    Endpoint("GET", "/api/calibration-events", "Kalibrierungen listen.", true),
                    Endpoint("GET", "/api/risk-events", "Risiken listen.", true)
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
                    Endpoint("GET", "/grows/{id}/export", "Legacy-Export-Route; leitet auf /api/exports/grows/{id} um und liefert keine alten Rohdaten mehr.", false),
                    Endpoint("GET", "/settings/backup", "Legacy-Backup-Route; direkter SQLite-Download ist deaktiviert.", true),
                    Endpoint("GET", "/tents/{id}/camera.jpg", "Legacy-Kamera-Snapshot; lokal/admin-geschützt.", true),
                    Endpoint("GET", "/tents/{id}/camera-stream", "Legacy-Kamera-Stream; lokal/admin-geschützt.", true),
                    Endpoint("GET", "/tents/{id}/latest-snapshot", "Legacy-Snapshot; lokal/admin-geschützt.", true),
                    Endpoint("GET", "/api/system/backend-health", "Backend-Zustand und Capabilities laden.", false),
                    Endpoint("GET", "/api/system/release-readiness", "Release-Readiness prüfen.", true),
                    Endpoint("GET", "/api/system/database-status", "Datenbankstatus und Pflichtschema prüfen.", true),
                    Endpoint("GET", "/api/system/api-manifest", "Maschinenlesbares API-Manifest laden.", true),
                    Endpoint("GET", "/api/system/security-status", "Security-Status und Remote-Admin-Guardrails laden.", true),
                    Endpoint("GET", "/api/system/audit-events", "System-Audit-Events fuer kritische Backend-Operationen laden.", true),
                    Endpoint("GET", "/api/system/error-contract", "Einheitlichen API-Fehlervertrag laden.", true),
                    Endpoint("GET", "/api/system/migration-status", "Schema-Migrationen und offene Migrationen prüfen.", true),
                    Endpoint("GET", "/api/system/migration-plan", "Schema-Migrationsplan als Dry-Run laden.", true, "Zeigt Pending/Applied, Backup-Pflicht und destructive Guardrails."),
                    Endpoint("POST", "/api/system/upgrade-preflight", "Update-Vorprüfung mit Datenbankstatus, Migrationstatus und validiertem Backup ausführen.", true),
                    Endpoint("POST", "/api/system/backup", "Lokales Backup ohne Secrets erstellen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}", "Backup herunterladen.", true),
                    Endpoint("GET", "/api/system/backup/{fileName}/validate", "Backup vor Restore validieren.", true),
                    Endpoint("POST", "/api/system/backup/{fileName}/restore-plan", "Restore-Dry-Run erzeugen, ohne Dateien zu überschreiben.", true, "Prüft Backupvalidität, Schema-Kompatibilität und betroffene Zielpfade."),
                    Endpoint("POST", "/api/system/backup/{fileName}/restore", "Backup kontrolliert wiederherstellen.", true, "Erstellt vor dem Restore automatisch ein Safety-Backup.", "Restored nur schema-kompatible, validierte Backups.", "Fuehrt SQLite-Integritaetscheck vor dem Swap aus.")
                })
        };

        var dto = new ApiManifestDto(
            SchemaVersion: "grow-os.api-manifest.v1",
            BackendSchema: "backend-core.v0.18-candidate",
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
        warnings.Add("Produkt-APIs sind fuer Remote-Zugriff geschuetzt. Mobile/PWA-Zugriff ausserhalb des Servers braucht Admin-Key, VPN/Tailscale oder Cloudflare Access.");

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
                "Admin-Key wird nur aus Environment gelesen und nicht in API-Responses ausgegeben.",
                "Produkt-APIs sind nicht mehr als ungeschuetzte Remote-Oberflaeche gedacht."
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
            ["Grows"] = new[] { "Id", "TentId", "SystemId", "HydroStyle", "ReservoirSize", "ContainerSize", "TentSnapshotJson", "HydroSetupSnapshotJson", "SnapshotsCapturedAtUtc" },
            ["HardwareItems"] = new[] { "Id", "TentId", "HydroSetupId", "Name", "Category", "Status" },
            ["AddbackLogs"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "ReservoirLiters", "EcBefore", "EcTarget", "LitersAdded", "CreatedAtUtc" },
            ["ChangeoutEntries"] = new[] { "Id", "GrowId", "HydroSetupId", "Kind", "PerformedAtUtc", "VolumeChangedLiters", "Notes", "CreatedAtUtc" },
            ["AppliedSchemaMigrations"] = new[] { "Id", "Name", "RequiredForSchemaVersion", "AppliedAtUtc", "Status", "CompletedAtUtc", "RequiresBackup", "IsDestructive", "EngineVersion" },
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

    [HttpGet("migration-plan")]
    [ProducesResponseType(typeof(SchemaMigrationPlanDto), StatusCodes.Status200OK)]
    public ActionResult<SchemaMigrationPlanDto> MigrationPlan()
    {
        var dto = BuildMigrationPlan();
        LogSystemAudit("system", "migration-plan-read", "Migration-Plan abgefragt.", true);
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
            "Echter Restore ist nur ueber den Restore-Endpunkt mit Safety-Backup, Schema-Pruefung und Integritaetscheck erlaubt."
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
            RestoreSupported: blockers.Count == 0 && backupValid && schemaCompatible,
            RequiresManualStop: false,
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

        var fileName = CreateUniqueBackupFileName(backupRoot);
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
            RestoreSupported: true,
            DownloadUrl: downloadUrl);

        LogSystemAudit("backup", "backup-created", $"Backup {fileName} erstellt.", true, relatedFileName: fileName);
        return Created(downloadUrl, manifest);
    }


    [HttpPost("backup/{fileName}/restore")]
    [ProducesResponseType(typeof(BackupRestoreResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public ActionResult<BackupRestoreResultDto> RestoreBackup(string fileName)
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

        BackupRestorePlanDto plan;
        var planResult = RestorePlan(fileName);
        if (planResult.Result is OkObjectResult ok && ok.Value is BackupRestorePlanDto dto)
        {
            plan = dto;
        }
        else
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiErrorFactory.ServerError("restore_plan_failed", "Restore-Plan konnte vor dem Restore nicht erstellt werden.", HttpContext?.TraceIdentifier));
        }

        if (plan.Blockers.Count > 0 || !plan.BackupValid || !plan.SchemaCompatible || !plan.RestoreSupported)
        {
            LogSystemAudit("backup", "restore-blocked", "Restore wurde durch Preflight-Blocker verhindert.", false, relatedFileName: fileName, severity: "warning");
            return BadRequestError("restore_blocked", "Restore wurde blockiert: " + string.Join(" ", plan.Blockers.DefaultIfEmpty("Backup ist nicht restore-ready.")));
        }

        BackupManifestDto safetyBackup;
        var safetyBackupResult = CreateBackup();
        if (safetyBackupResult.Result is CreatedResult created && created.Value is BackupManifestDto manifest)
        {
            safetyBackup = manifest;
        }
        else
        {
            LogSystemAudit("backup", "restore-safety-backup-failed", "Restore wurde abgebrochen, weil kein Safety-Backup erstellt werden konnte.", false, relatedFileName: fileName, severity: "error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiErrorFactory.ServerError("restore_safety_backup_failed", "Safety-Backup konnte vor dem Restore nicht erstellt werden.", HttpContext?.TraceIdentifier));
        }

        var restoreId = Guid.NewGuid().ToString("N");
        var tempRoot = Path.Combine(Path.GetTempPath(), "GrowOSRestore_" + restoreId);
        var rollbackRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "restore-rollback-" + restoreId);
        var restoredKnowledgeFiles = new List<string>();
        var warnings = new List<string>(plan.Warnings);

        try
        {
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(rollbackRoot);
            ZipFile.ExtractToDirectory(backupPath, tempRoot);

            var extractedDb = Path.Combine(tempRoot, "App_Data", "grow-diary.db");
            var extractedWal = extractedDb + "-wal";
            var extractedShm = extractedDb + "-shm";

            if (!System.IO.File.Exists(extractedDb))
            {
                throw new InvalidOperationException("Backup enthaelt keine extrahierbare Hauptdatenbank.");
            }

            var integrityResult = RunSqliteQuickCheck(extractedDb);
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backup-Datenbank hat den SQLite-Integritaetscheck nicht bestanden: " + integrityResult);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabasePath)!);
            RestoreFileWithRollback(extractedDb, _paths.DatabasePath, rollbackRoot, "grow-diary.db");
            RestoreOptionalFileWithRollback(extractedWal, _paths.DatabasePath + "-wal", rollbackRoot, "grow-diary.db-wal");
            RestoreOptionalFileWithRollback(extractedShm, _paths.DatabasePath + "-shm", rollbackRoot, "grow-diary.db-shm");

            var extractedKnowledgeRoot = Path.Combine(tempRoot, "App_Data", "knowledge");
            if (Directory.Exists(extractedKnowledgeRoot))
            {
                var targetKnowledgeRoot = Path.Combine(_paths.ContentRootPath, "App_Data", "knowledge");
                RestoreDirectoryWithRollback(extractedKnowledgeRoot, targetKnowledgeRoot, rollbackRoot, "knowledge");
                restoredKnowledgeFiles.AddRange(
                    Directory.EnumerateFiles(extractedKnowledgeRoot, "*", SearchOption.AllDirectories)
                        .Select(file => "App_Data/knowledge/" + Path.GetRelativePath(extractedKnowledgeRoot, file).Replace(Path.DirectorySeparatorChar, '/'))
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
            }

            DeleteDirectoryBestEffort(rollbackRoot);

            var result = new BackupRestoreResultDto(
                RestoreSchema: "grow-os.backup-restore.v1",
                FileName: fileName,
                RestoredAtUtc: DateTime.UtcNow,
                Success: true,
                SafetyBackupFileName: safetyBackup.FileName,
                SafetyBackupDownloadUrl: safetyBackup.DownloadUrl,
                DatabaseTargetPath: "App_Data/grow-diary.db",
                DatabaseRestored: true,
                WalRestored: System.IO.File.Exists(extractedWal),
                ShmRestored: System.IO.File.Exists(extractedShm),
                KnowledgeFileCount: restoredKnowledgeFiles.Count,
                RestoredKnowledgeFiles: restoredKnowledgeFiles,
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());

            LogSystemAudit("backup", "backup-restored", $"Backup {fileName} wiederhergestellt. Safety-Backup: {safetyBackup.FileName}.", true, relatedFileName: fileName, severity: "warning");
            return Ok(result);
        }
        catch (Exception ex)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                RestoreRollbackFiles(rollbackRoot);
            }
            catch
            {
                // Best-effort rollback only. Safety backup still exists.
            }

            LogSystemAudit("backup", "restore-failed", "Restore fehlgeschlagen: " + ex.Message, false, relatedFileName: fileName, severity: "error");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                ApiErrorFactory.ServerError("restore_failed", "Restore ist fehlgeschlagen. Safety-Backup wurde erstellt: " + safetyBackup.FileName + ". Fehler: " + ex.Message, HttpContext?.TraceIdentifier));
        }
        finally
        {
            DeleteDirectoryBestEffort(tempRoot);
            DeleteDirectoryBestEffort(rollbackRoot);
        }
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

    private SchemaMigrationPlanDto BuildMigrationPlan()
    {
        var status = BuildMigrationStatus();
        var appliedIds = status.AppliedMigrations.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = DatabaseInitializer.RequiredMigrations
            .Select(m => new SchemaMigrationPlanItemDto(
                Id: m.Id,
                Name: m.Name,
                RequiredForSchemaVersion: m.RequiredForSchemaVersion,
                Status: appliedIds.Contains(m.Id) ? "applied" : "pending",
                IsDestructive: m.IsDestructive,
                RequiresBackup: m.RequiresBackup || m.IsDestructive,
                ExecutionMode: m.IsDestructive ? "manual-blocked" : "idempotent",
                Checksum: m.Checksum))
            .ToArray();

        var pending = items.Where(item => string.Equals(item.Status, "pending", StringComparison.OrdinalIgnoreCase)).ToArray();
        var blockers = new List<string>();
        var warnings = new List<string>
        {
            "Migration-Plan ist ein Dry-Run. Dieser Endpoint führt keine Migration aus.",
            "Echte destructive Migrationen bleiben blockiert, bis Backup, Restore und manueller Rollback-Prozess vollständig implementiert sind."
        };

        if (pending.Any(item => item.IsDestructive))
        {
            blockers.Add("Mindestens eine destructive Migration ist pending und darf nicht automatisch ausgeführt werden.");
        }

        if (!status.IsCurrent)
        {
            warnings.Add("Migration-Status ist nicht vollständig aktuell; Upgrade-Preflight sollte vor jeder neuen Version ausgeführt werden.");
        }

        return new SchemaMigrationPlanDto(
            PlanSchema: "grow-os.schema-migration-plan.v1",
            CurrentSchemaVersion: DatabaseInitializer.CurrentSchemaVersion,
            CheckedAtUtc: DateTime.UtcNow,
            WouldModifyDatabase: pending.Length > 0,
            RequiresBackupBeforeApply: pending.Any(item => item.RequiresBackup),
            HasDestructiveSteps: items.Any(item => item.IsDestructive),
            ApplySupported: false,
            Items: items,
            Blockers: blockers,
            Warnings: warnings);
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


    private static string CreateUniqueBackupFileName(string backupRoot)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var suffix = attempt == 0 ? string.Empty : "-" + attempt.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            var fileName = $"grow-os-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}{suffix}.zip";
            if (!System.IO.File.Exists(Path.Combine(backupRoot, fileName)))
            {
                return fileName;
            }
        }

        return "grow-os-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8] + ".zip";
    }

    private static string RunSqliteQuickCheck(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return command.ExecuteScalar()?.ToString() ?? "quick_check returned no result";
    }

    private static void RestoreFileWithRollback(string sourcePath, string targetPath, string rollbackRoot, string rollbackName)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var rollbackPath = Path.Combine(rollbackRoot, rollbackName);
        if (System.IO.File.Exists(targetPath))
        {
            System.IO.File.Move(targetPath, rollbackPath, overwrite: true);
        }

        System.IO.File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static void RestoreOptionalFileWithRollback(string sourcePath, string targetPath, string rollbackRoot, string rollbackName)
    {
        var rollbackPath = Path.Combine(rollbackRoot, rollbackName);
        if (System.IO.File.Exists(targetPath))
        {
            System.IO.File.Move(targetPath, rollbackPath, overwrite: true);
        }

        if (System.IO.File.Exists(sourcePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            System.IO.File.Copy(sourcePath, targetPath, overwrite: true);
        }
    }

    private static void RestoreDirectoryWithRollback(string sourceDirectory, string targetDirectory, string rollbackRoot, string rollbackName)
    {
        var rollbackDirectory = Path.Combine(rollbackRoot, rollbackName);
        if (Directory.Exists(targetDirectory))
        {
            Directory.Move(targetDirectory, rollbackDirectory);
        }

        CopyDirectory(sourceDirectory, targetDirectory);
    }

    private static void RestoreRollbackFiles(string rollbackRoot)
    {
        if (!Directory.Exists(rollbackRoot))
        {
            return;
        }

        var appDataRoot = Directory.GetParent(rollbackRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            return;
        }

        var databasePath = Path.Combine(appDataRoot, "grow-diary.db");
        var rollbackDb = Path.Combine(rollbackRoot, "grow-diary.db");
        var rollbackWal = Path.Combine(rollbackRoot, "grow-diary.db-wal");
        var rollbackShm = Path.Combine(rollbackRoot, "grow-diary.db-shm");
        var rollbackKnowledge = Path.Combine(rollbackRoot, "knowledge");
        var knowledgePath = Path.Combine(appDataRoot, "knowledge");

        RestoreRollbackFile(rollbackDb, databasePath);
        RestoreRollbackFile(rollbackWal, databasePath + "-wal");
        RestoreRollbackFile(rollbackShm, databasePath + "-shm");

        if (Directory.Exists(rollbackKnowledge))
        {
            DeleteDirectoryBestEffort(knowledgePath);
            Directory.Move(rollbackKnowledge, knowledgePath);
        }
    }

    private static void RestoreRollbackFile(string rollbackPath, string targetPath)
    {
        if (!System.IO.File.Exists(rollbackPath))
        {
            return;
        }

        if (System.IO.File.Exists(targetPath))
        {
            System.IO.File.Delete(targetPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        System.IO.File.Move(rollbackPath, targetPath);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var target = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            System.IO.File.Copy(file, target, overwrite: true);
        }
    }

    private static void DeleteDirectoryBestEffort(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
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
