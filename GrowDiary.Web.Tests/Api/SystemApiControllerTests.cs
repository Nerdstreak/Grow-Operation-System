using System.IO.Compression;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

public sealed class SystemApiControllerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dbPath;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly SystemApiController _controller;

    public SystemApiControllerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "GrowSystemApiTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _dbPath = Path.Combine(_tempRoot, "App_Data", "grow-diary.db");
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", _dbPath);
        _paths = new AppPaths(_tempRoot);
        TestDatabase.Initialize(_paths);
        _repository = new GrowRepository(_paths);
        _controller = new SystemApiController(_paths, _repository);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("GROWDIARY_DB_PATH", null);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    [Fact]
    public void ReleaseReadiness_ReturnsBackendV05CandidateAndRemainingV1Items()
    {
        var result = _controller.ReleaseReadiness();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BackendReleaseReadinessDto>(ok.Value);
        Assert.Equal("backend.v0.5-ready-not-v1.0", dto.Status);
        Assert.Contains(dto.CompletedFoundations, value => value == "zero-tent-startup");
        Assert.Contains(dto.CompletedFoundations, value => value == "grow-export-v1");
        Assert.Contains(dto.RemainingBeforeV1, value => value == "versioned-database-migrations");
        Assert.Contains(dto.Checks, check => check.Key == "restore_api" && check.Status == "todo");
    }

    [Fact]
    public void BackendHealth_ListsCurrentBackendCapabilities()
    {
        var result = _controller.BackendHealth();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BackendHealthDto>(ok.Value);
        Assert.True(dto.ZeroTentStartupSupported);
        Assert.Contains(dto.Capabilities, capability => capability == "grow-requires-hydro-setup");
        Assert.Contains(dto.Capabilities, capability => capability == "local-backup-without-secrets");
    }

    [Fact]
    public void CreateBackup_ExcludesSecretsRuntimeKeysUploadsAndLogs()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "App_Data"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "App_Data", "DataProtectionKeys"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "wwwroot", "uploads"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "App_Data", "knowledge", "custom"));

        File.WriteAllText(Path.Combine(_tempRoot, "App_Data", "ha-config.json"), "secret-token");
        File.WriteAllText(Path.Combine(_tempRoot, "App_Data", "DataProtectionKeys", "key.xml"), "key-material");
        File.WriteAllText(Path.Combine(_tempRoot, "wwwroot", "uploads", "plant.jpg"), "image");
        File.WriteAllText(Path.Combine(_tempRoot, "runtime.log"), "log");
        File.WriteAllText(Path.Combine(_tempRoot, "App_Data", "knowledge", "custom", "note.json"), "{}");

        var result = _controller.CreateBackup();

        var created = Assert.IsType<CreatedResult>(result.Result);
        var manifest = Assert.IsType<BackupManifestDto>(created.Value);
        Assert.True(manifest.ExcludesSecrets);
        Assert.True(manifest.ExcludesHomeAssistantConfig);
        Assert.True(manifest.ExcludesDataProtectionKeys);
        Assert.True(manifest.ExcludesUploads);
        Assert.False(manifest.RestoreSupported);
        Assert.StartsWith("/api/system/backup/", manifest.DownloadUrl);

        var backupPath = Path.Combine(_tempRoot, "App_Data", "backups", manifest.FileName);
        using var archive = ZipFile.OpenRead(backupPath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToList();

        Assert.Contains("App_Data/grow-diary.db", entries);
        Assert.Contains("App_Data/knowledge/custom/note.json", entries);
        Assert.DoesNotContain("App_Data/ha-config.json", entries);
        Assert.DoesNotContain("App_Data/DataProtectionKeys/key.xml", entries);
        Assert.DoesNotContain("wwwroot/uploads/plant.jpg", entries);
        Assert.DoesNotContain("runtime.log", entries);
    }

    [Fact]
    public void DownloadBackup_RejectsUnsafeFileNames()
    {
        var result = _controller.DownloadBackup("../ha-config.json");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiError>(badRequest.Value);
        Assert.Equal("invalid_backup_file", error.Code);
    }

    [Fact]
    public void DownloadBackup_ReturnsPhysicalFileForExistingBackup()
    {
        var created = Assert.IsType<BackupManifestDto>(Assert.IsType<CreatedResult>(_controller.CreateBackup().Result).Value);

        var result = _controller.DownloadBackup(created.FileName);

        var file = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal(created.FileName, file.FileDownloadName);
        Assert.True(File.Exists(file.FileName));
    }
}
