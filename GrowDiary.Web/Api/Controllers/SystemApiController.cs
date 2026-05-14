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
        return Created($"/api/system/backup/{Uri.EscapeDataString(fileName)}", new BackupManifestDto(
            BackupSchema: "grow-os.backup.v1",
            CreatedAtUtc: DateTime.UtcNow,
            FileName: fileName,
            SizeBytes: info.Length,
            IncludesDatabase: System.IO.File.Exists(_paths.DatabasePath),
            IncludesWal: System.IO.File.Exists(_paths.DatabasePath + "-wal"),
            IncludesKnowledgeRuntimeCopy: Directory.Exists(Path.Combine(_paths.ContentRootPath, "App_Data", "knowledge")),
            ExcludesSecrets: true));
    }

    private static void AddIfExists(ZipArchive archive, string sourcePath, string entryName)
    {
        if (System.IO.File.Exists(sourcePath))
        {
            archive.CreateEntryFromFile(sourcePath, entryName);
        }
    }
}
