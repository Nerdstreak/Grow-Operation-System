using GrowDiary.Web.Services;

namespace GrowDiary.Web.ViewModels;

public sealed class AddbackViewModel
{
    public int? GrowId { get; set; }
    public string? GrowName { get; set; }

    // Eingaben
    public double? ReservoirLiters { get; set; }
    public double? EcIst { get; set; }
    public double? EcZiel { get; set; }
    public double? EcStock { get; set; } = 3.0;

    // Ergebnis (null wenn noch nicht berechnet)
    public AddbackCalculator.AddbackResult? Result { get; set; }

    // Vorschlagswerte aus dem Grow
    public double? SuggestedReservoirLiters { get; set; }
    public double? SuggestedEcIst { get; set; }
    public double? SuggestedEcZiel { get; set; }
}
