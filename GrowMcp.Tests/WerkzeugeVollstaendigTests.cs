using System.Net;
using System.Reflection;
using System.Text;
using GrowMcp.Tools;
using GrowOsAccess;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace GrowMcp.Tests;

/// <summary>
/// Jedes MCP-Werkzeug wird mindestens einmal ausgeführt.
///
/// <para><b>Der Anlass.</b> Der Server bietet 22 Werkzeuge an, und <b>keines
/// davon</b> wurde je von einem Test aufgerufen — <c>GrowOsReader</c> kam in
/// keiner der sieben Testdateien vor. Getestet waren die Türen, der
/// Token-Speicher und das Zusammenlegen von Listen; das, was der Nutzer
/// tatsächlich benutzt, nicht.</para>
///
/// <para><b>Was diese Zählung fängt.</b> Ein Werkzeug, das bei einem
/// Standardargument sofort abstürzt. Ein Pfad, der nicht mehr existiert. Ein
/// neues Werkzeug, das jemand hinzufügt und nie ausprobiert. Sie ist keine
/// Prüfung der Inhalte — die hängen an der laufenden App — sondern die
/// Zusicherung, dass jedes Werkzeug überhaupt läuft und antwortet.</para>
///
/// <para><b>Der Aufbau.</b> Ein Stub-Server statt Home Assistant: die Adresse
/// wird von Hand gesetzt (<see cref="GrowOsOptions.Adresse"/>), und ein
/// eigener <see cref="HttpMessageHandler"/> beantwortet jeden Pfad mit einem
/// leeren JSON-Rumpf und merkt sich, wonach gefragt wurde.</para>
/// </summary>
public sealed class WerkzeugeVollstaendigTests
{
    /// <summary>Beantwortet jede Anfrage und merkt sich den Pfad.</summary>
    private sealed class MitschreibenderHandler : HttpMessageHandler
    {
        public List<string> Pfade { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pfade.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class EinKlient(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://127.0.0.1:9/"),
        };
    }

    private static (GrowTools Werkzeuge, MitschreibenderHandler Handler) Aufbauen()
    {
        var handler = new MitschreibenderHandler();
        var fabrik = new EinKlient(handler);
        var optionen = new GrowOsOptions { Adresse = "http://127.0.0.1:9" };

        var discovery = new GrowOsDiscovery(
            fabrik,
            new SupervisorClient(fabrik, NullLogger<SupervisorClient>.Instance),
            optionen,
            NullLogger<GrowOsDiscovery>.Instance);

        var reader = new GrowOsReader(fabrik.CreateClient("test"), discovery);
        return (new GrowTools(reader), handler);
    }

    /// <summary>Alle Methoden, die als Werkzeug angeboten werden.</summary>
    private static MethodInfo[] Werkzeugmethoden()
        => typeof(GrowTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

    public static IEnumerable<object[]> Werkzeuge()
        => Werkzeugmethoden().Select(m => new object[] { m.Name });

    [Fact]
    public void Der_Test_sieht_die_Werkzeuge()
    {
        // Sonst laeuft die Schleife null Mal und der Test ist gruen, ohne etwas
        // geprueft zu haben — die Falle, in die seine Vorgaenger gelaufen sind.
        Assert.True(Werkzeugmethoden().Length >= 20,
            $"Nur {Werkzeugmethoden().Length} Werkzeuge gefunden — die Reflexion greift ins Leere.");
    }

    [Theory]
    [MemberData(nameof(Werkzeuge))]
    public async Task Jedes_Werkzeug_laesst_sich_ausfuehren_und_antwortet(string methodenName)
    {
        var (werkzeuge, _) = Aufbauen();
        var methode = Werkzeugmethoden().Single(m => m.Name == methodenName);

        // Pflichtargumente mit etwas Plausiblem fuellen: Ids mit 1, Texte mit
        // einem Wort. Optionale bleiben auf ihrem Standard — genau so ruft ein
        // Klient das Werkzeug beim ersten Mal auf.
        var argumente = methode.GetParameters().Select(StandardWert).ToArray();

        var ergebnis = methode.Invoke(werkzeuge, argumente);
        Assert.NotNull(ergebnis);

        // NICHT alle Werkzeuge geben Text zurueck: `foto_ansehen` liefert ein
        // Bild als ContentBlock-Folge. Ein erster Anlauf dieses Tests hat
        // stumpf auf Task<string> gecastet und ausgerechnet daran gescheitert —
        // an einem Werkzeug, das voellig in Ordnung ist.
        var aufgabe = (Task)ergebnis!;
        await aufgabe;

        var wert = aufgabe.GetType().GetProperty("Result")!.GetValue(aufgabe);
        Assert.NotNull(wert);

        // Der Inhalt haengt an echten Daten und wird hier nicht geprueft. Was
        // geprueft wird: das Werkzeug laeuft durch und sagt etwas.
        if (wert is string text)
        {
            Assert.False(string.IsNullOrWhiteSpace(text), $"{methodenName} antwortet mit nichts.");
        }
    }

    [Fact]
    public async Task Die_Werkzeuge_fragen_Grow_OS_wirklich()
    {
        // Gegenprobe: liefe der Aufbau ins Leere, wuerde jedes Werkzeug nur eine
        // Fehlermeldung zurueckgeben und der Test darueber waere trotzdem gruen.
        var (werkzeuge, handler) = Aufbauen();
        await werkzeuge.GrowsAuflistenAsync(cancellationToken: CancellationToken.None);

        Assert.NotEmpty(handler.Pfade);
        Assert.Contains(handler.Pfade, p => p.Contains("api/grows", StringComparison.Ordinal));
    }

    private static object? StandardWert(ParameterInfo p)
    {
        if (p.HasDefaultValue) return p.DefaultValue;
        if (p.ParameterType == typeof(CancellationToken)) return CancellationToken.None;
        if (p.ParameterType == typeof(int)) return 1;
        if (p.ParameterType == typeof(string)) return "test";
        if (p.ParameterType == typeof(bool)) return false;
        return p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
    }
}
