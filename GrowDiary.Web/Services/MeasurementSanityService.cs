using GrowDiary.Web.Models;
using GrowDiary.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrowDiary.Web.Services;

public sealed class MeasurementSanityService
{
    public IReadOnlyList<RecommendationCard> GetSanityCards(GrowRun grow, Measurement measurement)
    {
        var cards = new List<RecommendationCard>();
        var profile = grow.Profile;

        CheckHumidity(cards, measurement.HumidityPercent);
        CheckAirTemperature(cards, measurement.AirTemperatureC);
        CheckHeight(cards, measurement.HeightCm);
        CheckWaterAndRunoff(cards, measurement.WaterAmountMl, measurement.RunoffAmountMl);

        if (profile.IsHydro)
        {
            CheckHydro(cards, measurement);
        }
        else
        {
            CheckSubstrate(cards, grow, measurement);
        }

        return cards;
    }

    public void ApplyBlockingValidation(ModelStateDictionary modelState, GrowRun grow, Measurement measurement)
    {
        ValidatePh(modelState, nameof(MeasurementFormViewModel.IrrigationPh), measurement.IrrigationPh, "Gießwasser-pH");
        ValidatePh(modelState, nameof(MeasurementFormViewModel.DrainPh), measurement.DrainPh, "Drain-pH");
        ValidatePh(modelState, nameof(MeasurementFormViewModel.ReservoirPh), measurement.ReservoirPh, "Reservoir-pH");

        ValidateRange(modelState, nameof(MeasurementFormViewModel.HumidityPercent), measurement.HumidityPercent, 0, 100, "Luftfeuchtigkeit");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.HeightCm), measurement.HeightCm, "Höhe");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.WaterAmountMl), measurement.WaterAmountMl, "Gießmenge");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.RunoffAmountMl), measurement.RunoffAmountMl, "Runoff");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.IrrigationEc), measurement.IrrigationEc, "Gießwasser-EC");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.DrainEc), measurement.DrainEc, "Drain-EC");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.ReservoirEc), measurement.ReservoirEc, "Reservoir-EC");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.ReservoirLevelCm), measurement.ReservoirLevelCm, "Wasserstand cm");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.ReservoirLevelLiters), measurement.ReservoirLevelLiters, "Wasserstand Liter");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.TopOffLiters), measurement.TopOffLiters, "Top-Off Liter");
        ValidateNonNegative(modelState, nameof(MeasurementFormViewModel.AddbackEc), measurement.AddbackEc, "Addback-EC");

        if (measurement.DissolvedOxygenMgL is < 0 or > 20)
        {
            modelState.AddModelError(nameof(MeasurementFormViewModel.DissolvedOxygenMgL), "Der Sauerstoffwert wirkt physikalisch unplausibel. Bitte Messgerät oder Einheit prüfen.");
        }

        if (measurement.OrpMv is < -1000 or > 1000)
        {
            modelState.AddModelError(nameof(MeasurementFormViewModel.OrpMv), "Der ORP-Wert wirkt unplausibel. Bitte Sensor oder Einheit prüfen.");
        }

        if (measurement.RunoffAmountMl is { } runoff && measurement.WaterAmountMl is { } water && runoff > water)
        {
            modelState.AddModelError(nameof(MeasurementFormViewModel.RunoffAmountMl), "Runoff kann normalerweise nicht höher als die eingetragene Gießmenge sein. Bitte Eingabe prüfen.");
        }
    }

    private static void CheckHydro(List<RecommendationCard> cards, Measurement measurement)
    {
        CheckPhBand(
            cards,
            measurement.ReservoirPh,
            criticalLow: 4.5,
            warnLow: 5.5,
            targetLow: 5.8,
            targetHigh: 6.2,
            warnHigh: 6.5,
            criticalHigh: 7.2,
            titlePrefix: "Reservoir-pH",
            criticalMessage: "Ein so extremer Reservoir-pH ist in RDWC/DWC hochriskant oder ein Messfehler. Nährstoffaufnahme und Wurzeln sofort prüfen.",
            warningMessage: "Der Reservoir-pH liegt außerhalb des üblichen Arbeitsfensters. In RDWC wird meist 5,8–6,2 gefahren und erst unter 5,5 bzw. über 6,5 korrigiert.");

        if (measurement.ReservoirEc is { } ec)
        {
            if (ec >= 3.2)
            {
                cards.Add(Critical("Reservoir-EC extrem hoch", $"Mit {ec:0.00} EC ist die Lösung für rezirkulierendes Hydro sehr aggressiv. Prüfe, ob ein Messfehler, ein falsches Ziel oder akuter Salzstress vorliegt."));
            }
            else if (ec >= 2.6)
            {
                cards.Add(Warning("Reservoir-EC sehr hoch", $"Mit {ec:0.00} EC fährst du bereits deutlich aggressiv. Für RDWC ist das meist nur in Ausnahmefällen sinnvoll."));
            }
        }

        if (measurement.ReservoirWaterTempC is { } waterTemp)
        {
            if (waterTemp >= 26.0)
            {
                cards.Add(Critical("Reservoir viel zu warm", $"Bei {waterTemp:0.0} °C sinkt der Sauerstoffgehalt stark. Das ist ein akutes Wurzelrisiko."));
            }
            else if (waterTemp >= 24.0)
            {
                cards.Add(Critical("Reservoir kritisch warm", $"Bei {waterTemp:0.0} °C wird RDWC schnell instabil. Kühlung und Belüftung sofort prüfen."));
            }
            else if (waterTemp >= 22.0)
            {
                cards.Add(Warning("Reservoir warm", $"Bei {waterTemp:0.0} °C wird Sauerstoff knapper. Beobachte DO, ORP und Wurzelgeruch eng."));
            }
            else if (waterTemp < 16.0)
            {
                cards.Add(Warning("Reservoir ungewöhnlich kalt", $"Bei {waterTemp:0.0} °C kann die Aufnahme träger werden. Prüfe, ob das bewusst so gefahren wird."));
            }
        }

        if (measurement.DissolvedOxygenMgL is { } doMgL)
        {
            if (doMgL < 6.0)
            {
                cards.Add(Critical("DO sehr niedrig", $"Mit {doMgL:0.0} mg/L liegt der Sauerstoff klar unter dem oft genannten Mindestfenster. Belüftung, Luftsteine und Wassertemperatur sofort prüfen."));
            }
            else if (doMgL < 7.0)
            {
                cards.Add(Warning("DO unter Ziel", $"Mit {doMgL:0.0} mg/L bist du unter dem oft genannten Zielwert von mindestens 7 mg/L."));
            }
        }

        if (measurement.TopOffLiters is { } topOff && topOff > 0 && measurement.AddbackEc is null)
        {
            cards.Add(Info("Top-Off ohne Addback-EC", "Du hast Top-Off eingetragen, aber keinen Addback-EC. Für saubere RDWC-Entscheidungen ist genau diese Kombination sehr wertvoll."));
        }
    }

    private static void CheckSubstrate(List<RecommendationCard> cards, GrowRun grow, Measurement measurement)
    {
        var profile = grow.Profile;

        if (profile.IsSoilOrganic)
        {
            CheckPhBand(
                cards,
                measurement.IrrigationPh,
                criticalLow: 4.8,
                warnLow: 5.6,
                targetLow: 6.0,
                targetHigh: 6.8,
                warnHigh: 7.2,
                criticalHigh: 8.0,
                titlePrefix: "Gießwasser-pH",
                criticalMessage: "Ein so extremer pH passt kaum zu einem organischen Soil-Run und kann das Bodenleben sowie die Verfügbarkeit stark stören.",
                warningMessage: "Für organischen Soil liegt ein grob plausibler Bereich eher um 6,0–6,8. Deutliche Abweichungen nur bewusst und nachvollziehbar fahren.");

            CheckPhBand(
                cards,
                measurement.DrainPh,
                criticalLow: 4.5,
                warnLow: 5.0,
                targetLow: 5.4,
                targetHigh: 6.8,
                warnHigh: 7.3,
                criticalHigh: 8.0,
                titlePrefix: "Drain-pH",
                criticalMessage: "Ein Drain-pH in diesem Bereich ist biologisch sehr auffällig. pH 3 wäre praktisch ein Alarmwert oder ein klarer Messfehler.",
                warningMessage: "Der Drain-pH liegt klar außerhalb dessen, was für ein organisches Substrat noch ruhig wirkt. Substrat, Wasser und Messgerät prüfen.");

            if (measurement.IrrigationEc is { } orgEc)
            {
                cards.Add(Info("Input-EC im organischen Soil nur Nebenwert", $"Du hast {orgEc:0.00} als Gießwasser-EC eingetragen. In organischem Soil ist das eher ein Zusatzindikator als ein Führungswert."));
            }
        }
        else
        {
            CheckPhBand(
                cards,
                measurement.IrrigationPh,
                criticalLow: 4.8,
                warnLow: 5.3,
                targetLow: 5.6,
                targetHigh: 6.3,
                warnHigh: 6.6,
                criticalHigh: 7.3,
                titlePrefix: "Input-pH",
                criticalMessage: "Ein so extremer Input-pH ist für Coco/mineralische Medien hochriskant oder ein Messfehler.",
                warningMessage: "Für Coco und mineralische Medien liegt ein plausibler Bereich meist deutlich enger als in Erde.");

            CheckPhBand(
                cards,
                measurement.DrainPh,
                criticalLow: 4.5,
                warnLow: 5.0,
                targetLow: 5.4,
                targetHigh: 6.5,
                warnHigh: 6.9,
                criticalHigh: 7.5,
                titlePrefix: "Drain-pH",
                criticalMessage: "Ein Drain-pH in diesem Bereich ist in Coco/mineralischen Medien sehr auffällig und kann auf heftigen Lockout oder Messfehler hindeuten.",
                warningMessage: "Der Drain-pH liegt klar außerhalb eines ruhigen Fensters. Verlauf und Salzaufbau prüfen.");

            if (measurement.IrrigationEc is { } inputEc)
            {
                if (inputEc >= 4.0)
                {
                    cards.Add(Critical("Input-EC extrem hoch", $"Mit {inputEc:0.00} EC ist die Nährlösung für Coco/mineralische Medien sehr aggressiv. Das wäre nur in Ausnahmefällen plausibel."));
                }
                else if (inputEc >= 3.0)
                {
                    cards.Add(Warning("Input-EC hoch", $"Mit {inputEc:0.00} EC liegst du bereits in einem Bereich, der sehr bewusst gefahren werden sollte."));
                }
            }

            if (measurement.DrainEc is { } drainEc)
            {
                if (drainEc >= 4.5)
                {
                    cards.Add(Critical("Drain-EC extrem hoch", $"Mit {drainEc:0.00} EC im Drain deutet vieles auf massive Anreicherung oder einen Messfehler hin."));
                }
                else if (drainEc >= 3.5)
                {
                    cards.Add(Warning("Drain-EC sehr hoch", $"Mit {drainEc:0.00} EC im Drain liegt eine starke Anreicherung nahe."));
                }
            }
        }

        if (measurement.DrainPh is { } drainPh && measurement.IrrigationPh is { } inputPh)
        {
            var delta = Math.Abs(drainPh - inputPh);
            if (delta >= 1.5)
            {
                cards.Add(Critical("pH-Sprung zwischen Input und Drain sehr groß", $"Zwischen Input ({inputPh:0.00}) und Drain ({drainPh:0.00}) liegen {delta:0.00} pH-Punkte. Das ist biologisch auffällig und oft ein Zeichen für Messfehler, starken Salzstress oder ein aus dem Ruder gelaufenes Medium."));
            }
            else if (delta >= 0.8)
            {
                cards.Add(Warning("pH-Sprung zwischen Input und Drain deutlich", $"Zwischen Input ({inputPh:0.00}) und Drain ({drainPh:0.00}) liegen {delta:0.00} pH-Punkte. Verlauf und Medium prüfen."));
            }
        }

        if ((measurement.DrainEc is not null || measurement.DrainPh is not null) && measurement.RunoffAmountMl is null)
        {
            cards.Add(Info("Drain ohne Runoff-Menge", "Drain-Werte sind deutlich aussagekräftiger, wenn du auch die ungefähre Runoff-Menge dokumentierst."));
        }
    }

    private static void CheckHumidity(List<RecommendationCard> cards, double? humidityPercent)
    {
        if (humidityPercent is not { } rh)
        {
            return;
        }

        if (rh < 20)
        {
            cards.Add(Warning("Luftfeuchtigkeit sehr niedrig", $"Mit {rh:0} % ist die Luft sehr trocken. Das erhöht Verdunstungsstress deutlich."));
        }
        else if (rh < 35)
        {
            cards.Add(Info("Luftfeuchtigkeit niedrig", $"Die Luftfeuchtigkeit liegt bei {rh:0} %. Beobachte Transpiration und Spitzenstress."));
        }
        else if (rh > 85)
        {
            cards.Add(Warning("Luftfeuchtigkeit sehr hoch", $"Mit {rh:0} % ist die Luft sehr feucht. Schimmel- und Transpirationsprobleme werden wahrscheinlicher."));
        }
    }

    private static void CheckAirTemperature(List<RecommendationCard> cards, double? airTemperatureC)
    {
        if (airTemperatureC is not { } temp)
        {
            return;
        }

        if (temp >= 35)
        {
            cards.Add(Critical("Lufttemperatur kritisch hoch", $"Mit {temp:0.0} °C ist deutlicher Hitzestress wahrscheinlich."));
        }
        else if (temp >= 31)
        {
            cards.Add(Warning("Lufttemperatur hoch", $"Mit {temp:0.0} °C bist du klar im Hitzestress-Risiko."));
        }
        else if (temp > 30)
        {
            cards.Add(Warning("Lufttemperatur hoch", $"Die Lufttemperatur liegt bei {temp:0.0} °C. Prüfe Lampenabstand, Abluft und VPD."));
        }
        else if (temp < 14)
        {
            cards.Add(Warning("Lufttemperatur sehr niedrig", $"Mit {temp:0.0} °C wird das Wachstum deutlich träger."));
        }
    }

    private static void CheckHeight(List<RecommendationCard> cards, double? heightCm)
    {
        if (heightCm is { } height && height > 400)
        {
            cards.Add(Info("Höhe ungewöhnlich groß", $"Mit {height:0.0} cm ist die eingetragene Höhe sehr hoch. Prüfe Einheit und Eingabe, falls das nicht bewusst so gemeint war."));
        }
    }

    private static void CheckWaterAndRunoff(List<RecommendationCard> cards, double? waterAmountMl, double? runoffAmountMl)
    {
        if (waterAmountMl is { } water && water > 0 && water < 50)
        {
            cards.Add(Info("Sehr kleine Gießmenge", $"Mit {water:0} ml ist die Gießmenge sehr klein. Das kann in sehr frühen Stadien okay sein, sollte aber zur Phase passen."));
        }

        if (runoffAmountMl is { } runoff && waterAmountMl is { } givenWater && runoff > givenWater)
        {
            cards.Add(Critical("Runoff größer als Gießmenge", "Die dokumentierte Runoff-Menge ist höher als die eingetragene Gießmenge. Das spricht fast sicher für einen Eingabefehler."));
        }
    }

    private static void CheckPhBand(
        List<RecommendationCard> cards,
        double? value,
        double criticalLow,
        double warnLow,
        double targetLow,
        double targetHigh,
        double warnHigh,
        double criticalHigh,
        string titlePrefix,
        string criticalMessage,
        string warningMessage)
    {
        if (value is not { } ph)
        {
            return;
        }

        if (ph <= criticalLow || ph >= criticalHigh)
        {
            cards.Add(Critical($"{titlePrefix} extrem", $"{titlePrefix} liegt bei {ph:0.00}. {criticalMessage}"));
        }
        else if (ph < warnLow || ph > warnHigh)
        {
            cards.Add(Warning($"{titlePrefix} klar außerhalb des ruhigen Bereichs", $"{titlePrefix} liegt bei {ph:0.00}. {warningMessage}"));
        }
        else if (ph < targetLow || ph > targetHigh)
        {
            cards.Add(Info($"{titlePrefix} außerhalb des Sweet Spots", $"{titlePrefix} liegt bei {ph:0.00}. Das ist noch nicht zwingend kritisch, aber nicht mehr im engeren Wohlfühlfenster."));
        }
    }

    private static void ValidatePh(ModelStateDictionary modelState, string fieldName, double? value, string label)
    {
        if (value is < 0 or > 14)
        {
            modelState.AddModelError(fieldName, $"{label} muss zwischen 0 und 14 liegen.");
        }
    }

    private static void ValidateRange(ModelStateDictionary modelState, string fieldName, double? value, double min, double max, string label)
    {
        if (value is null)
        {
            return;
        }

        if (value < min || value > max)
        {
            modelState.AddModelError(fieldName, $"{label} muss zwischen {min:0} und {max:0} liegen.");
        }
    }

    private static void ValidateNonNegative(ModelStateDictionary modelState, string fieldName, double? value, string label)
    {
        if (value is < 0)
        {
            modelState.AddModelError(fieldName, $"{label} darf nicht negativ sein.");
        }
    }

    private static RecommendationCard Info(string title, string message)
        => new() { Severity = "info", Title = title, Message = message };

    private static RecommendationCard Warning(string title, string message)
        => new() { Severity = "warning", Title = title, Message = message };

    private static RecommendationCard Critical(string title, string message)
        => new() { Severity = "danger", Title = title, Message = message };
}
