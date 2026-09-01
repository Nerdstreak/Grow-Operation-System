using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Die Kette zum Zielband wird nicht abgetippt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026, vom Prüfer gefunden).</b> Die
/// Zusammenlegung in <see cref="Zielband"/> war am selben Tag als „eine Kette"
/// gemeldet worden — sie erreichte aber nur zwei von fünf Stellen. Profil →
/// Phase → Feedchart stand danach immer noch eigenständig im
/// <c>GrowDashboardComposer</c>, im <c>DosingContextBuilder</c> und im
/// <c>AgentContextBuilder</c>.</para>
///
/// <para><b>Was das kostet.</b> Genau der Fehler, gegen den
/// <see cref="Zielband"/> gebaut wurde: bei Athena Blended in Blütewoche 4
/// nennt das Chart EC 2,6, das Profil <c>rdwc-default</c> für Flower 1,0–1,2.
/// Wer eine der drei Kopien beim nächsten Mal vergisst, bekommt dieselbe
/// Doppelauskunft zurück — die Dosierpumpe fährt dann gegen ein anderes Ziel
/// als der Bildschirm zeigt.</para>
///
/// <para><b>Woran man die Kette erkennt.</b> An ihrem letzten Schritt:
/// <c>MischplanService.MitFeedchart</c>. Wer den aufruft, baut die Kette
/// selbst.</para>
/// </remarks>
public sealed class EineZielbandketteTests
{
    /// <summary>Nur <see cref="Zielband"/> legt Feedchart-Ziele auf.</summary>
    [Fact]
    public void NiemandLegtFeedchartZieleVonHandAuf()
    {
        var verzeichnis = QuellVerzeichnis();
        var dateien = Directory.EnumerateFiles(verzeichnis, "*.cs", SearchOption.AllDirectories).ToList();

        // Mengenwaechter: ohne Grundmenge laeuft die Schleife null Mal.
        Assert.True(dateien.Count >= 100,
            $"Nur {dateien.Count} Quelldateien gefunden — die Grundmenge stimmt nicht, "
            + "und diese Zaehlung prueft dann nichts.");

        var treffer = new List<string>();
        foreach (var datei in dateien)
        {
            // Zielband.cs IST die eine Wahrheit.
            if (string.Equals(Path.GetFileName(datei), "Zielband.cs", StringComparison.Ordinal)) continue;

            // MischplanService selbst bietet die Methode an — das ist ihr Zuhause.
            if (string.Equals(Path.GetFileName(datei), "MischplanService.cs", StringComparison.Ordinal)) continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i += 1)
            {
                var zeile = zeilen[i].Trim();

                // Eine Erwaehnung ist keine Verwendung.
                if (zeile.StartsWith("//", StringComparison.Ordinal)
                    || zeile.StartsWith("///", StringComparison.Ordinal)
                    || zeile.StartsWith("*", StringComparison.Ordinal)
                    || zeile.StartsWith("/*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (zeile.Contains("MischplanService.MitFeedchart(", StringComparison.Ordinal))
                {
                    treffer.Add($"{Path.GetFileName(datei)}:{i + 1}");
                }
            }
        }

        Assert.True(treffer.Count == 0,
            "Diese Stellen bauen die Zielband-Kette selbst: " + string.Join(", ", treffer)
            + ". Richtig ist Zielband.FuerGrow(...) — sonst nennt das Chart EC 2,6 und der "
            + "Bildschirm daneben 1,0-1,2, je nachdem wen man fragt.");
    }

    /// <summary>
    /// Und die Prüfung sieht ihre Grundmenge — sie findet den Aufruf, den es gibt.
    /// </summary>
    /// <remarks>
    /// Ohne diesen Selbsttest wäre die Zählung auch dann grün, wenn ihr
    /// Suchtext gar nicht mehr passt (umbenannte Methode, andere Schreibweise).
    /// </remarks>
    [Fact]
    public void DieZaehlungFindetDenEinenErlaubtenAufruf()
    {
        var zielband = Path.Combine(QuellVerzeichnis(), "Services", "Zielband.cs");
        Assert.True(File.Exists(zielband), $"{zielband} gibt es nicht — die Zaehlung sucht ins Leere.");

        Assert.Contains("MischplanService.MitFeedchart(", File.ReadAllText(zielband), StringComparison.Ordinal);
    }

    private static string QuellVerzeichnis()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var kandidat = Path.Combine(dir, "GrowDiary.Web");
            if (Directory.Exists(Path.Combine(kandidat, "Services"))) return kandidat;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Quellverzeichnis nicht gefunden.");
    }
}
