using GrowAgent.Services;

// Der Grow-Berater als eigenes Home-Assistant-Add-on.
//
// Aufteilung, wie mit dem Betreiber abgesprochen: Grow OS bleibt ohne
// Schluessel und ohne KI. Dieses Add-on haelt den Modell-Schluessel, holt sich
// den Lagebericht und das Fachwissen ueber die Schnittstelle von Grow OS und
// fuehrt das Gespraech. Beide reden im internen HA-Netz miteinander; nach
// draussen ist kein Port offen.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<SupervisorClient>();
builder.Services.AddHttpClient<GrowOsClient>();
builder.Services.AddHttpClient<ModellClient>();
builder.Services.AddSingleton(sp =>
    AgentEinstellungen.Laden(sp.GetRequiredService<ILogger<AgentEinstellungen>>()));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

/// <summary>Grow OS suchen und einmal anklopfen.</summary>
async Task<(GrowOsFund Fund, bool Erreichbar)> VerbindungAsync(
    SupervisorClient supervisor, GrowOsClient growOs, AgentEinstellungen einstellungen, CancellationToken ct)
{
    // Von Hand eingetragen schlaegt Nachfragen: wer den Weg kennt, soll nicht
    // an einer fehlenden Berechtigung scheitern.
    if (!string.IsNullOrWhiteSpace(einstellungen.GrowOsAdresse))
    {
        var adresse = einstellungen.GrowOsAdresse.TrimEnd('/');
        var eigen = new GrowOsFund("eingetragen", adresse, $"Grow OS laut Einstellungen unter {adresse}.", true);
        return (eigen, await growOs.ErreichbarAsync(adresse, ct));
    }

    if (!SupervisorClient.ImAddon)
    {
        // Beim Entwickeln laeuft Grow OS auf dem eigenen Rechner.
        var lokal = new GrowOsFund("local_grow_os", "localhost", "Entwicklungsbetrieb: Grow OS auf localhost.", true);
        return (lokal, await growOs.ErreichbarAsync(GrowOsLocator.BaseUrl(lokal.Host!), ct));
    }

    // Der Name von Grow OS wird aus dem eigenen abgeleitet — beide kommen aus
    // demselben Repository. Danach wird angeklopft; wer antwortet, ist es. Das
    // spart die Add-on-Liste und damit die Manager-Rolle.
    var eigener = await supervisor.EigenerSlugAsync(ct);
    foreach (var kandidat in GrowOsLocator.Kandidaten(eigener))
    {
        var host = GrowOsLocator.Hostname(kandidat);
        if (await growOs.ErreichbarAsync(GrowOsLocator.BaseUrl(host), ct))
        {
            return (new GrowOsFund(kandidat, host, $"Grow OS gefunden unter {host}.", true), true);
        }
    }

    return (GrowOsLocator.NichtGefunden, false);
}

/// <summary>Die fertige Adresse — eine eingetragene bringt ihr Schema schon mit.</summary>
static string Basis(GrowOsFund fund)
    => fund.Host!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        ? fund.Host!
        : GrowOsLocator.BaseUrl(fund.Host!);

app.MapGet("/api/verbindung", async (SupervisorClient supervisor, GrowOsClient growOs, AgentEinstellungen einstellungen, CancellationToken ct) =>
{
    var (fund, erreichbar) = await VerbindungAsync(supervisor, growOs, einstellungen, ct);
    return Results.Ok(new
    {
        gefunden = fund.Gefunden,
        erreichbar,
        slug = fund.Slug,
        adresse = fund.Host is null ? null : Basis(fund),
        meldung = fund.Gefunden && !erreichbar
            ? $"{fund.Meldung} Die Schnittstelle antwortet aber nicht — laeuft das Add-on wirklich?"
            : fund.Meldung,
    });
});

app.MapGet("/api/grows", async (SupervisorClient supervisor, GrowOsClient growOs, AgentEinstellungen einstellungen, CancellationToken ct) =>
{
    var (fund, erreichbar) = await VerbindungAsync(supervisor, growOs, einstellungen, ct);
    if (!fund.Gefunden || !erreichbar) return Results.Ok(Array.Empty<GrowKurz>());

    return Results.Ok(await growOs.GrowsAsync(Basis(fund), ct));
});

app.MapPost("/api/frage", async (
    FrageAnfrage anfrage,
    SupervisorClient supervisor,
    GrowOsClient growOs,
    ModellClient modell,
    AgentEinstellungen einstellungen,
    CancellationToken ct) =>
{
    var (fund, erreichbar) = await VerbindungAsync(supervisor, growOs, einstellungen, ct);
    if (!fund.Gefunden || !erreichbar)
    {
        return Results.Ok(new { antwort = fund.Meldung });
    }

    var wissen = await growOs.WissenAsync(Basis(fund), anfrage.GrowId, ct);

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
