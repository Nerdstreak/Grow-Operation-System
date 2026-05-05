using System.ComponentModel.DataAnnotations;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class GrowTaskCreateRequest
{
    [Required]
    [MinLength(1)]
    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public string? DueAtLocal { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
}
