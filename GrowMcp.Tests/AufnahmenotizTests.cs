using System.Text.Json;
using GrowMcp.Tools;

namespace GrowMcp.Tests;

/// <summary>
/// Der Satz, der neben dem Bild steht.
/// </summary>
/// <remarks>
/// <para>Ein Blatt ohne Zusammenhang ist nur ein Blatt. Erst „Wurzel, vor drei
/// Tagen, Grow 4" macht daraus etwas, wozu ein Modell etwas sagen kann —
/// braune Wurzeln nach einem Wasserwechsel bedeuten etwas anderes als braune
/// Wurzeln in Woche sieben.</para>
///
/// <para>Deshalb ist diese Notiz kein Beiwerk, sondern der halbe Nutzen des
/// Werkzeugs. Sie darf nur behaupten, was wirklich am Foto steht: was Grow OS
/// nicht weiß, bleibt weg.</para>
/// </remarks>
public sealed class AufnahmenotizTests
{
    private static JsonElement Foto(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void EverythingKnownAboutTheShotIsNamed()
    {
        var gestern = DateTime.UtcNow.AddDays(-1).ToString("O");
        var notiz = GrowTools.Aufnahmenotiz(Foto($$"""
            {"id":5,"tag":"Root","takenAtUtc":"{{gestern}}","caption":"Wurzeln wirken braun","measurementId":12}
            """), growId: 4);

        Assert.Contains("Grow 4", notiz);
        Assert.Contains("Motiv: Root", notiz);
        Assert.Contains("gestern aufgenommen", notiz);
        Assert.Contains("Wurzeln wirken braun", notiz);
        // Der Verweis auf die Messung ist der Faden zu den Zahlen: ein Modell,
        // das braune Wurzeln sieht, soll den EC daneben nachschlagen koennen.
        Assert.Contains("Messung 12", notiz);
    }

    [Fact]
    public void WhatIsNotRecordedIsNotClaimed()
    {
        var notiz = GrowTools.Aufnahmenotiz(Foto("""{"id":7}"""), growId: 2);

        Assert.Equal("Grow 2", notiz);
        Assert.DoesNotContain("Motiv", notiz);
        Assert.DoesNotContain("aufgenommen", notiz);
        Assert.DoesNotContain("Messung", notiz);
    }

    [Fact]
    public void AgeIsCountedInDaysNotInTimestamps()
    {
        // „vor 12 Tagen" beantwortet die Frage, die man beim Bild hat.
        // Ein ISO-Zeitstempel muesste erst umgerechnet werden.
        var vorZwoelfTagen = DateTime.UtcNow.AddDays(-12).ToString("O");
        var notiz = GrowTools.Aufnahmenotiz(Foto($$"""
            {"id":9,"takenAtUtc":"{{vorZwoelfTagen}}"}
            """), growId: 1);

        Assert.Contains("vor 12 Tagen aufgenommen", notiz);
    }

    [Fact]
    public void APhotoTakenTodaySaysToday()
    {
        var notiz = GrowTools.Aufnahmenotiz(Foto($$"""
            {"id":9,"takenAtUtc":"{{DateTime.UtcNow.ToString("O")}}"}
            """), growId: 1);

        Assert.Contains("heute aufgenommen", notiz);
    }
}
