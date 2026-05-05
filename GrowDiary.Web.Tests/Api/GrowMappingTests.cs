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
}
