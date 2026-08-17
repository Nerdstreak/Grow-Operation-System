namespace GrowDiary.Web.Models;

/// <summary>
/// Ein Glas im Aushärten.
/// </summary>
/// <remarks>
/// <para>Bis hierher hat die App die Pflanze bis zum Trockengewicht begleitet und
/// dann aufgehört — ausgerechnet vor dem Schritt, der über die Qualität von
/// Monaten Arbeit entscheidet. Schlimmer noch: das Speichern der Ernte setzt den
/// Grow auf <see cref="GrowStatus.Completed"/>, also verschwand er aus der
/// Übersicht in genau dem Moment, in dem das Aushärten anfing.</para>
///
/// <para>Ein Glas, nicht ein Grow: bei mehreren Sorten im Zelt härtet jede für
/// sich aus, und auch eine einzelne Sorte füllt oft mehrere Gläser, die
/// unterschiedlich feucht sind. Deshalb hängt <see cref="StrainId"/> optional
/// am Glas — dieselbe Trennung wie bei den Pflanzen seit beta.41.</para>
/// </remarks>
public sealed class CuringJar
{
    public int Id { get; set; }

    public int GrowId { get; set; }

    /// <summary>„Glas 1", „Mimosa oben" — wie es am Glas steht.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Welche Sorte im Glas ist, falls der Grow mehrere trägt.</summary>
    public int? StrainId { get; set; }

    /// <summary>Wann eingeglast wurde — ab hier zählt das Aushärten.</summary>
    public DateTime FilledAtUtc { get; set; }

    /// <summary>Was hineinkam, in Gramm.</summary>
    public double? WeightG { get; set; }

    /// <summary>
    /// Ein Feuchtigkeitsregler (Boveda, Integra) liegt im Glas.
    /// </summary>
    /// <remarks>
    /// Ändert den Lüft-Rhythmus: der Regler hält die Feuchte selbst im Fenster,
    /// gelüftet wird dann nur noch, um die feuchte Luft der ersten Tage
    /// herauszulassen. Ohne ihn trägt das Lüften die ganze Regelung.
    /// </remarks>
    public bool HasHumidityPack { get; set; }

    /// <summary>
    /// Wann das Aushärten für dieses Glas beendet wurde. <c>null</c> = läuft noch.
    /// </summary>
    public DateTime? FinishedAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
