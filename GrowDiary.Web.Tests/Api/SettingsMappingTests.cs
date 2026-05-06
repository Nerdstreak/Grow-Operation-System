using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Api;

public sealed class SettingsMappingTests
{
    [Fact]
    public void TentToDto_IncludesSetupCounts()
    {
        var tent = new Tent
        {
            Id = 12,
            Name = "Mother Tent",
            Kind = "Rack",
            ActiveGrowCount = 1,
            ArchivedGrowCount = 2,
            ActiveSetupCount = 3,
            ArchivedSetupCount = 4
        };

        var dto = tent.ToDto();

        Assert.Equal(1, dto.ActiveGrowCount);
        Assert.Equal(2, dto.ArchivedGrowCount);
        Assert.Equal(3, dto.ActiveSetupCount);
        Assert.Equal(4, dto.ArchivedSetupCount);
    }
}
