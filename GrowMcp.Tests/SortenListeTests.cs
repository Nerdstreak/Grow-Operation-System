using System.Text.Json;
using GrowMcp.Tools;

namespace GrowMcp.Tests;

/// <summary>
/// Das Werkzeug „sorte" liest die Sorten eines Grows als Liste.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Der Tester hat definiert, was ein Grow
/// ist: „ein Durchgang in einem RDWC/DWC, der N Pflanzen mit N verschiedenen
/// Sorten/Phenos beinhalten kann." Das Werkzeug gab bis dahin GENAU EINE
/// zurück — die Hauptsorte —, und die KI beriet damit über ein Becken, dessen
/// halber Inhalt ihr unbekannt war.</para>
///
/// <para><b>Warum das gefährlicher ist als eine Fehlermeldung.</b> Eine
/// Halbwahrheit sieht aus wie eine Antwort. Liest <c>Texte</c> das Feld falsch,
/// fällt das Werkzeug <i>still</i> auf das alte Verhalten zurück — nichts
/// bricht, niemand merkt es, und die Auskunft ist wieder falsch. Deshalb eine
/// eigene Prüfung für den Leser.</para>
/// </remarks>
public sealed class SortenListeTests
{
    private static JsonElement Aus(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ZweiSortenKommenBeideAn()
    {
        var grow = Aus("""{"pflanzenSorten":["White Widow","Gorilla Glue"]}""");

        Assert.Equal(["White Widow", "Gorilla Glue"], GrowTools.Texte(grow, "pflanzenSorten"));
    }

    [Fact]
    public void FehltDasFeld_KommtEineLeereListe()
    {
        // Ein älterer Server ohne das Feld darf das Werkzeug nicht umwerfen —
        // dann gilt eben wieder die Hauptsorte.
        Assert.Empty(GrowTools.Texte(Aus("""{"strain":"White Widow"}"""), "pflanzenSorten"));
    }

    [Fact]
    public void IstDasFeldKeineListe_KommtEineLeereListe()
    {
        Assert.Empty(GrowTools.Texte(Aus("""{"pflanzenSorten":"White Widow"}"""), "pflanzenSorten"));
        Assert.Empty(GrowTools.Texte(Aus("""{"pflanzenSorten":null}"""), "pflanzenSorten"));
    }

    [Fact]
    public void LeereUndFalscheEintraegeFallenRaus()
    {
        // Ein leerer Name wäre in der Aufzählung ein Komma ohne Wort dahinter.
        var grow = Aus("""{"pflanzenSorten":["White Widow","","  ",7,null,"Gorilla Glue"]}""");

        Assert.Equal(["White Widow", "Gorilla Glue"], GrowTools.Texte(grow, "pflanzenSorten"));
    }
}
