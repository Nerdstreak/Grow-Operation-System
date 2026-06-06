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

public sealed partial class GrowExportsApiController
{
    private void LogExportAudit(string action, string summary, bool success, int? relatedGrowId = null, string? relatedFileName = null, string severity = "info")
    {
        try
        {
            _auditRepository.Add(new SystemAuditEvent
            {
                EventType = "export",
                Action = action,
                Summary = summary,
                Severity = severity,
                Source = "grow-export-api",
                RelatedGrowId = relatedGrowId,
                RelatedFileName = relatedFileName,
                Success = success
            });
        }
        catch
        {
            // Audit logging must never break export/import-plan endpoints.
        }
    }

}
