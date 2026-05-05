using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Contracts;

public sealed class TaskStatusUpdateRequest
{
    public GrowTaskStatus Status { get; set; } = GrowTaskStatus.Open;
}
