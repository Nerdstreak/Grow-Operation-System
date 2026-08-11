using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Was als „Licht an" zählt.
/// </summary>
/// <remarks>
/// <para>Aus dem Feld gemeldet: ein Nutzer hatte seinen Lichtsensor korrekt
/// eingetragen (`sensor.…_lichtstarke`, Wert „100.0" in Prozent) — und die
/// Kachel behauptete, es sei kein Sensor gemappt. Der Normalisierer kannte nur
/// Schalterstellungen, kein Helligkeitssensor meldet aber „on".</para>
/// </remarks>
public sealed class LightStateNormalizerTests
{
    [Theory]
    [InlineData("on")]
    [InlineData("ON")]
    [InlineData("true")]
    [InlineData("open")]
    [InlineData("1")]
    public void SwitchesStillReadAsOn(string roh)
        => Assert.Equal(LightState.On, LightStateNormalizer.Normalize(roh));

    [Theory]
    [InlineData("off")]
    [InlineData("false")]
    [InlineData("closed")]
    [InlineData("0")]
    public void SwitchesStillReadAsOff(string roh)
        => Assert.Equal(LightState.Off, LightStateNormalizer.Normalize(roh));

    [Theory]
    [InlineData("100.0")]   // der gemeldete Feldfall: Lichtstaerke in Prozent
    [InlineData("100")]
    [InlineData("42.5")]
    [InlineData("18500")]   // Lux
    [InlineData("1")]
    public void ABrightnessReadingCountsAsLightOn(string roh)
        => Assert.Equal(LightState.On, LightStateNormalizer.Normalize(roh));

    [Theory]
    [InlineData("0.0")]
    [InlineData("0.4")]     // Restlicht, nicht die Lampe
    public void DarknessCountsAsOff(string roh)
        => Assert.Equal(LightState.Off, LightStateNormalizer.Normalize(roh));

    [Theory]
    [InlineData("unavailable")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void WhatCannotBeReadStaysUnknown(string? roh)
        => Assert.Equal(LightState.Unknown, LightStateNormalizer.Normalize(roh));
}
