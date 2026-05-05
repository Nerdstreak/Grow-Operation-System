using GrowDiary.Web.Models;
using Xunit;

namespace GrowDiary.Web.Tests.Models;

public sealed class TentTests
{
    [Fact]
    public void Tent_DefaultTentType_IsMultiPurpose()
    {
        var tent = new Tent();
        Assert.Equal(TentType.MultiPurpose, tent.TentType);
    }

    [Fact]
    public void Tent_SensorList_DefaultsToEmpty()
    {
        var tent = new Tent();
        Assert.Empty(tent.Sensors);
    }

    [Fact]
    public void Tent_Co2Available_DefaultsToFalse()
    {
        var tent = new Tent();
        Assert.False(tent.Co2Available);
    }
}
