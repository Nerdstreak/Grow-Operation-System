using System.Net;
using System.Text;
using GrowOsAccess;

namespace GrowMcp.Services;

/// <summary>
/// Die Seite, die in der Home-Assistant-Seitenleiste erscheint.
/// </summary>
/// <remarks>
/// <para>Sie hat eine Aufgabe: den fertigen Befehl zeigen, mit dem sich ein
/// Klient verbindet. Ein Schlüssel, den man aus einem Protokoll abschreiben muss,
/// wird falsch abgeschrieben.</para>
///
/// <para>Die Adresse wird aus der Anfrage genommen, nicht geraten — der Name, unter
/// dem der Betreiber gerade Home Assistant offen hat, ist genau der Name, unter dem
/// er es auch vom selben Rechner aus erreicht.</para>
/// </remarks>
public static class Einrichtungsseite
{
    public static async Task<IResult> RendernAsync(
        HttpRequest anfrage, TokenSpeicher speicher, GrowOsDiscovery suche, CancellationToken cancellationToken)
    {
        var verbindung = await suche.FindenAsync(cancellationToken);
        var host = anfrage.Host.Host;
        var befehl = $"claude mcp add --transport http grow-os http://{host}:{Tueren.NetzPort}{Tueren.McpPfad} "
                   + $"--header \"Authorization: Bearer {speicher.Token}\"";

        var zustand = verbindung.Erreichbar ? "gut" : "schlecht";

        // Solange Grow OS nicht antwortet, ist der Verbindungsbefehl eine Falle:
        // er funktioniert, aber jedes Werkzeug laeuft danach ins Leere. Also erst
        // sagen, was fehlt.
        var befehlsKarte = verbindung.Erreichbar
            ? $"""
              <section>
                <h2>So verbindest du dich</h2>
                <p>Diesen Befehl auf dem Rechner ausführen, auf dem Claude Code läuft:</p>
                <pre id="befehl">{WebUtility.HtmlEncode(befehl)}</pre>
                <button type="button" onclick="kopieren()">Befehl kopieren</button>
              </section>
              """
            : """
              <section>
                <h2>So verbindest du dich</h2>
                <p>Erst muss Grow OS erreichbar sein — sonst verbindet sich Claude zwar,
                   aber jede Frage läuft ins Leere. Prüf der Reihe nach:</p>
                <ul>
                  <li>Läuft das Add-on <strong>Grow OS</strong>?</li>
                  <li>Ist es mindestens <strong>2.0.0-beta.24</strong>? Erst ab da lässt es
                      andere Add-ons mitlesen.</li>
                  <li>Sonst: Adresse aus <em>Grow OS → Info → Hostname</em> oben unter
                      <code>grow_os_adresse</code> eintragen, mit <code>:5076</code> am Ende.</li>
                </ul>
                <p>Danach diese Seite neu laden — dann steht der Befehl hier.</p>
              </section>
              """;

        var html = $$"""
            <!doctype html>
            <html lang="de">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Grow MCP</title>
              <style>
                :root {
                  color-scheme: light dark;
                  --bg: #ffffff; --text: #1a1a1a; --dim: #5f5e5a;
                  --karte: #f4f3ef; --rand: #d8d6cd;
                  --gut: #2b6a4a; --schlecht: #9a3232;
                }
                @media (prefers-color-scheme: dark) {
                  :root {
                    --bg: #16181a; --text: #e8e6e1; --dim: #a3a19b;
                    --karte: #1f2225; --rand: #34383c;
                    --gut: #7fd0a4; --schlecht: #f0a0a0;
                  }
                }
                * { box-sizing: border-box; }
                body {
                  margin: 0; padding: 24px; background: var(--bg); color: var(--text);
                  font: 16px/1.6 system-ui, -apple-system, "Segoe UI", sans-serif;
                }
                main { max-width: 720px; margin: 0 auto; }
                h1 { font-size: 22px; font-weight: 500; margin: 0 0 4px; }
                p.lead { color: var(--dim); margin: 0 0 24px; }
                section { background: var(--karte); border: 1px solid var(--rand); border-radius: 12px; padding: 20px; margin-bottom: 16px; }
                h2 { font-size: 16px; font-weight: 500; margin: 0 0 12px; }
                .zustand { font-weight: 500; }
                .zustand.gut { color: var(--gut); }
                .zustand.schlecht { color: var(--schlecht); }
                pre {
                  background: var(--bg); border: 1px solid var(--rand); border-radius: 8px;
                  padding: 14px; overflow-x: auto; font-size: 13px; margin: 0 0 12px;
                  white-space: pre-wrap; word-break: break-all;
                }
                button {
                  font: inherit; font-size: 14px; padding: 8px 16px; cursor: pointer;
                  background: var(--bg); color: var(--text);
                  border: 1px solid var(--rand); border-radius: 8px;
                }
                button:hover { border-color: var(--text); }
                ul { margin: 0; padding-left: 20px; color: var(--dim); }
                li { margin-bottom: 6px; }
              </style>
            </head>
            <body>
            <main>
              <h1>Grow MCP</h1>
              <p class="lead">Verbindet Claude auf deinem Rechner mit deiner Anlage.</p>

              <section>
                <h2>Verbindung zu Grow OS</h2>
                <p class="zustand {{zustand}}">{{WebUtility.HtmlEncode(verbindung.Meldung)}}</p>
              </section>

              {{befehlsKarte}}

              <section>
                <h2>Was du wissen solltest</h2>
                <ul>
                  <li>Der Schlüssel steht im Befehl. Gib ihn nicht weiter — wer ihn hat, kann deine Grow-Daten lesen.</li>
                  <li>Erreichbar ist das nur in deinem eigenen Netz. Aus dem Internet kommt niemand hier an.</li>
                  <li>Gelesen wird nur. Dosieren und Schalten bleiben in Grow OS.</li>
                  <li>Neuen Schlüssel gewünscht? Datei <code>mcp-token</code> im Add-on-Speicher löschen und neu starten.</li>
                </ul>
              </section>
            </main>
            <script>
              function kopieren() {
                const befehl = document.getElementById('befehl');
                if (!befehl) return;
                navigator.clipboard.writeText(befehl.textContent);
                const knopf = document.querySelector('button');
                knopf.textContent = 'Kopiert';
                setTimeout(() => knopf.textContent = 'Befehl kopieren', 1500);
              }
            </script>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8);
    }
}
