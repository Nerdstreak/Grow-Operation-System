using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Jedes <c>Url.Action("…", "…")</c> zeigt auf eine Aktion, die es gibt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Beim Aufräumen der Legacy-Kamera-Wege
/// wurde <c>TentsController.CameraSnapshot</c> gelöscht — und in derselben
/// Datei blieb <c>Url.Action("CameraSnapshot", "Tents", …)</c> stehen.</para>
///
/// <para><b>Was der Nutzer merkt.</b> <c>Url.Action</c> wirft nicht, es gibt
/// <c>null</c> zurück. Das Feld <c>cameraUrl</c> war damit dauerhaft leer, und
/// auf der Zelt-Seite stand für immer „Kamera — Nicht eingerichtet", auch wenn
/// die Kamera in Home Assistant sauber eingetragen war
/// (<c>TentDetailPage.tsx</c>). Lokal fiel das nicht auf, weil ohne Home
/// Assistant die Bedingung davor kurzschliesst — <b>der Schaden trifft nur die
/// echte Anlage</b>.</para>
///
/// <para><b>Warum eine Zählung.</b> Der Compiler sieht Zeichenketten nicht. Ein
/// Aktionsname in Anführungszeichen überlebt jede Umbenennung und jede
/// Löschung, ohne dass irgendwo etwas rot wird.</para>
/// </remarks>
public sealed class JedeUrlActionZeigtAufEineEchteAktionTests
{
    /// <summary>Der Aufruf, so wie er im Quelltext steht.</summary>
    private static readonly Regex URL_ACTION = new(
        @"Url\.Action\(\s*""(?<aktion>[^""]+)""\s*,\s*""(?<controller>[^""]+)""",
        RegexOptions.Compiled);

    [Fact]
    public void JedeUrlActionZeigtAufEineEchteAktion()
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.Web");
        var aktionen = AlleAktionen();

        // Mengenwaechter: ohne Aktionen waere jeder Vergleich darunter wertlos.
        Assert.True(aktionen.Count >= 50,
            $"Nur {aktionen.Count} Aktionen gefunden — die Zaehlung sieht ihre Grundmenge nicht.");

        var tote = new List<string>();
        var gesehen = 0;

        foreach (var datei in Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories))
        {
            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
                var ohneKommentar = zeilen[i].Split("//")[0];
                var treffer = URL_ACTION.Match(ohneKommentar);
                if (!treffer.Success) continue;

                gesehen += 1;
                var schluessel = $"{treffer.Groups["controller"].Value}.{treffer.Groups["aktion"].Value}";
                if (aktionen.Contains(schluessel)) continue;

                tote.Add($"{Path.GetFileName(datei)}:{i + 1}  {schluessel}");
            }
        }

        Assert.True(tote.Count == 0,
            "Diese Verweise zeigen auf eine Aktion, die es nicht gibt:\n  "
            + string.Join("\n  ", tote)
            + "\n\nUrl.Action wirft nicht — es gibt null zurueck. Das Feld bleibt leer, und auf "
            + "dem Schirm steht dauerhaft „nicht eingerichtet\", ohne dass irgendwo etwas rot "
            + "wird. Bekannt sind: " + string.Join(", ", aktionen.Order().Take(12)) + " …");
    }

    /// <summary>Der Selbsttest: trifft das Muster die belegte Form?</summary>
    /// <remarks>
    /// Die erste Zeile ist wörtlich die, die den Fehler getragen hat. Eine
    /// Zählung mit kaputtem Muster läuft null Mal durch und ist grün.
    /// </remarks>
    [Theory]
    [InlineData("? Url.Action(\"CameraSnapshot\", \"Tents\", new { id = tent.Id })", "Tents.CameraSnapshot")]
    [InlineData("var u = Url.Action( \"Detail\" , \"Grows\" );", "Grows.Detail")]
    [InlineData("var u = Url.Action(nameof(Detail), \"Grows\");", null)]
    [InlineData("// Url.Action(\"Alt\", \"Tents\")", "Tents.Alt")]
    public void DasMusterTrifftDieBelegteForm(string zeile, string? erwartet)
    {
        var treffer = URL_ACTION.Match(zeile);

        if (erwartet is null)
        {
            Assert.False(treffer.Success, $"<{zeile}> sollte nicht treffen.");
            return;
        }

        Assert.True(treffer.Success, $"<{zeile}> wurde nicht getroffen.");
        Assert.Equal(erwartet,
            $"{treffer.Groups["controller"].Value}.{treffer.Groups["aktion"].Value}");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>Alle Aktionen als „Controller.Methode".</summary>
    private static HashSet<string> AlleAktionen()
    {
        var raus = new HashSet<string>(StringComparer.Ordinal);
        var assembly = typeof(GrowDiary.Web.Services.Bauzeit).Assembly;

        foreach (var typ in assembly.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            // "TentsController" -> "Tents", so wie Url.Action ihn nennt.
            var kurz = typ.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? typ.Name[..^"Controller".Length]
                : typ.Name;

            foreach (var methode in typ.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (methode.IsSpecialName) continue;
                if (methode.GetCustomAttribute<NonActionAttribute>() is not null) continue;

                raus.Add($"{kurz}.{methode.Name}");

                // Und der Name aus [ActionName("…")], falls einer gesetzt ist.
                if (methode.GetCustomAttribute<ActionNameAttribute>()?.Name is { } eigener)
                {
                    raus.Add($"{kurz}.{eigener}");
                }
            }
        }

        return raus;
    }

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
