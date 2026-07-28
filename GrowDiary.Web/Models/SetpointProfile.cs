namespace GrowDiary.Web.Models;

/// <summary>
/// Ein eigenes Sollwert-Profil: die Erfahrungswerte des Nutzers.
/// </summary>
/// <remarks>
/// Gespeichert wird nur, was er WIRKLICH geändert hat — nicht der ganze
/// Wertesatz. Wer bloß den pH in der Blüte anpasst, bekommt weiterhin jede
/// spätere Verbesserung an EC, VPD und allem anderen aus der Wissensbasis. Eine
/// Vollkopie hätte ihn beim ersten Speichern von allen Updates abgeschnitten,
/// ohne dass er es gemerkt hätte.
/// </remarks>
public sealed class SetpointProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Worauf es aufbaut — „rdwc-default" oder „dwc-default".</summary>
    public string BaseProfileId { get; set; } = "rdwc-default";

    /// <summary>
    /// Nur die abweichenden Werte, nach Phase und Feld.
    /// Beispiel: <c>{"Veg":{"phMin":5.8,"phMax":6.0}}</c>
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> Overrides { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Wie viele Werte der Nutzer angefasst hat — für die Übersicht.</summary>
    public int ChangedValueCount => Overrides.Values.Sum(stage => stage.Count);

    /// <summary>Die Kennung, unter der Grows und Systeme auf dieses Profil zeigen.</summary>
    public string ReferenceId => Reference(Id);

    /// <summary>„custom:7" — so unterscheidbar von „rdwc-default".</summary>
    public static string Reference(int id) => $"{Prefix}{id}";

    public const string Prefix = "custom:";

    /// <summary>Liest die Id aus „custom:7"; null, wenn es kein eigenes Profil ist.</summary>
    public static int? IdFromReference(string? reference)
        => reference is not null && reference.StartsWith(Prefix, StringComparison.Ordinal)
           && int.TryParse(reference[Prefix.Length..], out var id)
            ? id
            : null;

    /// <summary>Die Felder, die ein Profil je Phase kennt — Reihenfolge wie in der Tabelle.</summary>
    public static readonly IReadOnlyList<string> Fields =
    [
        "phMin", "phMax",
        "ecMin", "ecMax",
        "orpMin", "orpMax",
        "waterTempDayC", "waterTempNightC",
        "vpdMin", "vpdMax",
        "ppfdMin", "ppfdMax",
        "co2Min", "co2Max",
    ];
}
