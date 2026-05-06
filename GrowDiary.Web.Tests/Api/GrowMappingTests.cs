using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Api;

public sealed class GrowMappingTests
{
    [Fact]
    public void ToDetailDto_IncludesSystemId()
    {
        var grow = new GrowRun
        {
            Id = 12,
            SystemId = 34,
            Name = "System Grow"
        };

        var dto = grow.ToDetailDto();

        Assert.Equal(34, dto.SystemId);
    }

    [Fact]
    public void ToDetailDto_IncludesSetupId()
    {
        var grow = new GrowRun
        {
            Id = 12,
            SetupId = 56,
            Name = "Setup Grow"
        };

        var dto = grow.ToDetailDto();

        Assert.Equal(56, dto.SetupId);
    }

    [Fact]
    public void ToSummaryDto_IncludesSetupId()
    {
        var grow = new GrowRun
        {
            Id = 12,
            SetupId = 56,
            Name = "Setup Grow"
        };

        var dto = grow.ToSummaryDto();

        Assert.Equal(56, dto.SetupId);
    }
}
