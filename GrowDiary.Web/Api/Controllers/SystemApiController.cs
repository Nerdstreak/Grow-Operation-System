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
public sealed partial class SystemApiController : ApiControllerBase
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

}
