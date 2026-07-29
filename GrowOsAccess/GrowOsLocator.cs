namespace GrowOsAccess;

/// <summary>Wo Grow OS steckt — oder warum es nicht gefunden wurde.</summary>
public sealed record GrowOsFund(string? Slug, string? Host, string Meldung, bool Gefunden);

/// <summary>
/// Grow OS im internen Add-on-Netz finden.
/// </summary>
/// <remarks>
/// <para>Der Hostname eines Add-ons setzt sich aus Repository und Slug zusammen
/// — <c>local_grow_os</c> bei einer lokalen Installation, sonst mit dem Hash des
/// Repositories davor, etwa <c>a1b2c3d4_grow_os</c>. Als DNS-Name werden die
/// Unterstriche zu Bindestrichen. Der Hash ist von aussen nicht vorhersagbar.</para>
///
/// <para>Geraten wird er trotzdem nicht — er wird abgeleitet: Berater und Grow OS
/// kommen aus demselben Repository und tragen deshalb denselben Vorsatz. Der
/// Berater fragt den Supervisor nach seinem <em>eigenen</em> Namen und tauscht den
/// hinteren Teil aus. Das genügt der Standardrolle. Die vollständige Add-on-Liste
/// verlangte <c>hassio_role: manager</c>, und die erlaubt nebenbei, jedes andere
/// Add-on zu starten, zu stoppen und zu entfernen — zu viel Recht für eine
/// Namensauskunft.</para>
///
/// <para>Reine Auswahllogik, ohne Netzwerk: was hineingegeben wird, bestimmt das
/// Ergebnis.</para>
/// </remarks>
public static class GrowOsLocator
{
    /// <summary>Der Slug von Grow OS, ohne den Repository-Teil davor.</summary>
    public const string Slug = "grow_os";

    /// <summary>Der eigene Slug, ohne den Repository-Teil davor.</summary>
    public const string AgentSlug = "grow_agent";

    /// <summary>Der Port, auf dem Grow OS im Container lauscht.</summary>
    public const int Port = 5076;

    /// <summary>
    /// Die Namen, unter denen Grow OS stecken kann — in der Reihenfolge, in der
    /// es sich lohnt, anzuklopfen.
    /// </summary>
    /// <remarks>
    /// Zuerst der abgeleitete Name: gleicher Store, gleicher Vorsatz, das ist der
    /// Normalfall. Danach die beiden Namen, die ohne jede Auskunft feststehen —
    /// wer Grow OS aus dem Ordner heraus installiert hat, findet es unter
    /// <c>local_grow_os</c>. Angeklopft wird nacheinander; wer nicht antwortet,
    /// fällt raus.
    /// </remarks>
    public static IReadOnlyList<string> Kandidaten(string? eigenerSlug)
    {
        var namen = new List<string>();

        if (!string.IsNullOrWhiteSpace(eigenerSlug) &&
            eigenerSlug.EndsWith(AgentSlug, StringComparison.Ordinal))
        {
            // "a1b2c3d4_grow_agent" -> "a1b2c3d4_" -> "a1b2c3d4_grow_os"
            namen.Add(string.Concat(eigenerSlug.AsSpan(0, eigenerSlug.Length - AgentSlug.Length), Slug));
        }

        namen.Add("local_" + Slug);
        namen.Add(Slug);

        return namen.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Die Meldung, wenn unter keinem Namen jemand antwortet.</summary>
    public static GrowOsFund NichtGefunden { get; } = new(
        null, null,
        "Grow OS ist von hier aus nicht erreichbar. Läuft das Add-on? " +
        "Sonst trag seine Adresse in den Einstellungen des Beraters ein.",
        false);

    /// <summary>Aus dem Slug den DNS-Namen im internen Netz machen.</summary>
    /// <remarks>Unterstriche sind in einem DNS-Namen nicht erlaubt.</remarks>
    public static string Hostname(string slug) => slug.Replace('_', '-');

    /// <summary>Die Adresse, unter der die Schnittstelle von Grow OS erreichbar ist.</summary>
    public static string BaseUrl(string host) => $"http://{host}:{Port}";
}
