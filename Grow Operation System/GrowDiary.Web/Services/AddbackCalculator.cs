namespace GrowDiary.Web.Services;

public static class AddbackCalculator
{
    public sealed record AddbackResult(
        bool NeedsAddback,
        double? LitersToAdd,
        double? NewReservoirVolume,
        string? ErrorMessage
    );

    public static AddbackResult Calculate(
        double reservoirLiters,
        double ecIst,
        double ecZiel,
        double ecStock)
    {
        if (reservoirLiters <= 0)
            return new(false, null, null,
                "Reservoir-Volumen muss größer 0 sein.");

        if (ecIst >= ecZiel)
            return new(false, 0, reservoirLiters,
                null);

        if (ecStock <= ecZiel)
            return new(false, null, null,
                "Addback-EC muss höher sein als Ziel-EC.");

        var liters = reservoirLiters
            * (ecZiel - ecIst)
            / (ecStock - ecZiel);

        return new(true,
            Math.Round(liters, 2),
            Math.Round(reservoirLiters + liters, 1),
            null);
    }
}
