using System.Collections;
using System.Reflection;
using GrowDiary.Web.Api.Mapping;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Kein Mapping lässt ein Feld fallen.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b>
/// <c>GrowWorkflowMapping.ToEntry</c> kopierte <c>PlantWeightsJson</c> nicht.
/// Wer eine Ernte über den Abschluss-Weg speicherte, sah die Summe und verlor
/// die Aufteilung je Pflanze — ohne Meldung, ohne Fehler im Protokoll.</para>
///
/// <para><b>Warum die Abdeckung das nie findet.</b> Ein vergessenes Feld ist
/// eine Zeile, die es <b>nicht gibt</b>. <c>GrowWorkflowMapping</c> stand bei
/// 76 % — und die meisten Mappings daneben bei 100 %, ohne dass das irgendetwas
/// über verlorene Felder aussagt. Zeilenabdeckung kann diese Klasse Fehler
/// grundsätzlich nicht sehen.</para>
///
/// <para><b>Was hier gemessen wird.</b> Über die <b>Grundmenge</b> aller
/// Mapping-Methoden: Quelle mit lauter unterscheidbaren Werten füllen,
/// abbilden, und für jede Eigenschaft, die es auf <b>beiden</b> Seiten unter
/// demselben Namen gibt, den Wert vergleichen. Kommt dort der Standardwert an,
/// ist das Feld unterwegs verloren gegangen.</para>
///
/// <para>Bewusst nur gleichnamige Eigenschaften: eine Umbenennung oder eine
/// Umrechnung ist eine Entscheidung, kein Versehen. Wo eine gleichnamige
/// Eigenschaft absichtlich nicht übernommen wird, steht sie unten in
/// <see cref="MitGrund"/> — mit ausgeschriebenem Grund.</para>
/// </remarks>
public sealed class KeinFeldGehtVerlorenTests
{
    /// <summary>
    /// Gleichnamige Eigenschaften, die absichtlich nicht übernommen werden.
    /// </summary>
    /// <remarks>
    /// Schlüssel: <c>Klasse.Methode.Eigenschaft</c>. Kein Freibrief — wer hier
    /// etwas einträgt, weil eine Prüfung rot ist, hat sie abgeschaltet statt den
    /// Fehler behoben.
    /// </remarks>
    private static readonly Dictionary<string, string> MitGrund = new(StringComparer.Ordinal)
    {
        ["SettingsMapping.ToDto.AccessToken"] =
            "Absicht: das Zugangstoken zu Home Assistant wird zu ******** maskiert. "
            + "Ein Geheimnis, das die API herausgibt, steht danach im Browser-Verlauf, "
            + "im Netzwerk-Mitschnitt und in jedem Fehlerbericht.",
    };

    /// <summary>
    /// Mapping-Methoden, die sich nicht mit erfundenen Werten aufrufen lassen.
    /// </summary>
    /// <remarks>
    /// Schlüssel: <c>Klasse.Methode</c>. Auch hier kein Freibrief — der Grund
    /// muß erklären, warum ein tauglicher Füllwert nicht möglich ist, nicht
    /// warum die Prüfung gerade stört.
    /// </remarks>
    private static readonly Dictionary<string, string> NichtBlindAufrufbar = new(StringComparer.Ordinal);

    [Fact]
    public void JedesMapping_TraegtJedesGleichnamigeFeld()
    {
        var methoden = MappingMethoden();

        // Mengenwaechter: ohne Grundmenge laeuft die Schleife null Mal durch.
        Assert.True(methoden.Count >= 30,
            $"Nur {methoden.Count} Mapping-Methoden gefunden — die Grundmenge stimmt nicht, "
            + "und diese Zaehlung prueft dann nichts.");

        var verloren = new List<string>();
        var uebersprungen = new List<string>();
        var untersucht = 0;

        foreach (var (methode, quelleTyp) in methoden)
        {
            var wer = $"{methode.DeclaringType!.Name}.{methode.Name}";
            if (NichtBlindAufrufbar.ContainsKey(wer)) continue;
            object? quelle;
            object? ergebnis;
            try
            {
                quelle = Fuellen(quelleTyp);
                if (quelle is null)
                {
                    uebersprungen.Add($"{wer}: die Quelle {quelleTyp.Name} liess sich nicht erzeugen");
                    continue;
                }

                ergebnis = Aufrufen(methode, quelle);
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                var kern = ex.InnerException ?? ex;
                uebersprungen.Add($"{wer}: {kern.GetType().Name} — {kern.Message}");
                continue;
            }

            if (ergebnis is null)
            {
                uebersprungen.Add($"{wer}: lieferte null");
                continue;
            }

            foreach (var (name, quellWert, zielWert) in Gleichnamige(quelle, ergebnis))
            {
                untersucht++;
                var schluessel = $"{methode.DeclaringType!.Name}.{methode.Name}.{name}";
                if (MitGrund.ContainsKey(schluessel)) continue;

                if (!Equals(quellWert, zielWert))
                {
                    verloren.Add(
                        $"{schluessel}: Quelle „{Zeige(quellWert)}\", Ergebnis „{Zeige(zielWert)}\"");
                }
            }
        }

        /* KEIN stilles Ueberspringen.
           Die erste Fassung dieser Datei fing jede Ausnahme und ging weiter.
           `GrowWorkflowMapping.ToEntry` ruft DateTime.Parse(request.HarvestedAtLocal)
           auf; mein Fuellwert war "wert-HarvestedAtLocal", die Methode warf, und
           die Zaehlung sprang darueber hinweg — ausgerechnet ueber die Methode,
           fuer die sie gebaut wurde. Der Bissnachweis (PlantWeightsJson wieder
           entfernt) blieb GRUEN.

           Genau das Muster, das CLAUDE.md verbietet: "Ein uebersprungener Test
           ist kein bestandener." Wer eine Methode nicht blind aufrufen kann,
           traegt sie unten mit Grund ein — er verschweigt sie nicht. */
        Assert.True(uebersprungen.Count == 0,
            $"Diese {uebersprungen.Count} Mapping-Methoden wurden gar nicht geprueft:\n  "
            + string.Join("\n  ", uebersprungen)
            + "\n\nEine uebersprungene Methode ist keine gepruefte. Entweder den Fuellwert "
            + "so waehlen, dass die Methode laeuft (siehe Kennwert), oder die Methode in "
            + "NichtBlindAufrufbar mit Grund eintragen.");

        // Und die Zahl der wirklich verglichenen Paare: ohne sie waere die
        // Pruefung auch dann gruen, wenn ueberall nur Standardwerte ankaemen.
        Assert.True(untersucht >= 300,
            $"Nur {untersucht} Felderpaare verglichen — zu wenig fuer {methoden.Count} "
            + "Mapping-Methoden. Diese Zaehlung prueft dann fast nichts.");

        Assert.True(verloren.Count == 0,
            $"Diese gleichnamigen Felder gehen im Mapping verloren ({verloren.Count} von "
            + $"{untersucht} verglichenen):\n  " + string.Join("\n  ", verloren)
            + "\n\nEin vergessenes Feld ist stiller Datenverlust: der Nutzer traegt etwas ein, "
            + "die App meldet Erfolg, und beim naechsten Aufruf ist es weg. Entweder uebernehmen "
            + "oder oben in MitGrund mit Grund eintragen.");
    }

    /// <summary>Und die Zählung sieht ihre Grundmenge wirklich an.</summary>
    /// <remarks>
    /// Ohne diesen Selbsttest wäre oben auch dann alles gut, wenn die
    /// Namenssuche ins Leere liefe — etwa nach einer Umbenennung des
    /// Namensraums.
    /// </remarks>
    [Fact]
    public void DieZaehlungFindetDieBekanntenMappings()
    {
        var namen = MappingMethoden()
            .Select(m => m.Methode.DeclaringType!.Name)
            .Distinct()
            .ToList();

        foreach (var erwartet in new[] { "GrowWorkflowMapping", "TaskMapping", "GrowMapping", "MeasurementMapping" })
        {
            Assert.True(namen.Contains(erwartet),
                $"{erwartet} ist nicht in der Grundmenge ({namen.Count} Klassen: "
                + $"{string.Join(", ", namen)}). Die Zaehlung sucht ins Leere.");
        }
    }

    // ------------------------------------------------------------- Grundmenge

    private static List<(MethodInfo Methode, Type Quelle)> MappingMethoden()
    {
        var raus = new List<(MethodInfo, Type)>();

        foreach (var typ in typeof(TaskMapping).Assembly.GetTypes()
                     .Where(t => t.Namespace == "GrowDiary.Web.Api.Mapping" && t.IsAbstract && t.IsSealed))
        {
            foreach (var m in typ.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var p = m.GetParameters();
                if (p.Length == 0) continue;

                // Der erste Parameter ist die Quelle (Erweiterungsmethoden).
                var quelle = p[0].ParameterType;
                if (quelle.IsPrimitive || quelle == typeof(string) || quelle.IsEnum) continue;

                // ApplyTo(request, ziel) und ToDto(quelle) — beide zaehlen.
                /* Nur Rueckgabetypen aus der Anwendung selbst — ein DTO oder
                   ein Modell. `HydroSetupMapping.CalculateTotalVolumeLiters`
                   liefert double? und ist eine Rechnung, kein Mapping: dort gibt
                   es keine gleichnamigen Felder zu vergleichen. */
                var liefertObjekt = m.ReturnType != typeof(void)
                                    && m.ReturnType.Assembly == typ.Assembly;
                var istApplyTo = m.ReturnType == typeof(void) && p.Length >= 2;
                if (!liefertObjekt && !istApplyTo) continue;

                raus.Add((m, quelle));
            }
        }

        return raus;
    }

    // ------------------------------------------------------------- Aufrufen

    private static object? Aufrufen(MethodInfo methode, object quelle)
    {
        var p = methode.GetParameters();
        var werte = new object?[p.Length];
        werte[0] = quelle;

        /* Weitere Parameter werden GEFUELLT, nicht auf null gelassen.
           `RequestMapping.ToFormModel(request, GrowRun grow)` liest grow.Id —
           mit null warf die Methode, und die Zaehlung haette sie uebersprungen. */
        for (var i = 1; i < p.Length; i++)
        {
            var t = p[i].ParameterType;
            werte[i] = t == typeof(string) ? "wert"
                : t.IsValueType ? Standard(t)
                : Fuellen(t);
        }

        if (methode.ReturnType == typeof(void))
        {
            // ApplyTo: das ZIEL ist der zweite Parameter, frisch und leer.
            var ziel = Activator.CreateInstance(p[1].ParameterType);
            if (ziel is null) return null;
            werte[1] = ziel;
            methode.Invoke(null, werte);
            return ziel;
        }

        return methode.Invoke(null, werte);
    }

    // ------------------------------------------------------------- Vergleich

    private static IEnumerable<(string Name, object? Quelle, object? Ziel)> Gleichnamige(
        object quelle, object ziel)
    {
        var zielEigenschaften = ziel.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        foreach (var q in quelle.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!q.CanRead || !zielEigenschaften.TryGetValue(q.Name, out var z)) continue;
            if (!Vergleichbar(q.PropertyType) || q.PropertyType != z.PropertyType) continue;

            object? qw;
            object? zw;
            try
            {
                qw = q.GetValue(quelle);
                zw = z.GetValue(ziel);
            }
            catch
            {
                continue;
            }

            // Konnte die Quelle gar nicht besetzt werden, sagt der Vergleich nichts.
            if (qw is null || Equals(qw, Standard(q.PropertyType))) continue;

            yield return (q.Name, qw, zw);
        }
    }

    // ------------------------------------------------------------- Werte

    /// <summary>Typen, deren Gleichheit sich sinnvoll vergleichen lässt.</summary>
    private static bool Vergleichbar(Type t)
    {
        var kern = Nullable.GetUnderlyingType(t) ?? t;
        if (kern.IsEnum) return true;
        if (kern == typeof(string) || kern == typeof(bool) || kern == typeof(DateTime)) return true;
        return kern.IsPrimitive || kern == typeof(decimal);
    }

    private static object? Standard(Type t)
        => t.IsValueType ? Activator.CreateInstance(t) : null;

    /// <summary>Eine Quelle, in der jedes Feld einen unterscheidbaren Wert trägt.</summary>
    /// <remarks>
    /// <b>Nie der Standardwert.</b> Ein Feld, das mit <c>0</c>, <c>false</c>
    /// oder dem ersten Enum-Wert besetzt wäre, sähe nach dem Verlieren genauso
    /// aus wie davor — die Prüfung wäre grün, ohne etwas gesehen zu haben.
    /// </remarks>
    private static object? Fuellen(Type typ)
    {
        var objekt = Erzeugen(typ);
        if (objekt is null) return null;

        foreach (var p in typ.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanWrite || !Vergleichbar(p.PropertyType)) continue;
            var wert = Kennwert(p.PropertyType, p.Name);
            if (wert is null) continue;
            try { p.SetValue(objekt, wert); } catch { /* init-only o.ae. */ }
        }

        return objekt;
    }

    /// <summary>
    /// Erzeugt eine Instanz — auch für positionale <c>record</c>s ohne
    /// parameterlosen Konstruktor.
    /// </summary>
    private static object? Erzeugen(Type typ)
    {
        try
        {
            if (typ.GetConstructor(Type.EmptyTypes) is not null)
            {
                return Activator.CreateInstance(typ);
            }

            var ctor = typ.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();
            if (ctor is null) return null;

            var werte = ctor.GetParameters()
                .Select(p => Kennwert(p.ParameterType, p.Name ?? "x") ?? LeererWert(p.ParameterType))
                .ToArray();
            return ctor.Invoke(werte);
        }
        catch
        {
            return null;
        }
    }

    private static object? LeererWert(Type t)
    {
        if (t == typeof(string)) return string.Empty;
        if (t.IsValueType) return Activator.CreateInstance(t);
        if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
        {
            try { return Array.CreateInstance(t.GetGenericArguments()[0], 0); } catch { return null; }
        }
        return null;
    }

    /// <summary>Ein Wert, der sich vom Standardwert unterscheidet.</summary>
    private static object? Kennwert(Type typ, string name)
    {
        var kern = Nullable.GetUnderlyingType(typ) ?? typ;
        // Aus dem Namen, damit ein vertauschtes Feldpaar auffaellt.
        var streu = Math.Abs(name.Aggregate(17, (a, c) => a * 31 + c)) % 900 + 100;

        /* Zeichenketten, die ein Datum MEINEN, bekommen ein Datum.
           Sonst wirft die Methode beim Parsen, und die Zaehlung haette sie
           uebersprungen — genau so ist ihr der Fehler entgangen, fuer den sie
           gebaut wurde. */
        if (kern == typeof(string))
        {
            if (name.EndsWith("Local", StringComparison.Ordinal)
                || name.EndsWith("Utc", StringComparison.Ordinal)
                || name.EndsWith("At", StringComparison.Ordinal)
                || name.EndsWith("Date", StringComparison.Ordinal))
            {
                return new DateTime(2026, 3, 4).AddDays(streu % 300).ToString("yyyy-MM-dd");
            }

            if (name.EndsWith("Time", StringComparison.Ordinal))
            {
                return $"{streu % 24:00}:{streu % 60:00}";
            }

            return $"wert-{name}";
        }
        if (kern == typeof(bool)) return true;
        if (kern == typeof(DateTime)) return new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc).AddMinutes(streu);
        if (kern == typeof(int)) return streu;
        if (kern == typeof(long)) return (long)streu;
        if (kern == typeof(double)) return streu + 0.5;
        if (kern == typeof(decimal)) return (decimal)streu + 0.5m;
        if (kern == typeof(float)) return (float)streu + 0.5f;

        if (kern.IsEnum)
        {
            // NICHT der erste Wert: der ist meist der Standardwert, und ein
            // verlorenes Feld saehe damit aus wie ein uebernommenes.
            var werte = Enum.GetValues(kern);
            return werte.Length > 1 ? werte.GetValue(werte.Length - 1) : werte.GetValue(0);
        }

        return null;
    }

    private static string Zeige(object? wert)
        => wert switch
        {
            null => "null",
            DateTime d => d.ToString("O"),
            _ => wert.ToString() ?? "",
        };
}
