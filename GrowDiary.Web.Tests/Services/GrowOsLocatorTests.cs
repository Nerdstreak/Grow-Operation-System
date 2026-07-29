using GrowOsAccess;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Grow OS im internen Add-on-Netz finden.
/// </summary>
/// <remarks>
/// <para>Der Hostname eines Add-ons setzt sich aus Repository und Slug zusammen —
/// <c>local_grow_os</c> bei einer lokalen Installation, sonst mit dem Hash des
/// Repositories davor. Als DNS-Name werden die Unterstriche zu Bindestrichen.</para>
///
/// <para>Der Hash ist von aussen nicht vorhersagbar, aber er ist ableitbar: alle
/// Add-ons aus diesem Repository tragen denselben Vorsatz. Ein Add-on fragt den
/// Supervisor nach seinem eigenen Namen und tauscht den hinteren Teil aus. Genau
/// das erspart die Manager-Rolle.</para>
///
/// <para>Die Tests fahren beide Add-ons durch, nicht nur eines. Als der eigene
/// Slug hier noch fest <c>grow_agent</c> war, sah alles gruen aus — und das
/// MCP-Add-on fand ein Grow OS aus dem Store trotzdem nicht.</para>
/// </remarks>
public sealed class GrowOsLocatorTests
{
    /// <summary>Die Slugs, unter denen Add-ons dieses Repositories laufen.</summary>
    public static TheoryData<string> Addons => ["grow_agent", "grow_mcp"];

    [Theory]
    [MemberData(nameof(Addons))]
    public void EveryAddonDerivesGrowOsFromItsOwnName(string eigener)
    {
        var namen = GrowOsLocator.Kandidaten($"a1b2c3d4_{eigener}", eigener);

        // Erst der abgeleitete Name — dasselbe Repository, derselbe Vorsatz.
        Assert.Equal("a1b2c3d4_grow_os", namen[0]);
        Assert.Equal("a1b2c3d4-grow-os", GrowOsLocator.Hostname(namen[0]));
        Assert.Equal("http://a1b2c3d4-grow-os:5076", GrowOsLocator.BaseUrl(GrowOsLocator.Hostname(namen[0])));
    }

    [Fact]
    public void TheNameOfAnotherAddonDoesNotDeriveAnything()
    {
        // Der Fehler, der in freier Wildbahn zugeschlagen hat: das MCP-Add-on
        // heisst „…_grow_mcp", suchte aber nach der Endung „grow_agent". Ohne
        // Treffer bleiben nur die Namen ohne Hash — und die gibt es bei einer
        // Installation aus dem Store nicht.
        var namen = GrowOsLocator.Kandidaten("a1b2c3d4_grow_mcp", eigenerBasisSlug: "grow_agent");

        Assert.DoesNotContain("a1b2c3d4_grow_os", namen);
        Assert.Equal(["local_grow_os", "grow_os"], namen);
    }

    [Fact]
    public void ALocalInstallIsTriedEvenWithoutAnyDerivation()
    {
        // Wer Grow OS aus dem Ordner heraus installiert hat, findet es hier —
        // ohne dass der Supervisor etwas herausrücken muss.
        var namen = GrowOsLocator.Kandidaten(vollerSlug: null, eigenerBasisSlug: "grow_mcp");

        Assert.Contains("local_grow_os", namen);
        Assert.Equal("local-grow-os", GrowOsLocator.Hostname("local_grow_os"));
    }

    [Theory]
    [MemberData(nameof(Addons))]
    public void TheDerivedNameComesBeforeTheFixedGuesses(string eigener)
    {
        var namen = GrowOsLocator.Kandidaten($"a1b2c3d4_{eigener}", eigener);

        // Sonst antwortete bei zwei Installationen die falsche zuerst.
        Assert.Equal(["a1b2c3d4_grow_os", "local_grow_os", "grow_os"], namen);
    }

    [Theory]
    [MemberData(nameof(Addons))]
    public void ALocallyInstalledAddonDoesNotProduceTheSameNameTwice(string eigener)
    {
        // „local_grow_mcp" leitet auf „local_grow_os" ab — das steht ohnehin
        // schon auf der Liste. Doppelt anklopfen wäre nur Wartezeit.
        var namen = GrowOsLocator.Kandidaten($"local_{eigener}", eigener);

        Assert.Equal(["local_grow_os", "grow_os"], namen);
    }

    [Fact]
    public void AnUnexpectedOwnNameFallsBackInsteadOfInventingAHost()
    {
        var namen = GrowOsLocator.Kandidaten("irgendwas_anderes", "grow_agent");

        Assert.Equal(["local_grow_os", "grow_os"], namen);
    }

    [Fact]
    public void TheFailureMessageSaysWhatToDoNextWithoutNamingTheWrongAddon()
    {
        // Diese Meldung erscheint in JEDEM Add-on, das die Bibliothek nutzt. Sie
        // stand auf der MCP-Seite und schickte den Nutzer in die Einstellungen
        // des Beraters — dort gibt es das Feld zwar auch, es half nur nichts.
        var meldung = GrowOsLocator.NichtGefunden.Meldung;

        Assert.False(GrowOsLocator.NichtGefunden.Gefunden);
        Assert.Contains("dieses Add-ons", meldung);
        Assert.DoesNotContain("Berater", meldung);
        // Ohne beta.24 antwortet Grow OS aus dem internen Netz mit 403, und das
        // sieht von hier aus genauso aus wie „nicht da".
        Assert.Contains("beta.24", meldung);
    }
}
