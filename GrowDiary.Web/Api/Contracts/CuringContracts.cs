using System.ComponentModel.DataAnnotations;

namespace GrowDiary.Web.Api.Contracts;

/// <summary>Ein Glas mit allem, was die Oberfläche darüber zeigt.</summary>
/// <param name="Duty">
/// Was jetzt ansteht. Gerechnet, nicht gespeichert — deshalb steht es am DTO
/// und nicht am Modell.
/// </param>
/// <param name="LatestHumidity">
/// Die jüngste Feuchte-Ablesung. <c>null</c> heißt „noch nie gemessen" und wird
/// in der Oberfläche auch so benannt — nicht als 0 %.
/// </param>
public sealed record CuringJarDto(
    int Id,
    int GrowId,
    string GrowName,
    string Label,
    int? StrainId,
    string? StrainName,
    DateTime FilledAtUtc,
    double? WeightG,
    bool HasHumidityPack,
    DateTime? FinishedAtUtc,
    string? Notes,
    CuringDutyDto Duty,
    CuringHumidityDto? LatestHumidity);

/// <summary>Der nächste Lüft-Termin und was dabei zu tun ist.</summary>
public sealed record CuringDutyDto(
    string Level,
    int DayInCure,
    int IntervalDays,
    int BurpMinutesMin,
    int BurpMinutesMax,
    DateTime? NextDueUtc,
    string Text,
    string Source);

/// <summary>Ein bewerteter Feuchtewert mit Alter und Herkunft.</summary>
public sealed record CuringHumidityDto(
    double Percent,
    DateTime ReadAtUtc,
    string Source,
    string Level,
    string Summary,
    string Action,
    string RatingSource);

public sealed record CuringReadingDto(
    int Id,
    int JarId,
    DateTime ReadAtUtc,
    double? HumidityPercent,
    int? BurpedMinutes,
    string? Note,
    string Source);

public sealed class CuringJarUpsertRequest
{
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;

    public int? StrainId { get; set; }

    /// <summary>Wann eingeglast wurde, als Ortszeit-Datum („2026-08-17").</summary>
    [Required]
    public string FilledAtLocal { get; set; } = string.Empty;

    [Range(0, 100000)]
    public double? WeightG { get; set; }

    public bool HasHumidityPack { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Eine Ablesung. Beide Werte sind einzeln erlaubt — aber nicht beide leer.
/// </summary>
public sealed class CuringReadingRequest
{
    [Range(0, 100)]
    public double? HumidityPercent { get; set; }

    /// <summary>
    /// Wie lange gelüftet wurde. Mindestens eine Minute — „0 Minuten gelüftet"
    /// hat den nächsten Termin zurückgesetzt, ohne dass ein Glas offen war.
    /// Genau das soll die Trennung von Ablesen und Lüften verhindern.
    /// </summary>
    [Range(1, 600)]
    public int? BurpedMinutes { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
