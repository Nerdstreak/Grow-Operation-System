using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Der AC-Test schaltet an der <b>echten</b> Anlage — und prüft vorher.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Ein Zeitplan mit gleicher Ein- und
/// Aus-Zeit wurde angenommen, und derselbe Aufruf zwang den Modus danach auf
/// „Schedule". Was danach im Zelt passiert, hängt vom Controller ab: eine
/// Abluft oder ein LED-Treiber läuft ab da nach einem Plan, der keine Dauer
/// hat.</para>
///
/// <para>Das ist der einzige Bereich dieser App, in dem ein Fehler
/// <b>Pflanzen</b> kostet — hier wird nicht in eine Datenbank geschrieben,
/// sondern an eine Steckdose in einem echten Zelt.</para>
/// </remarks>
public sealed class AcTestSchuetztDieEchteAnlageTests
{
    /// <summary>Ein- und Aus-Zeit dürfen nicht gleich sein.</summary>
    /// <remarks>
    /// „Durchgehend an" ist in der Vegetation ein ganz normaler Wunsch (24/0) —
    /// nur schreibt man ihn nicht als 20:00 bis 20:00. Der Nutzer bekommt jetzt
    /// gesagt, was er stattdessen tun soll.
    /// </remarks>
    [Theory]
    [InlineData("20:00", "20:00")]
    [InlineData("00:00", "00:00")]
    [InlineData("06:30", "06:30")]
    public void GleicheEinUndAusZeit_WirdAbgelehnt(string ein, string aus)
    {
        Assert.False(AcTest.ZeitplanErlaubt(ein, aus),
            $"Ein-Zeit {ein} und Aus-Zeit {aus} wurden angenommen. Danach zwingt derselbe "
            + "Aufruf den Modus auf „Schedule\" — und das Geraet faehrt einen Plan ohne Dauer.");
    }

    /// <summary>Ein gewöhnlicher Plan geht durch — auch über Mitternacht.</summary>
    /// <remarks>
    /// Die Gegenrichtung. 12/12 in der Blüte heisst oft 20:00 bis 08:00, also
    /// über Mitternacht; wer das ablehnt, macht die Seite unbrauchbar.
    /// </remarks>
    [Theory]
    [InlineData("08:00", "20:00")]
    [InlineData("20:00", "08:00")]
    [InlineData("23:59", "00:01")]
    public void EinGewoehnlicherPlan_GehtDurch(string ein, string aus)
    {
        Assert.True(AcTest.ZeitplanErlaubt(ein, aus),
            $"{ein}–{aus} wurde abgelehnt — 12/12 laeuft ueber Mitternacht, das ist der Normalfall.");
    }

    /// <summary>Und eine unlesbare Zeit bleibt unlesbar.</summary>
    [Theory]
    [InlineData("25:00", "08:00")]
    [InlineData("08:00", "abc")]
    [InlineData("", "08:00")]
    public void EineUnlesbareZeit_WirdAbgelehnt(string ein, string aus)
    {
        Assert.False(AcTest.ZeitplanErlaubt(ein, aus),
            $"„{ein}\" / „{aus}\" wurde angenommen.");
    }
}
