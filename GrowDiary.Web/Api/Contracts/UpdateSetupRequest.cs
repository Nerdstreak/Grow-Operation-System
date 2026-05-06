using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class UpdateSetupRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public SetupStatus Status { get; set; } = SetupStatus.Planning;
    public string? Notes { get; set; }
}
