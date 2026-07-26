namespace GrowDiary.Web.Models;

public sealed class HarvestEntry
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public DateTime HarvestedAt { get; set; } = DateTime.Today;
    public double? WetWeightG { get; set; }
    public double? DryWeightG { get; set; }
    public int? DryDays { get; set; }
    public string? YieldNotes { get; set; }
    public double? Rating { get; set; }
    public string? FlavorNotes { get; set; }
    public string? EffectNotes { get; set; }
    public string? NugStructure { get; set; }

    /// <summary>
    /// Einzelgewichte je Pflanze als JSON: <c>[{"label":"PL-01","wetG":486,"dryG":null}]</c>.
    /// Die Summe steht in <see cref="WetWeightG"/> und <see cref="DryWeightG"/> —
    /// die bleiben die Wahrheit fuer Auswertungen, damit aeltere Ernten ohne
    /// Aufschluesselung weiter zaehlen.
    /// </summary>
    public string? PlantWeightsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
