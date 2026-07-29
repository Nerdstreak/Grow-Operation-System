using GrowAgent.Services;
using GrowOsAccess;

// Der Grow-Berater als eigenes Home-Assistant-Add-on.
//
// Aufteilung, wie mit dem Betreiber abgesprochen: Grow OS bleibt ohne
// Schluessel und ohne KI. Dieses Add-on haelt den Modell-Schluessel, holt sich
// den Lagebericht und das Fachwissen ueber die Schnittstelle von Grow OS und
// fuehrt das Gespraech. Beide reden im internen HA-Netz miteinander; nach
// draussen ist kein Port offen.

var builder = WebApplication.CreateBuilder(args);

var einstellungen = AgentEinstellungen.Laden(
    LoggerFactory.Create(bau => bau.AddConsole()).CreateLogger<AgentEinstellungen>());

builder.Services.AddSingleton(einstellungen);
builder.Services.AddGrowOsAccess(einstellungen.GrowOsAdresse);
builder.Services.AddHttpClient<GrowOsClient>();
builder.Services.AddHttpClient<ModellClient>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/verbindung", async (GrowOsDiscovery suche, CancellationToken ct) =>
{
    var verbindung = await suche.FindenAsync(ct);
    return Results.Ok(new
    {
        gefunden = verbindung.Basis is not null,
        erreichbar = verbindung.Erreichbar,
        adresse = verbindung.Basis,
        meldung = verbindung.Meldung,
    });
});

app.MapGet("/api/grows", async (GrowOsDiscovery suche, GrowOsClient growOs, CancellationToken ct) =>
{
    var verbindung = await suche.FindenAsync(ct);
    if (!verbindung.Erreichbar) return Results.Ok(Array.Empty<GrowKurz>());

    return Results.Ok(await growOs.GrowsAsync(verbindung.Basis!, ct));
});

app.MapPost("/api/frage", async (
    FrageAnfrage anfrage,
    GrowOsDiscovery suche,
    GrowOsClient growOs,
    ModellClient modell,
    CancellationToken ct) =>
{
    var verbindung = await suche.FindenAsync(ct);
    if (!verbindung.Erreichbar)
    {
        return Results.Ok(new { antwort = verbindung.Meldung });
    }

    var wissen = await growOs.WissenAsync(verbindung.Basis!, anfrage.GrowId, ct);

    // Anweisung, Lage und Wissen kommen aus Grow OS — dieselbe Quelle wie die
    // Berater-Mappe zum Herunterladen. So koennen die beiden nie auseinander-
    // laufen.
    var anweisung = $"{wissen.Anweisung}\n\n# Lagebericht\n\n{wissen.Lagebericht}\n\n# Wissen\n\n{wissen.Wissen}";
    var verlauf = anfrage.Verlauf.Select(zug => new Zug(zug.Rolle, zug.Text)).ToList();

    return Results.Ok(new { antwort = await modell.AntwortAsync(anweisung, verlauf, ct) });
});

app.Run();

/// <summary>Was die Chat-Seite schickt.</summary>
public sealed record FrageAnfrage(int GrowId, List<ZugAnfrage> Verlauf);

/// <summary>Eine Zeile des bisherigen Gespraechs.</summary>
public sealed record ZugAnfrage(string Rolle, string Text);
