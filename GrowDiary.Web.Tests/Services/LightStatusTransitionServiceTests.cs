using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

public sealed class LightStatusTransitionServiceTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly AppPaths _paths;
    private readonly GrowRepository _repository;

    public LightStatusTransitionServiceTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), $"grow-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        _paths = new AppPaths(_contentRoot);
        GrowDiary.Web.Tests.TestDatabase.InitializeWithDefaultTent(_paths);
        _repository = new GrowRepository(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_contentRoot, recursive: true); } catch { }
    }

    [Theory]
    [InlineData("on", LightState.On)]
    [InlineData(" true ", LightState.On)]
    [InlineData("1", LightState.On)]
    [InlineData("open", LightState.On)]
    [InlineData("off", LightState.Off)]
    [InlineData("FALSE", LightState.Off)]
    [InlineData("0", LightState.Off)]
    [InlineData("closed", LightState.Off)]
    [InlineData("unavailable", LightState.Unknown)]
    public void LightStateNormalizer_RecognizesHomeAssistantStates(string raw, LightState expected)
    {
        Assert.Equal(expected, LightStateNormalizer.Normalize(raw));
    }

    [Fact]
    public void ProcessLightStatus_CreatesTransitionForNonNumericHomeAssistantState()
    {
        var service = new LightStatusTransitionService(_repository);
        var tent = _repository.GetTents().Single();
        var firstPoll = new DateTime(2026, 5, 7, 7, 55, 0, DateTimeKind.Utc);
        var secondPoll = firstPoll.AddMinutes(5);

        Assert.Null(service.Process(tent.Id, new HomeAssistantState { State = "off" }, firstPoll));

        var transition = service.Process(tent.Id, new HomeAssistantState { State = "on" }, secondPoll);

        Assert.NotNull(transition);
        Assert.Equal(LightTransitionKind.LightOn, transition!.Kind);
        Assert.Equal("on", transition.RawState);
        Assert.Single(_repository.GetLightTransitionsByTent(tent.Id));
    }
}
