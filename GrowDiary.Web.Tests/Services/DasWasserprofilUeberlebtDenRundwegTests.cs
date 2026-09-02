using System.Reflection;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Das Wasserprofil kommt so zurück, wie es hineinging — jedes Feld.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>WaterProfileStore</c> stand auf der
/// Liste der Klassen, die kein Test je anfasst. Die Klasse ist klein und
/// sauber, aber sie hält etwas, das in <b>jede</b> Dünger-Rechnung eingeht: das
/// Leitungswasser des Nutzers (bei ihm weich, EC 0,28). Wer hier ein Feld
/// verliert, verschiebt jede Mischempfehlung.</para>
///
/// <para><b>Warum über Reflexion.</b> Eine handgeschriebene Liste kann nur an
/// dem scheitern, was schon draufsteht. Ein neues Feld am Profil — Kieselsäure,
/// Eisen, was auch immer — soll den Test rot machen, nicht stillschweigend
/// durchrutschen. Also läuft die Prüfung über alle Eigenschaften.</para>
/// </remarks>
public sealed class DasWasserprofilUeberlebtDenRundwegTests : IDisposable
{
    private readonly string _wurzel;
    private readonly WaterProfileStore _speicher;

    public DasWasserprofilUeberlebtDenRundwegTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Wasserprofil_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        var pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(pfade);
        _speicher = new WaterProfileStore(new AppSettingsRepository(pfade));
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Jedes Feld kommt zurück — auch die, die es morgen erst gibt.</summary>
    [Fact]
    public void JedesFeld_KommtZurueck()
    {
        var hinein = Gefuellt();
        _speicher.Save(hinein);

        var heraus = _speicher.Get();
        Assert.True(heraus is not null, "Nach dem Speichern kam nichts zurueck.");

        var abweichungen = new List<string>();
        var geprueft = 0;

        foreach (var feld in typeof(WaterProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!feld.CanWrite) continue;

            // Der Zeitstempel wird beim Speichern absichtlich neu gesetzt.
            if (feld.Name == nameof(WaterProfile.UpdatedAtUtc)) continue;

            geprueft += 1;
            var vorher = feld.GetValue(hinein);
            var nachher = feld.GetValue(heraus);
            if (!Equals(vorher, nachher))
            {
                abweichungen.Add($"{feld.Name}: hinein <{vorher}>, heraus <{nachher}>");
            }
        }

        // Mengenwaechter: sieht die Pruefung die Felder ueberhaupt?
        Assert.True(geprueft >= 12,
            $"Nur {geprueft} Felder gepruef — dann ist der Vergleich darunter wertlos.");

        Assert.True(abweichungen.Count == 0,
            "Diese Felder haben den Rundweg nicht ueberlebt:\n  " + string.Join("\n  ", abweichungen)
            + "\n\nDas Wasserprofil geht in JEDE Duenger-Rechnung ein; ein verlorenes Feld "
            + "verschiebt jede Mischempfehlung.");
    }

    /// <summary>Kommazahlen bleiben Kommazahlen.</summary>
    /// <remarks>
    /// Die Leitfähigkeit des Nutzers ist 280 µS/cm, sein Calcium 42,5 mg/l. In
    /// diesem Projekt ist der Dezimalpunkt schon mehrfach zur Falle geworden —
    /// hier geht der Wert durch JSON und zurück.
    /// </remarks>
    [Fact]
    public void EineKommazahl_KommtAlsKommazahlZurueck()
    {
        _speicher.Save(new WaterProfile
        {
            SourceLabel = "Leitung",
            ConductivityUsCm = 280,
            CalciumMgL = 42.5,
            Ph = 7.65,
        });

        var heraus = _speicher.Get()!;
        Assert.True(Math.Abs((heraus.CalciumMgL ?? 0) - 42.5) < 0.0001,
            $"Calcium kam als {heraus.CalciumMgL} zurueck, hinein gingen 42,5.");
        Assert.True(Math.Abs((heraus.Ph ?? 0) - 7.65) < 0.0001,
            $"pH kam als {heraus.Ph} zurueck, hinein gingen 7,65.");
    }

    /// <summary>Speichern stempelt den Zeitpunkt.</summary>
    /// <remarks>
    /// Er steht auf der Seite („zuletzt geändert"), und eine Wasseranalyse ist
    /// nach einem Jahr nicht mehr dieselbe. Ohne Stempel weiss niemand, wie alt
    /// die Zahlen sind.
    /// </remarks>
    [Fact]
    public void Speichern_StempeltDenZeitpunkt()
    {
        var vorher = DateTime.UtcNow.AddSeconds(-1);
        _speicher.Save(new WaterProfile { SourceLabel = "Leitung", UpdatedAtUtc = new DateTime(2001, 1, 1) });

        var heraus = _speicher.Get()!;
        Assert.True(heraus.UpdatedAtUtc >= vorher,
            $"Gespeichert wurde der Zeitpunkt {heraus.UpdatedAtUtc:O} — das ist der mitgegebene, "
            + "nicht der von jetzt. Auf der Seite steht dann ein Alter, das nicht stimmt.");
    }

    /// <summary>
    /// Ein kaputter Eintrag gilt als „kein Profil" — und reisst nichts mit.
    /// </summary>
    /// <remarks>
    /// Das steht so im Kommentar der Klasse. Geprüft hat es nie jemand, und ein
    /// Kommentar ist keine Zusage: ohne den Fang bricht jede Seite, die das
    /// Profil liest — Dosierung, Mischplan, Wasserwechsel.
    /// </remarks>
    [Fact]
    public void EinKaputterEintrag_GiltAlsKeinProfil()
    {
        var pfade = new AppPaths(_wurzel);
        new AppSettingsRepository(pfade).SetValue("water-profile", "{das ist kein JSON");

        var heraus = new WaterProfileStore(new AppSettingsRepository(pfade)).Get();

        Assert.True(heraus is null,
            "Ein kaputter Eintrag hat nicht als „kein Profil\" gegolten. Wenn stattdessen eine "
            + "Ausnahme fliegt, bricht jede Seite, die das Profil liest.");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>Ein Profil, in dem jedes beschreibbare Feld gesetzt ist.</summary>
    /// <remarks>
    /// Über Reflexion gefüllt: ein neues Feld wird damit automatisch mitgeprüft,
    /// statt still auf seinem Vorgabewert zu bleiben — und ein Vorgabewert, der
    /// den Rundweg übersteht, belegt nichts.
    /// </remarks>
    private static WaterProfile Gefuellt()
    {
        var profil = new WaterProfile();
        var zahl = 1.5;

        foreach (var feld in typeof(WaterProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!feld.CanWrite) continue;
            var typ = Nullable.GetUnderlyingType(feld.PropertyType) ?? feld.PropertyType;

            if (typ == typeof(double)) feld.SetValue(profil, zahl += 1.25);
            else if (typ == typeof(int)) feld.SetValue(profil, (int)(zahl += 1));
            else if (typ == typeof(bool)) feld.SetValue(profil, true);
            else if (typ == typeof(string)) feld.SetValue(profil, "Wert " + feld.Name);
            else if (typ == typeof(DateTime)) feld.SetValue(profil, new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));
            else throw new InvalidOperationException(
                $"Fuer {feld.Name} ({typ.Name}) gibt es hier keinen Fuellwert — bitte ergaenzen, "
                + "sonst prueft der Rundweg dieses Feld nicht.");
        }

        return profil;
    }
}
