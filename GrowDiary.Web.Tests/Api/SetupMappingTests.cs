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
        var lastCloneCutAt = new DateTime(2026, 2, 3);
        var quarantineStartedAt = new DateTime(2026, 3, 1);
        var quarantinePlannedEndAt = new DateTime(2026, 3, 14);
        var setup = new Setup
        {
            Id = 12,
            TentId = 34,
            Name = "Mother Setup",
            SetupType = SetupType.Mother,
            Status = SetupStatus.Active,
            Notes = "Notes",
            CloneCounterTotal = 7,
            LastCloneCutAt = lastCloneCutAt,
            MotherHealthStatus = "Stable",
            QuarantineStartedAt = quarantineStartedAt,
            QuarantinePlannedEndAt = quarantinePlannedEndAt,
            QuarantineResult = "Pending",
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
        Assert.Equal(7, dto.CloneCounterTotal);
        Assert.Equal(lastCloneCutAt, dto.LastCloneCutAt);
        Assert.Equal("Stable", dto.MotherHealthStatus);
        Assert.Equal(quarantineStartedAt, dto.QuarantineStartedAt);
        Assert.Equal(quarantinePlannedEndAt, dto.QuarantinePlannedEndAt);
        Assert.Equal("Pending", dto.QuarantineResult);
        Assert.Equal(createdAt, dto.CreatedAtUtc);
        Assert.Equal(updatedAt, dto.UpdatedAtUtc);
    }
}
