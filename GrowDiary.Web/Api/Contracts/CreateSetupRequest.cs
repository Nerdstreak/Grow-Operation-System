using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class CreateSetupRequest
{
    public int TentId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public SetupType SetupType { get; set; } = SetupType.Production;
    public string? Notes { get; set; }
}
