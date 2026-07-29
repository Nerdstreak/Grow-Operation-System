using System.Net;
using GrowMcp;
using GrowMcp.Services;
using GrowMcp.Tools;
using GrowOsAccess;

// Grow MCP: Grow OS als Werkzeugkasten fuer einen beliebigen MCP-Klienten —
// Claude Code, Claude Desktop, was auch immer im eigenen Netz laeuft.
//
// Der Unterschied zum Berater-Add-on: dort bekommt das Modell EINEN fertigen
// Stapel Papier, hier bekommt es Griffe und fragt gezielt nach. Das ist der
// einzige Weg zu Verlaufsfragen — ein Momentwert zeigt keine Bewegung.
//
// Zwei Tueren, mit Absicht getrennt:
//   Port 5078  Ingress. Die Seite, auf der der Schluessel steht. Home Assistant
//              besitzt hier die Anmeldung, nach draussen ist der Port zu.
//   Port 5079  Das WLAN. Nur die MCP-Schnittstelle, nur mit Schluessel. Wer hier
//              anklopft, sieht die Seite mit dem Schluessel NICHT — sonst haette
//              das Absichern keinen Sinn.

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Beide Ports fest im Programm statt ueber ASPNETCORE_URLS: welcher Port was
    // darf, ist hier eine Sicherheitsfrage und keine Umgebungsvariable.
    kestrel.ListenAnyIP(Tueren.IngressPort);
    kestrel.ListenAnyIP(Tueren.NetzPort);
});

var einstellungen = McpEinstellungen.Laden(
    LoggerFactory.Create(bau => bau.AddConsole()).CreateLogger<McpEinstellungen>());

builder.Services.AddSingleton(einstellungen);
builder.Services.AddSingleton<TokenSpeicher>();
builder.Services.AddGrowOsAccess(einstellungen.GrowOsAdresse);

builder.Services.AddMcpServer()
    // Zustandslos: dieser Server haelt nichts zwischen zwei Aufrufen fest, also
    // braucht er auch keine Sitzung. Ein Neustart kostet damit nichts.
    .WithHttpTransport(transport => transport.Stateless = true)
    .WithTools<GrowTools>();

var app = builder.Build();

app.Use(async (kontext, weiter) =>
{
    var speicher = kontext.RequestServices.GetRequiredService<TokenSpeicher>();
    var zutritt = Tueren.Pruefen(
        kontext.Connection.LocalPort,
        kontext.Request.Path.Value ?? "/",
        speicher.Stimmt(Mitgeschickt(kontext.Request)));

    switch (zutritt)
    {
        case Zutritt.NichtGefunden:
            kontext.Response.StatusCode = (int)HttpStatusCode.NotFound;
            return;

        case Zutritt.SchluesselFehlt:
            kontext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            kontext.Response.Headers.WWWAuthenticate = "Bearer";
            await kontext.Response.WriteAsync("Kein oder falscher Zugriffsschluessel.");
            return;

        default:
            await weiter();
            return;
    }
});

app.MapMcp(Tueren.McpPfad);

app.MapGet("/", (HttpRequest anfrage, TokenSpeicher speicher, GrowOsDiscovery suche, CancellationToken ct)
    => Einrichtungsseite.RendernAsync(anfrage, speicher, suche, ct));

app.Run();

/// <summary>Den Schlüssel aus dem Kopf der Anfrage holen.</summary>
static string? Mitgeschickt(HttpRequest anfrage)
{
    var kopf = anfrage.Headers.Authorization.ToString();
    return kopf.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? kopf["Bearer ".Length..].Trim()
        : null;
}
