using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Die Einkaufsliste aus den Materiallisten der Abläufe.
/// </summary>
/// <remarks>
/// Die Materiallisten lagen seit jeher in den Abläufen und wurden nur beim
/// Drucken der Mappe gelesen. Wer im Laden steht, hatte davon nichts. Diese
/// Tests halten fest, was die Zusammenführung leisten muss: kein Posten
/// doppelt, und zu jedem Posten der Grund, warum er auf der Liste steht.
/// </remarks>
public sealed class EinkaufslisteTests
{
    private static IReadOnlyList<Einkaufsgruppe> Bauen(params (string, IEnumerable<string>)[] quellen)
        => EinkaufslisteService.Bauen(quellen);

    private static Einkaufsposten Finden(IReadOnlyList<Einkaufsgruppe> liste, string beginntMit)
        => liste.SelectMany(g => g.Posten).Single(p => p.Material.StartsWith(beginntMit, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void OneEntryPerThingWithEveryProcedureThatNeedsIt()
    {
        var liste = Bauen(
            ("Tägliche Messroutine", ["pH-Messgerät (kalibriert)", "EC-Messgerät"]),
            ("Wöchentlicher Wasserwechsel", ["pH-Messgerät", "Frisches RO-Wasser"]));

        // „pH-Messgeraet (kalibriert)" und „pH-Messgeraet" sind dasselbe Geraet.
        // Zweimal gelistet wuerde man zwei kaufen.
        var ph = Finden(liste, "pH-Messgerät");
        Assert.Equal(2, ph.Wofuer.Count);
        Assert.Contains("Tägliche Messroutine", ph.Wofuer);
        Assert.Contains("Wöchentlicher Wasserwechsel", ph.Wofuer);
    }

    [Fact]
    public void WhatSeveralProceduresNeedComesFirst()
    {
        var liste = Bauen(
            ("A", ["Eimer", "pH-Messgerät"]),
            ("B", ["pH-Messgerät"]),
            ("C", ["pH-Messgerät"]));

        // Wer einkaufen geht, will oben sehen, was er sicher braucht.
        var messen = liste.Single(g => g.Titel == "Messen");
        Assert.Equal("pH-Messgerät", messen.Posten[0].Material);
        Assert.Equal(3, messen.Posten[0].Wofuer.Count);
    }

    [Fact]
    public void TheGroupsFollowTheWayOneShops()
    {
        var liste = Bauen(("A", [
            "pH-Messgerät", "HOCl-Lösung 750 ppm", "Frisches RO-Wasser", "Sterile Schere",
        ]));

        Assert.Equal(["Messen", "Chemie & Dünger", "Verbrauch", "Werkzeug & Behälter"],
            liste.Select(g => g.Titel).ToArray());
    }

    [Fact]
    public void OneEntryNamingSeveralThingsBecomesSeveralLines()
    {
        // So steht es in der taeglichen Messroutine: drei Geraete in einem Feld.
        // Auf einem Einkaufszettel sind das drei Zeilen.
        var liste = Bauen(("Routine", ["pH-Messgerät, EC-Messgerät, ORP-Messgerät"]));

        var messen = liste.Single(g => g.Titel == "Messen");
        Assert.Equal(3, messen.Posten.Count);
    }

    [Fact]
    public void ADecimalCommaIsNotASeparator()
    {
        // „Addback-Behaelter 18,9 L" ist EIN Behaelter — die Trennung am
        // Dezimalkomma machte daraus „Addback-Behaelter 18" und „9 L".
        var liste = Bauen(("Addback", ["Addback-Behälter 18,9 L (mehrere)"]));

        var posten = liste.SelectMany(g => g.Posten).Single();
        Assert.Contains("18,9", posten.Material);
    }

    [Fact]
    public void ACommaInsideBracketsStaysWithItsItem()
    {
        // „(z. B. Athena Silica, Rhino Skin)" ist ein Beispiel, kein zweiter Posten.
        var liste = Bauen(("Mischen", ["Kaliumsilikat (z. B. Athena Silica, Rhino Skin)"]));

        Assert.Single(liste.SelectMany(g => g.Posten));
    }

    [Fact]
    public void EmptyAndBlankEntriesDoNotBecomeLines()
    {
        var liste = Bauen(("A", ["  ", "", "Eimer"]));

        Assert.Single(liste.SelectMany(g => g.Posten));
        Assert.Equal("Eimer", liste[0].Posten[0].Material);
    }

    [Fact]
    public void WithoutProceduresTheListIsEmptyRatherThanInvented()
    {
        Assert.Empty(EinkaufslisteService.Bauen([]));
    }
}
