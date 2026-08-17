namespace GrowDiary.Web.Models;

public sealed class PhotoAsset
{
    public int Id { get; set; }
    public int GrowId { get; set; }
    public int? MeasurementId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public PhotoTag Tag { get; set; } = PhotoTag.Overview;
    public ValueOrigin Source { get; set; } = ValueOrigin.Manual;
    public bool IsReferenceShot { get; set; }

    /// <summary>
    /// Das Symptom, das auf diesem Bild zu sehen ist — der Schlüssel aus der
    /// Wissensbasis, oder <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Damit wird die eigene Fotosammlung zum Nachschlagewerk: wer im Wissen
    /// „Braune, schleimige Wurzeln" liest, sieht darunter, wie das im eigenen
    /// Zelt aussah — und nicht ein Bild aus einer fremden Anlage mit fremdem
    /// Licht. Fremde Beispielbilder wären ohnehin nicht zu haben, ohne das
    /// Urheberrecht zu verletzen.
    /// </remarks>
    public string? SymptomId { get; set; }
    public DateTime TakenAtUtc { get; set; } = DateTime.UtcNow;
}
