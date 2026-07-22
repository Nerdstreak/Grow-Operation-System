using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Models;

public sealed class TentCameraListTests
{
    [Fact]
    public void Parse_SplitsNewlineSeparatedEntities()
    {
        var cameras = TentCameraList.Parse("camera.a\ncamera.b\ncamera.c", null);

        Assert.Equal(new[] { "camera.a", "camera.b", "camera.c" }, cameras);
    }

    [Fact]
    public void Parse_FallsBackToSingleWhenListEmpty()
    {
        Assert.Equal(new[] { "camera.legacy" }, TentCameraList.Parse(null, "camera.legacy"));
        Assert.Equal(new[] { "camera.legacy" }, TentCameraList.Parse("  ", "camera.legacy"));
    }

    [Fact]
    public void Parse_EmptyWhenNothingSet()
    {
        Assert.Empty(TentCameraList.Parse(null, null));
    }

    [Fact]
    public void Serialize_CleansTrimsAndDeduplicates_AndReportsFirst()
    {
        var (ids, first) = TentCameraList.Serialize(new[] { " camera.a ", "", "camera.b", "camera.a" });

        Assert.Equal("camera.a\ncamera.b", ids);
        Assert.Equal("camera.a", first);
    }

    [Fact]
    public void Serialize_EmptyListGivesNulls()
    {
        var (ids, first) = TentCameraList.Serialize(new[] { "", "   " });

        Assert.Null(ids);
        Assert.Null(first);
    }

    [Fact]
    public void RoundTrip_PreservesOrder()
    {
        var (ids, _) = TentCameraList.Serialize(new[] { "camera.plant1", "camera.plant2", "camera.plant3" });

        Assert.Equal(new[] { "camera.plant1", "camera.plant2", "camera.plant3" }, TentCameraList.Parse(ids, null));
    }
}
