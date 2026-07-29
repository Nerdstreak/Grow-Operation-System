using GrowMcp;

namespace GrowMcp.Tests;

/// <summary>
/// Wer über welchen Port was sehen darf.
/// </summary>
/// <remarks>
/// <para>Grow OS selbst hat keinen offenen Port; der MCP-Server braucht einen,
/// sonst käme kein Klient aus dem Heimnetz heran. Damit dieser Port nicht zum
/// Scheunentor wird, hängen zwei Türen an zwei Ports: die Einrichtungsseite mit
/// dem Schlüssel darauf am Ingress-Port, die Schnittstelle am Netz-Port.</para>
///
/// <para>Der Fehler, den diese Tests verhindern sollen, ist ein einziger: dass die
/// Seite mit dem Schlüssel auch über das WLAN abrufbar wird. Dann könnte sich
/// jeder im Netz den Schlüssel abholen, und das Absichern wäre eine Geste.</para>
/// </remarks>
public sealed class TuerenTests
{
    [Fact]
    public void TheSetupPageIsNotReachableFromTheHomeNetwork()
    {
        // Der eine Test, um den es geht.
        var zutritt = Tueren.Pruefen(Tueren.NetzPort, "/", schluesselStimmt: true);

        Assert.Equal(Zutritt.NichtGefunden, zutritt);
    }

    [Fact]
    public void TheInterfaceIsNotReachableThroughIngress()
    {
        // Andersherum genauso: durch den Ingress kommt man nur an die Seite. Sonst
        // haette jeder, der in Home Assistant angemeldet ist, die Werkzeuge —
        // ohne je einen Schluessel gesehen zu haben.
        var zutritt = Tueren.Pruefen(Tueren.IngressPort, "/mcp", schluesselStimmt: true);

        Assert.Equal(Zutritt.NichtGefunden, zutritt);
    }

    [Fact]
    public void TheInterfaceNeedsTheKey()
    {
        Assert.Equal(Zutritt.SchluesselFehlt, Tueren.Pruefen(Tueren.NetzPort, "/mcp", schluesselStimmt: false));
        Assert.Equal(Zutritt.Erlaubt, Tueren.Pruefen(Tueren.NetzPort, "/mcp", schluesselStimmt: true));
    }

    [Fact]
    public void TheSetupPageNeedsNoKeyBecauseHomeAssistantAlreadyAsked()
    {
        var zutritt = Tueren.Pruefen(Tueren.IngressPort, "/", schluesselStimmt: false);

        Assert.Equal(Zutritt.Erlaubt, zutritt);
    }

    [Theory]
    [InlineData("/mcp")]
    [InlineData("/mcp/")]
    [InlineData("/mcp/nachricht")]
    public void EveryPathUnderTheInterfaceCountsAsTheInterface(string pfad)
    {
        Assert.Equal(Zutritt.SchluesselFehlt, Tueren.Pruefen(Tueren.NetzPort, pfad, schluesselStimmt: false));
    }

    [Fact]
    public void APathThatMerelyStartsWithThoseLettersIsNotTheInterface()
    {
        // „/mcp-einstellungen" faengt mit /mcp an, ist aber ein anderer Weg. Ohne
        // die Pruefung auf den Trenner haetten solche Pfade den Schluessel
        // verlangt — oder, schlimmer, ihn spaeter einmal umgangen.
        Assert.Equal(Zutritt.NichtGefunden, Tueren.Pruefen(Tueren.NetzPort, "/mcpx", schluesselStimmt: false));
        Assert.Equal(Zutritt.Erlaubt, Tueren.Pruefen(Tueren.IngressPort, "/mcpx", schluesselStimmt: false));
    }
}
