using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Api;

public sealed class SetupMappingTests
{
    [Fact]
    public void ToDto_IncludesExpectedFields()
    {
        var createdAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var setup = new Setup
        {
            Id = 12,
            TentId = 34,
            Name = "Mother Setup",
            SetupType = SetupType.Mother,
            Status = SetupStatus.Active,
            Notes = "Notes",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = updatedAt
        };

        var dto = setup.ToDto();

        Assert.Equal(12, dto.Id);
        Assert.Equal(34, dto.TentId);
        Assert.Equal("Mother Setup", dto.Name);
        Assert.Equal(SetupType.Mother, dto.SetupType);
        Assert.Equal(SetupStatus.Active, dto.Status);
        Assert.Equal("Notes", dto.Notes);
        Assert.Equal(createdAt, dto.CreatedAtUtc);
        Assert.Equal(updatedAt, dto.UpdatedAtUtc);
    }
}
