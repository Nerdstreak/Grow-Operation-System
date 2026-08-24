namespace GrowDiary.Web.Models;

public sealed class PlantInstance
{
    public int Id { get; set; }
    public int? StrainId { get; set; }
    public int? SetupId { get; set; }
    public int? GrowId { get; set; }

    /// <summary>
    /// Der Topf im Hydro-System, ab 1 — dieselbe Nummer, die die Draufsicht
    /// (SystemPlan) an die Sites zeichnet.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass:</b> ein Nutzer fährt im RDWC je Topf eine eigene
    /// Sorte und konnte nur sagen WELCHE Sorten im Grow sind, nicht WO. Die
    /// Sorte je Pflanze gab es längst; der Ort fehlte.</para>
    ///
    /// <para><b>Bewusst nur eine Nummer</b>, kein eigenes Topf-Modell und keine
    /// Koordinaten: die Draufsicht nummeriert ihre Sites schon heute
    /// deterministisch (1..n, zeilenweise) rein aus der Geometrie. Eine Zahl,
    /// die auf diese Zählung zeigt, reicht — alles Weitere wäre ein zweites
    /// Modell für dieselbe Wahrheit.</para>
    /// </remarks>
    public int? SiteIndex { get; set; }

    public int? ParentPlantId { get; set; }
    public string Label { get; set; } = string.Empty;
    public PlantRole PlantRole { get; set; } = PlantRole.Production;
    public PlantStatus PlantStatus { get; set; } = PlantStatus.Planned;
    public string? PhenoLabel { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Notes { get; set; }
    public string? StrainName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
