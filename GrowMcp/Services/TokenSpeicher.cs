using System.Security.Cryptography;
using System.Text;

namespace GrowMcp.Services;

/// <summary>
/// Der Schlüssel für die Tür ins Heimnetz.
/// </summary>
/// <remarks>
/// <para>Beim ersten Start erzeugt, danach in <c>/data</c> liegend — dieselbe
/// Ablage, die Home Assistant sichert. Wer ihn neu haben will, löscht die Datei
/// und startet das Add-on neu.</para>
///
/// <para>Der Betreiber muss ihn nicht ausdenken. Ein Passwort, das jemand sich
/// selbst ausdenkt, ist kürzer und öfter schon woanders in Gebrauch.</para>
/// </remarks>
public sealed class TokenSpeicher
{
    private readonly byte[] _erwartet;

    public TokenSpeicher(ILogger<TokenSpeicher> logger)
    {
        var ordner = Directory.Exists("/data") ? "/data" : AppContext.BaseDirectory;
        var pfad = Path.Combine(ordner, "mcp-token");

        if (File.Exists(pfad) && File.ReadAllText(pfad).Trim() is { Length: > 0 } vorhanden)
        {
            Token = vorhanden;
        }
        else
        {
            Token = Base64Url(RandomNumberGenerator.GetBytes(32));
            File.WriteAllText(pfad, Token);
            logger.LogInformation("Ein neuer Zugriffsschluessel wurde erzeugt und in {Pfad} abgelegt.", pfad);
        }

        _erwartet = Encoding.UTF8.GetBytes(Token);
    }

    /// <summary>Der Schlüssel im Klartext — nur für die Ingress-Seite.</summary>
    public string Token { get; }

    /// <summary>Stimmt der mitgeschickte Schlüssel?</summary>
    /// <remarks>
    /// Zeitkonstanter Vergleich: ein <c>==</c> bricht beim ersten falschen Zeichen
    /// ab und verrät über die Antwortzeit, wie weit ein Rateversuch gekommen ist.
    /// </remarks>
    public bool Stimmt(string? mitgeschickt)
    {
        if (string.IsNullOrEmpty(mitgeschickt)) return false;

        var gegeben = Encoding.UTF8.GetBytes(mitgeschickt);
        return CryptographicOperations.FixedTimeEquals(gegeben, _erwartet);
    }

    /// <summary>Base64 ohne <c>+</c>, <c>/</c> und <c>=</c> — das übersteht jede Kommandozeile.</summary>
    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
