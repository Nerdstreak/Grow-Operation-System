namespace GrowDiary.Web.Models;

public sealed class MetricCard
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "-";
    public string? Unit { get; set; }
    public string Tone { get; set; } = "default";
    public string? Hint { get; set; }
    public string? Target { get; set; }   // z. B. "5.8-6.2" fuer Sollwert-Anzeige

    /// <summary>
    /// Der Wert als Zahl, zusaetzlich zum formatierten <see cref="Value"/>.
    /// Die Anzeige zeichnet daraus die Skala; aus "25,4 °C" laesst sich das nicht
    /// zurueckrechnen, ohne die Formatierung wieder aufzudroeseln.
    /// </summary>
    public double? NumericValue { get; set; }

    /// <summary>Zielbereich fuer diese Phase, null wo es keinen gibt (Licht, Fuellstand).</summary>
    public double? TargetMin { get; set; }
    public double? TargetMax { get; set; }
}
