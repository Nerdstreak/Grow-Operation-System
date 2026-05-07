using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class AutoMeasurementValueGuardTests
{
    private readonly AutoMeasurementValueGuard _guard = new();

    [Fact]
    public void Check_ReservoirPhZeroIsRejected()
    {
        var result = _guard.Check(AutoMeasurementField.ReservoirPh, 0);

        Assert.False(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Reject, result.Severity);
    }

    [Fact]
    public void Check_ReservoirPhFivePointEightIsValid()
    {
        var result = _guard.Check(AutoMeasurementField.ReservoirPh, 5.8);

        Assert.True(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.None, result.Severity);
    }

    [Fact]
    public void Check_ReservoirEcNinetyNineIsRejected()
    {
        var result = _guard.Check(AutoMeasurementField.ReservoirEc, 99);

        Assert.False(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Reject, result.Severity);
    }

    [Fact]
    public void Check_ReservoirWaterTempEightyIsRejected()
    {
        var result = _guard.Check(AutoMeasurementField.ReservoirWaterTempC, 80);

        Assert.False(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Reject, result.Severity);
    }

    [Fact]
    public void Check_DissolvedOxygenTwoIsWarning()
    {
        var result = _guard.Check(AutoMeasurementField.DissolvedOxygenMgL, 2);

        Assert.True(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Check_PpfdThreeThousandIsRejected()
    {
        var result = _guard.Check(AutoMeasurementField.PpfdMol, 3000);

        Assert.False(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Reject, result.Severity);
    }

    [Fact]
    public void Check_Co2TwentyFiveHundredIsWarning()
    {
        var result = _guard.Check(AutoMeasurementField.Co2Ppm, 2500);

        Assert.True(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Check_HumidityOneHundredOneIsRejected()
    {
        var result = _guard.Check(AutoMeasurementField.HumidityPercent, 101);

        Assert.False(result.IsValid);
        Assert.Equal(AutoMeasurementValueSeverity.Reject, result.Severity);
    }
}
