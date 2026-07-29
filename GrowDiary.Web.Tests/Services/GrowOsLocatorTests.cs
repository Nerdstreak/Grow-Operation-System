using GrowAgent.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Grow OS im internen Add-on-Netz finden.
/// </summary>
/// <remarks>
/// <para>Der Hostname eines Add-ons setzt sich aus Repository und Slug zusammen —
/// <c>local_grow_os</c> bei einer lokalen Installation, sonst mit dem Hash des
/// Repositories davor. Als DNS-Name werden die Unterstriche zu Bindestrichen.</para>
///
/// <para>Der Hash ist von aussen nicht vorhersagbar, aber er ist ableitbar: der
/// Berater und Grow OS kommen aus demselben Repository und tragen deshalb
/// denselben Vorsatz. Genau das prüfen diese Tests — die Ableitung ist der
/// Grund, warum das Add-on ohne die Manager-Rolle auskommt.</para>
/// </remarks>
public sealed class GrowOsLocatorTests
{
    [Fact]
    public void TheNameOfGrowOsIsDerivedFromTheAgentsOwnName()
    {
        var namen = GrowOsLocator.Kandidaten("a1b2c3d4_grow_agent");

        // Erst der abgeleitete Name — derselbe Store, derselbe Vorsatz.
        Assert.Equal("a1b2c3d4_grow_os", namen[0]);
        Assert.Equal("a1b2c3d4-grow-os", GrowOsLocator.Hostname(namen[0]));
        Assert.Equal("http://a1b2c3d4-grow-os:5076", GrowOsLocator.BaseUrl(GrowOsLocator.Hostname(namen[0])));
    }

    [Fact]
    public void ALocalInstallIsTriedEvenWithoutAnyDerivation()
    {
        // Wer Grow OS aus dem Ordner heraus installiert hat, findet es hier —
        // ohne dass der Supervisor etwas herausrücken muss.
        var namen = GrowOsLocator.Kandidaten(eigenerSlug: null);

        Assert.Contains("local_grow_os", namen);
        Assert.Equal("local-grow-os", GrowOsLocator.Hostname("local_grow_os"));
    }

    [Fact]
    public void TheDerivedNameComesBeforeTheFixedGuesses()
    {
        var namen = GrowOsLocator.Kandidaten("a1b2c3d4_grow_agent");

        // Sonst antwortete bei zwei Installationen die falsche zuerst.
        Assert.Equal(3, namen.Count);
        Assert.Equal(["a1b2c3d4_grow_os", "local_grow_os", "grow_os"], namen);
    }

    [Fact]
    public void ALocallyInstalledAgentDoesNotProduceTheSameNameTwice()
    {
        // "local_grow_agent" leitet auf "local_grow_os" ab — das steht ohnehin
        // schon auf der Liste. Doppelt anklopfen wäre nur Wartezeit.
        var namen = GrowOsLocator.Kandidaten("local_grow_agent");

        Assert.Equal(["local_grow_os", "grow_os"], namen);
    }

    [Fact]
    public void AnUnexpectedOwnNameFallsBackInsteadOfInventingAHost()
    {
        // Sollte sich der eigene Slug einmal ändern, wird nicht geraten.
        var namen = GrowOsLocator.Kandidaten("irgendwas_anderes");

        Assert.Equal(["local_grow_os", "grow_os"], namen);
    }

    [Fact]
    public void TheFailureMessageSaysWhatToDoNext()
    {
        // „Nicht erreichbar" allein lässt den Nutzer stehen; der Satz muss den
        // Ausweg nennen, den es tatsächlich gibt.
        Assert.False(GrowOsLocator.NichtGefunden.Gefunden);
        Assert.Contains("Einstellungen", GrowOsLocator.NichtGefunden.Meldung);
    }
}
