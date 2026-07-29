namespace GrowMcp;

/// <summary>Was mit einer Anfrage geschehen soll.</summary>
public enum Zutritt
{
    /// <summary>Durchlassen.</summary>
    Erlaubt,

    /// <summary>Diesen Weg gibt es an dieser Tür nicht — 404.</summary>
    NichtGefunden,

    /// <summary>Richtige Tür, falscher oder fehlender Schlüssel — 401.</summary>
    SchluesselFehlt,
}

/// <summary>
/// Welcher Port was darf.
/// </summary>
/// <remarks>
/// <para>An einer Stelle, weil die Trennung die eigentliche Absicherung ist: die
/// Seite mit dem Schlüssel hängt am Ingress-Port, die Schnittstelle am Netz-Port.
/// Wer hier etwas ändert, ändert, wer was sehen darf — deshalb ist die
/// Entscheidung eine eigene Funktion mit eigenen Tests und keine Verzweigung
/// mitten in der Middleware.</para>
/// </remarks>
public static class Tueren
{
    /// <summary>Home Assistant reicht die Einrichtungsseite hierüber durch.</summary>
    public const int IngressPort = 5078;

    /// <summary>Der einzige Port, der ins Heimnetz veröffentlicht wird.</summary>
    public const int NetzPort = 5079;

    /// <summary>Wo die MCP-Schnittstelle liegt.</summary>
    public const string McpPfad = "/mcp";

    /// <summary>Darf diese Anfrage weiter?</summary>
    /// <param name="port">Der Port, auf dem sie hereinkam.</param>
    /// <param name="pfad">Der angefragte Pfad.</param>
    /// <param name="schluesselStimmt">Hat der Aufrufer den richtigen Schlüssel mitgeschickt?</param>
    public static Zutritt Pruefen(int port, string pfad, bool schluesselStimmt)
    {
        var amNetz = port == NetzPort;
        var zurSchnittstelle = pfad.StartsWith(McpPfad, StringComparison.OrdinalIgnoreCase)
            && (pfad.Length == McpPfad.Length || pfad[McpPfad.Length] == '/');

        // Jede Tür hat genau eine Aufgabe. Am Netz-Port gibt es nur die
        // Schnittstelle — sonst könnte jeder im WLAN die Seite mit dem Schlüssel
        // aufrufen und sich den Schlüssel einfach abholen. Am Ingress-Port gibt es
        // nur die Seite.
        if (amNetz != zurSchnittstelle) return Zutritt.NichtGefunden;

        return zurSchnittstelle && !schluesselStimmt ? Zutritt.SchluesselFehlt : Zutritt.Erlaubt;
    }
}
