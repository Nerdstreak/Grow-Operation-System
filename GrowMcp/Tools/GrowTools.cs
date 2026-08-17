using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GrowOsAccess;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GrowMcp.Tools;

/// <summary>
/// Die Griffe, die ein Modell an Grow OS hat.
/// </summary>
/// <remarks>
/// <para>Alle nur lesend. Es gibt bewusst kein Werkzeug zum Dosieren, Schalten
/// oder Ändern: die Sperren dafür sitzen in Grow OS, und was hier nicht gebaut
/// ist, kann auch nicht versehentlich ausgelöst werden. Ein Modell, das eine
/// Pumpe starten will, muss den Menschen fragen.</para>
///
/// <para>Zurückgegeben wird meist das JSON von Grow OS, unverändert. Das ist
/// ehrlicher als eine eigene Zusammenfassung — sie wäre eine zweite Stelle, an der
/// sich Bedeutung verschieben kann.</para>
/// </remarks>
[McpServerToolType]
public sealed class GrowTools(GrowOsReader reader)
{
    /// <summary>Die Messgrössen, die Grow OS kennt — für die Werkzeugbeschreibung.</summary>
    private const string Messgroessen =
        "reservoir-ph, reservoir-ec, reservoir-temp, reservoir-level, reservoir-level-cm, " +
        "orp, dissolved-oxygen, temperature, humidity, vpd, co2, ppfd";

    [McpServerTool(Name = "grows_auflisten")]
    [Description("Listet die laufenden Grows mit Id, Name, Sorte und Phase. Jedes andere Werkzeug braucht eine dieser Ids.")]
    public Task<string> GrowsAuflistenAsync(
        [Description("true, um auch abgeschlossene Grows zu sehen")] bool auchAbgeschlossene = false,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync(
            $"api/grows?archived={(auchAbgeschlossene ? "true" : "false")}", cancellationToken));

    [McpServerTool(Name = "lagebericht")]
    [Description("Der vollständige Stand eines Grows als Text: Phase, aktuelle Werte mit Zielbereich, offene Risiken, Journal und Dosierungen. Der beste erste Griff bei jeder Frage zu einem konkreten Grow.")]
    public Task<string> LageberichtAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var roh = await reader.LesenAsync($"api/agent-export/grows/{growId}", cancellationToken);
            using var json = JsonDocument.Parse(roh);
            return Text(json.RootElement, "markdown") ?? roh;
        });

    [McpServerTool(Name = "messwert_verlauf")]
    [Description("Der zeitliche Verlauf einzelner Messgrössen — das, was ein Momentwert nicht zeigt. Bis 2 Tage kommen Einzelmessungen, darüber Tageswerte mit Min, Mittel und Max.")]
    public Task<string> MesswertVerlaufAsync(
        [Description("Die Id des Grows")] int growId,
        [Description($"Eine oder mehrere Messgrössen, mit Komma getrennt. Möglich: {Messgroessen}")] string messgroessen,
        [Description("Wie viele Tage zurück, 1 bis 365")] int tage = 14,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            // Der Verlauf haengt am Zelt, nicht am Grow. Das Modell soll davon
            // nichts wissen muessen — die Zuordnung steht im Grow selbst.
            var grow = await DetailAsync(growId, cancellationToken);
            if (Zahl(grow, "tentId") is not { } zeltId)
            {
                return $"Der Grow {growId} hängt an keinem Zelt, deshalb gibt es keine Messreihen.";
            }

            var gefragt = Uri.EscapeDataString(messgroessen.Trim());
            return await reader.LesenAsync(
                $"api/tents/{zeltId}/history?metrics={gefragt}&days={tage}", cancellationToken);
        });

    [McpServerTool(Name = "trends")]
    [Description("Was Grow OS selbst an Bewegung erkannt hat: Befunde je Messgrösse und eine Einschätzung, wie stabil der Grow läuft.")]
    public Task<string> TrendsAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var befunde = await reader.LesenAsync($"api/trends/{growId}", cancellationToken);
            var stabilitaet = await reader.LesenAsync($"api/trends/{growId}/stability", cancellationToken);
            return Zusammen(("befunde", befunde), ("stabilitaet", stabilitaet));
        });

    [McpServerTool(Name = "abweichungen")]
    [Description("Wo der Grow von seinen Sollwerten abweicht, und welche Behandlungen Grow OS dazu vorschlägt.")]
    public Task<string> AbweichungenAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var abweichungen = await reader.LesenAsync($"api/grows/{growId}/deviations", cancellationToken);
            var vorschlaege = await reader.LesenAsync(
                $"api/grows/{growId}/treatment-recommendations", cancellationToken);
            return Zusammen(("abweichungen", abweichungen), ("vorschlaege", vorschlaege));
        });

    [McpServerTool(Name = "anlage")]
    [Description("Die Technik hinter dem Grow: Volumen, Pumpen, Kühler, UV-Klärer, Topfzahl. Wichtig für alles, was mit Mengen zu tun hat.")]
    public Task<string> AnlageAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var grow = await DetailAsync(growId, cancellationToken);
            return Zahl(grow, "systemId") is { } systemId
                ? await reader.LesenAsync($"api/hydro-setups/{systemId}", cancellationToken)
                : $"Dem Grow {growId} ist keine Anlage zugeordnet.";
        });

    [McpServerTool(Name = "sorte")]
    [Description("Die hinterlegte Sorte: Blütewochen, Stretch, Düngerbedarf und eigene Notizen aus früheren Läufen.")]
    public Task<string> SorteAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var grow = await DetailAsync(growId, cancellationToken);
            return Zahl(grow, "strainId") is { } sortenId
                ? await reader.LesenAsync($"api/strains/{sortenId}", cancellationToken)
                : $"Dem Grow {growId} ist keine Sorte aus der Sortenliste zugeordnet.";
        });

    [McpServerTool(Name = "alarme")]
    [Description("Was Grow OS als Problem führt: offene Risiko-Ereignisse zu diesem Grow und die Grenzwerte, die für sein Zelt eingestellt sind. Der Griff für „ist gerade etwas im Argen?\".")]
    public Task<string> AlarmeAsync(
        [Description("Die Id des Grows")] int growId,
        [Description("false, um auch bereits erledigte Risiko-Ereignisse zu sehen")] bool nurOffene = true,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var risiken = await reader.LesenAsync(
                $"api/risk-events?growId={growId}&openOnly={(nurOffene ? "true" : "false")}", cancellationToken);

            if (await ZeltAsync(growId, cancellationToken) is not { } zeltId)
            {
                return Zusammen(("risiken", risiken));
            }

            var grenzwerte = await DarfFehlenAsync($"api/alerts/tents/{zeltId}", cancellationToken);
            return Zusammen(("risiken", risiken), ("grenzwerte", grenzwerte));
        });

    [McpServerTool(Name = "dosierungen")]
    [Description("Die Dosierpumpen am Zelt und das Protokoll der letzten Dosen — was wann und wie viel eingebracht wurde.")]
    public Task<string> DosierungenAsync(
        [Description("Die Id des Grows")] int growId,
        [Description("Wie viele Protokolleinträge, höchstens 200")] int anzahl = 30,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            if (await ZeltAsync(growId, cancellationToken) is not { } zeltId)
            {
                return $"Der Grow {growId} hängt an keinem Zelt, deshalb gibt es keine Pumpen dazu.";
            }

            var grenze = Math.Clamp(anzahl, 1, 200);
            var pumpen = await reader.LesenAsync($"api/dosing/pumps?tentId={zeltId}", cancellationToken);
            var protokoll = await reader.LesenAsync(
                $"api/dosing/log?tentId={zeltId}&limit={grenze}", cancellationToken);

            return Zusammen(("pumpen", pumpen), ("protokoll", protokoll));
        });

    [McpServerTool(Name = "dosier_vorschlag")]
    [Description("Was Grow OS für diese Pumpe gerade dosieren würde, samt Begründung und den Sperren, die dabei greifen. Rechnet nur — es wird nichts geschaltet. Pumpen-Id kommt aus dosierungen.")]
    public Task<string> DosierVorschlagAsync(
        [Description("Die Id der Pumpe aus dem Werkzeug dosierungen")] int pumpeId,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync($"api/dosing/pumps/{pumpeId}/suggestion", cancellationToken));

    [McpServerTool(Name = "ablauf_fortschritt")]
    [Description("Welche Abläufe für diesen Grow gestartet sind und wie weit sie sind. Mit einer Instanz-Id kommen die einzelnen Schritte mit ihrem Zustand.")]
    public Task<string> AblaufFortschrittAsync(
        [Description("Die Id des Grows")] int growId,
        [Description("Die Id einer laufenden Ablauf-Instanz; weglassen, um alle zu sehen")] int? instanzId = null,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => instanzId is { } id
            ? reader.LesenAsync($"api/sop-instances/{id}/steps", cancellationToken)
            : reader.LesenAsync($"api/sop-instances?growId={growId}", cancellationToken));

    [McpServerTool(Name = "pflanzen")]
    [Description("Die einzelnen Pflanzen dieses Grows und, falls ein Pheno Hunt läuft, deren Bewertung im Vergleich.")]
    public Task<string> PflanzenAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var pflanzen = await reader.LesenAsync($"api/plants?growId={growId}", cancellationToken);
            var hunt = await DarfFehlenAsync($"api/pheno/grows/{growId}", cancellationToken);
            return Zusammen(("pflanzen", pflanzen), ("phenoHunt", hunt));
        });

    [McpServerTool(Name = "licht")]
    [Description("Der eingestellte Lichtzyklus des Zelts und die tatsächlich beobachteten An- und Aus-Zeitpunkte. Zeigt, ob die Lampe wirklich tut, was der Plan sagt.")]
    public Task<string> LichtAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            if (await ZeltAsync(growId, cancellationToken) is not { } zeltId)
            {
                return $"Der Grow {growId} hängt an keinem Zelt, deshalb gibt es keinen Lichtplan.";
            }

            var plan = await reader.LesenAsync($"api/light-schedules?tentId={zeltId}", cancellationToken);
            var beobachtet = await reader.LesenAsync($"api/light-transitions?tentId={zeltId}", cancellationToken);
            return Zusammen(("plan", plan), ("beobachtet", beobachtet));
        });

    [McpServerTool(Name = "technik")]
    [Description("Die Geräte am Zelt mit ihrem Zustand, dazu anstehende Wartungen und Sonden-Kalibrierungen. Der Griff für „was ist bald fällig?\".")]
    public Task<string> TechnikAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            if (await ZeltAsync(growId, cancellationToken) is not { } zeltId)
            {
                return $"Der Grow {growId} hängt an keinem Zelt, deshalb gibt es keine Geräte dazu.";
            }

            var geraete = await reader.LesenAsync($"api/hardware-items?tentId={zeltId}", cancellationToken);
            var wartung = await reader.LesenAsync("api/maintenance-events", cancellationToken);
            var kalibrierung = await reader.LesenAsync("api/calibration-events", cancellationToken);

            return Zusammen(("geraete", geraete), ("wartung", wartung), ("kalibrierung", kalibrierung));
        });

    [McpServerTool(Name = "journal")]
    [Description("Die Einträge, die der Betreiber selbst geschrieben hat. Was er getan und beobachtet hat, in seinen Worten.")]
    public Task<string> JournalAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync($"api/grows/{growId}/journal", cancellationToken));

    [McpServerTool(Name = "wissen_liste")]
    [Description("Das Fachwissen in Grow OS als Übersicht — die Kürzel, mit denen sich dann einzelne Einträge nachschlagen lassen. Bei sops und treatments kommen nur die Kopfdaten; den ganzen Eintrag holt wissen_nachschlagen.")]
    public Task<string> WissenListeAsync(
        [Description("Eine von: sops, treatments, symptoms, pathogens, setpoints")] string art,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            if (Bereich(art) is not { } pfad)
            {
                return $"„{art}\" gibt es nicht. Möglich sind: sops, treatments, symptoms, pathogens, setpoints.";
            }

            var liste = await reader.LesenAsync($"api/knowledge/{pfad}", cancellationToken);

            // Abläufe und Behandlungen bringen jeder Dutzende Schritte mit — die
            // ganze Liste waere ein Papierstapel von der Sorte, die dieses Add-on
            // gerade vermeiden soll. Gekuerzt wird nur, wo es auch einen Weg zum
            // vollen Eintrag gibt; Symptome, Erreger und Sollwerte haben keinen
            // Einzelabruf und sind ohnehin kurz.
            return pfad is "sops" or "treatments" ? NurKopfdaten(liste) : liste;
        });

    [McpServerTool(Name = "wissen_nachschlagen")]
    [Description("Einen einzelnen Eintrag im Fachwissen vollständig lesen, etwa den Ablauf 'root-rot-treatment'. Kürzel vorher mit wissen_liste oder suchen finden.")]
    public Task<string> WissenNachschlagenAsync(
        [Description("Eine von: sops, treatments")] string art,
        [Description("Das Kürzel des Eintrags, etwa root-rot-treatment")] string kuerzel,
        CancellationToken cancellationToken = default)
        => SicherAsync(async () =>
        {
            var pfad = Bereich(art);
            if (pfad is not ("sops" or "treatments"))
            {
                return $"Einzeln nachschlagen geht bei sops und treatments. Für „{art}\" nimm wissen_liste.";
            }

            try
            {
                return await reader.LesenAsync(
                    $"api/knowledge/{pfad}/{Uri.EscapeDataString(kuerzel)}", cancellationToken);
            }
            catch (GrowOsException)
            {
                // „Kennt Grow OS nicht" hilft niemandem weiter. Abläufe und
                // Behandlungen sehen von aussen gleich aus — root-rot-treatment
                // klingt wie eine Behandlung und ist ein Ablauf. Also nachsehen,
                // ob es in der anderen Schublade liegt, und dorthin verweisen.
                var andere = pfad == "sops" ? "treatments" : "sops";
                var liste = await reader.LesenAsync($"api/knowledge/{andere}", cancellationToken);

                return EnthaeltKuerzel(liste, kuerzel)
                    ? $"„{kuerzel}\" steht nicht unter {pfad}, sondern unter {andere}. "
                      + $"Ruf wissen_nachschlagen noch einmal mit art={andere} auf."
                    : $"„{kuerzel}\" gibt es weder unter sops noch unter treatments. "
                      + "Mit wissen_liste die vorhandenen Kürzel ansehen oder mit suchen danach fahnden.";
            }
        });

    [McpServerTool(Name = "suchen")]
    [Description("Volltextsuche über Grows, Journal und Fachwissen. Nützlich, wenn das passende Kürzel oder die Grow-Id noch fehlt.")]
    public Task<string> SuchenAsync(
        [Description("Der Suchbegriff, mindestens zwei Zeichen")] string begriff,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync(
            $"api/search?q={Uri.EscapeDataString(begriff.Trim())}", cancellationToken));

    /// <summary>
    /// Wie gross ein Bild sein darf, bevor es abgelehnt wird.
    /// </summary>
    /// <remarks>
    /// base64 blaeht Bytes um rund ein Drittel auf, und die Antwort muss durch
    /// das Kontextfenster des Modells passen. 6 MB roh sind etwa 8 MB kodiert —
    /// genug fuer jedes Handyfoto, das Grow OS ablegt, und weit genug von der
    /// Grenze entfernt, an der eine Antwort nur noch aus einem Bild besteht.
    /// </remarks>
    private const long MaxBildBytes = 6L * 1024 * 1024;

    [McpServerTool(Name = "fotos")]
    [Description("Die Fotos eines Grows als Liste: Id, Aufnahmezeit, Motiv-Tag (Overview, Leaf, Root, Problem …) und Bildunterschrift. Liefert noch keine Bilder — mit foto_ansehen holt man das einzelne Bild.")]
    public Task<string> FotosAsync(
        [Description("Die Id des Grows")] int growId,
        CancellationToken cancellationToken = default)
        => SicherAsync(() => reader.LesenAsync($"api/grows/{growId}/photos", cancellationToken));

    /// <summary>
    /// Ein Foto so herausgeben, dass das Modell es wirklich ansehen kann.
    /// </summary>
    /// <remarks>
    /// <para>Der Grund, warum es dieses Werkzeug gibt: Grow OS soll keine KI
    /// enthalten — kein fremder Schlüssel, keine Bilder, die das Haus ungefragt
    /// verlassen. Der Weg herum ist dieser: das Bild bleibt in Grow OS, und
    /// wer ein Modell fragen will, holt es sich über seinen eigenen Zugang.
    /// Die Entscheidung, ein Foto einem Modell zu zeigen, trifft damit der
    /// Betreiber pro Bild, nicht eine Voreinstellung.</para>
    ///
    /// <para>Zurück kommen zwei Blöcke: das Bild und ein kurzer Text mit dem,
    /// was Grow OS über die Aufnahme weiß. Ein Blatt ohne den Hinweis „Wurzel,
    /// vor 3 Tagen, Grow 4" ist nur ein Blatt.</para>
    /// </remarks>
    [McpServerTool(Name = "foto_ansehen")]
    [Description("Holt EIN Foto als Bild, damit du es wirklich ansehen kannst — für Fragen wie „was ist mit diesem Blatt?\". Die Id kommt aus fotos. Bilder über 6 MB werden abgelehnt.")]
    public async Task<IEnumerable<ContentBlock>> FotoAnsehenAsync(
        [Description("Die Id des Grows")] int growId,
        [Description("Die Id des Fotos aus dem Werkzeug fotos")] int fotoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roh = await reader.LesenAsync($"api/grows/{growId}/photos", cancellationToken);
            using var json = JsonDocument.Parse(roh);

            var treffer = json.RootElement.EnumerateArray()
                .FirstOrDefault(foto => foto.TryGetProperty("id", out var id) && id.GetInt32() == fotoId);

            if (treffer.ValueKind != JsonValueKind.Object)
            {
                return [new TextContentBlock { Text = $"Zu Grow {growId} gibt es kein Foto mit der Id {fotoId}. Die vorhandenen Ids liefert das Werkzeug fotos." }];
            }

            var pfad = treffer.TryGetProperty("relativePath", out var p) ? p.GetString() : null;
            if (string.IsNullOrWhiteSpace(pfad))
            {
                return [new TextContentBlock { Text = $"Foto {fotoId} hat keinen Dateipfad — es ist nur als Eintrag vorhanden." }];
            }

            var (bytes, medienTyp) = await reader.DateiLesenAsync(
                $"uploads/{pfad.TrimStart('/')}", MaxBildBytes, cancellationToken);

            return
            [
                ImageContentBlock.FromBytes(bytes, medienTyp),
                new TextContentBlock { Text = Aufnahmenotiz(treffer, growId) },
            ];
        }
        catch (GrowOsException ex)
        {
            return [new TextContentBlock { Text = ex.Message }];
        }
    }

    /// <summary>Was Grow OS über die Aufnahme weiss — der Satz neben dem Bild.</summary>
    public static string Aufnahmenotiz(JsonElement foto, int growId)
    {
        var teile = new List<string> { $"Grow {growId}" };

        if (foto.TryGetProperty("tag", out var tag) && tag.GetString() is { Length: > 0 } motiv)
        {
            teile.Add($"Motiv: {motiv}");
        }

        if (foto.TryGetProperty("takenAtUtc", out var zeit) && zeit.GetString() is { Length: > 0 } aufgenommen
            && DateTime.TryParse(aufgenommen, out var zeitpunkt))
        {
            var tage = (int)(DateTime.UtcNow - zeitpunkt.ToUniversalTime()).TotalDays;
            teile.Add(tage <= 0 ? "heute aufgenommen" : tage == 1 ? "gestern aufgenommen" : $"vor {tage} Tagen aufgenommen");
        }

        if (foto.TryGetProperty("caption", out var text) && text.GetString() is { Length: > 0 } bildunterschrift)
        {
            teile.Add($"Notiz des Betreibers: „{bildunterschrift}\"");
        }

        if (foto.TryGetProperty("measurementId", out var mess) && mess.ValueKind == JsonValueKind.Number)
        {
            teile.Add($"gehört zur Messung {mess.GetInt32()} — deren Werte liefert lagebericht oder messwert_verlauf");
        }

        return string.Join(" · ", teile);
    }

    /// <summary>Den Grow holen, um Zelt, Anlage und Sorte daraus zu lesen.</summary>
    private async Task<JsonElement> DetailAsync(int growId, CancellationToken cancellationToken)
    {
        var roh = await reader.LesenAsync($"api/grows/{growId}", cancellationToken);
        using var json = JsonDocument.Parse(roh);
        return json.RootElement.Clone();
    }

    /// <summary>
    /// Einen Teil holen, der auch fehlen darf.
    /// </summary>
    /// <remarks>
    /// Kennt Grow OS den Weg nicht — kein Pheno Hunt, keine Grenzwerte
    /// eingestellt —, kommt <c>null</c> zurück und der Rest des Ergebnisses
    /// bleibt stehen. Ist Grow OS dagegen gar nicht erreichbar, fliegt der Fehler
    /// weiter: ein halbes Ergebnis, das aussieht wie ein ganzes, wäre schlimmer
    /// als gar keins.
    /// </remarks>
    private async Task<string> DarfFehlenAsync(string pfad, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.LesenAsync(pfad, cancellationToken);
        }
        catch (GrowOsException ex) when (ex.NichtGefunden)
        {
            return "null";
        }
    }

    /// <summary>Das Zelt zu einem Grow — oder <c>null</c>, wenn keins zugeordnet ist.</summary>
    /// <remarks>
    /// Vieles hängt in Grow OS am Zelt und nicht am Grow: Messreihen, Grenzwerte,
    /// Pumpen, Licht, Geräte. Das Modell soll davon nichts wissen müssen, es
    /// kennt nur den Grow.
    /// </remarks>
    private async Task<int?> ZeltAsync(int growId, CancellationToken cancellationToken)
        => Zahl(await DetailAsync(growId, cancellationToken), "tentId");

    /// <summary>
    /// Mehrere Antworten zu einem Objekt zusammenfassen.
    /// </summary>
    /// <remarks>
    /// Die Teile sind bereits gültiges JSON und werden roh eingesetzt, statt sie
    /// zu zerlegen und neu zu schreiben — das spart einen Umbau, bei dem nur
    /// etwas verlorengehen kann.
    /// <para>Öffentlich, damit geprüft werden kann, dass dabei gültiges JSON
    /// herauskommt. Ein zusammengeklebtes Ergebnis, das sich nicht lesen lässt,
    /// wäre für ein Modell schlimmer als eine Fehlermeldung.</para>
    /// </remarks>
    public static string Zusammen(params (string Name, string Json)[] teile)
    {
        var text = new StringBuilder("{");
        for (var i = 0; i < teile.Length; i++)
        {
            if (i > 0) text.Append(',');
            text.Append('"').Append(teile[i].Name).Append("\":").Append(teile[i].Json);
        }

        return text.Append('}').ToString();
    }

    /// <summary>
    /// Erwartbare Fehler als Satz zurückgeben statt als Absturz.
    /// </summary>
    /// <remarks>
    /// „Grow OS ist nicht erreichbar" ist eine Antwort, keine Panne — das Modell
    /// kann sie weitergeben und der Mensch weiss, was zu tun ist. Alles andere
    /// fliegt weiter und wird zum Werkzeugfehler; Stillschweigen waere schlimmer.
    /// </remarks>
    private static async Task<string> SicherAsync(Func<Task<string>> arbeit)
    {
        try
        {
            return await arbeit();
        }
        catch (GrowOsException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Aus jedem Eintrag nur die einfachen Felder behalten.
    /// </summary>
    /// <remarks>
    /// Die Regel ist absichtlich stumpf: Text, Zahl und Ja/Nein bleiben, alles
    /// Verschachtelte fliegt raus. Damit überlebt jede Übersicht auch ein neues
    /// Feld im Wissen, ohne dass hier jemand nachziehen muss — und Schritte,
    /// Quellen und Materiallisten landen erst dann im Gespräch, wenn jemand den
    /// Eintrag wirklich aufschlägt.
    /// <para>Öffentlich, damit die Kürzung geprüft werden kann — sie ist der
    /// Unterschied zwischen einer Übersicht und einem Papierstapel.</para>
    /// </remarks>
    public static string NurKopfdaten(string listeAlsJson)
    {
        using var json = JsonDocument.Parse(listeAlsJson);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return listeAlsJson;

        using var speicher = new MemoryStream();

        // Umlaute bleiben Umlaute. Die Vorgabe schreibt „Wurzelfäule" — gueltig,
        // aber laenger und schlechter zu lesen. „Unsafe" heisst hier nur: nicht fuer
        // HTML gedacht, und das ist eine Werkzeugantwort auch nicht.
        var einstellungen = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        using (var schreiber = new Utf8JsonWriter(speicher, einstellungen))
        {
            schreiber.WriteStartArray();
            foreach (var eintrag in json.RootElement.EnumerateArray())
            {
                schreiber.WriteStartObject();
                foreach (var feld in eintrag.EnumerateObject())
                {
                    if (feld.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number
                        or JsonValueKind.True or JsonValueKind.False)
                    {
                        feld.WriteTo(schreiber);
                    }
                }
                schreiber.WriteEndObject();
            }
            schreiber.WriteEndArray();
        }

        return Encoding.UTF8.GetString(speicher.ToArray());
    }

    /// <summary>Steht dieses Kürzel in der übergebenen Liste?</summary>
    private static bool EnthaeltKuerzel(string listeAlsJson, string kuerzel)
    {
        using var json = JsonDocument.Parse(listeAlsJson);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return false;

        return json.RootElement.EnumerateArray()
            .Any(eintrag => string.Equals(Text(eintrag, "id"), kuerzel, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Bereich(string art) => art.Trim().ToLowerInvariant() switch
    {
        "sops" or "sop" or "ablauf" or "ablaeufe" => "sops",
        "treatments" or "treatment" or "behandlungen" => "treatments",
        "symptoms" or "symptome" => "symptoms",
        "pathogens" or "erreger" => "pathogens",
        "setpoints" or "sollwerte" => "setpoints",
        _ => null,
    };

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String
            ? wert.GetString()
            : null;

    private static int? Zahl(JsonElement element, string name)
        => element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.Number
            ? wert.GetInt32()
            : null;
}
