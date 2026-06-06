using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

[ApiController]
[Route("api/exports/grows")]
[Produces("application/json")]
public sealed partial class GrowExportsApiController : ApiControllerBase
{
    private const string ExportSchemaVersion = "grow-os.grow-export.v1";
    private const string ExportValidationSchemaVersion = "grow-os.grow-export.validation.v1";
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;
    private readonly JournalRepository _journalRepository;
    private readonly TaskRepository _taskRepository;
    private readonly HarvestRepository _harvestRepository;
    private readonly SystemAuditRepository _auditRepository;

    public GrowExportsApiController(
        AppPaths paths,
        GrowRepository repository,
        JournalRepository journalRepository,
        TaskRepository taskRepository,
        HarvestRepository harvestRepository,
        SystemAuditRepository auditRepository)
    {
        _paths = paths;
        _repository = repository;
        _journalRepository = journalRepository;
        _taskRepository = taskRepository;
        _harvestRepository = harvestRepository;
        _auditRepository = auditRepository;
    }

}
