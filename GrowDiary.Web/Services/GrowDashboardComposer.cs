using System.Globalization;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Services;

public sealed class GrowDashboardComposer
{
    private readonly ChartService _chartService;
    private readonly DeviationAnalyzerService _deviationAnalyzer;
    private readonly WeekCounterService _weekCounter;
    private readonly TargetValueService _targetValues;
    private readonly AlertRuleRepository? _alertRules;
    private readonly HydroSetupRepository? _hydroSetups;
    private readonly SetpointProfileRepository? _setpointProfiles;
    private readonly LightRepository? _lights;
    private readonly GrowRepository? _grows;
    private readonly HarvestRepository? _harvests;
    private readonly LightCycleReader? _lightCycles;
    private readonly ILogger<GrowDashboardComposer> _logger;

    public GrowDashboardComposer(
        ChartService chartService,
        DeviationAnalyzerService deviationAnalyzer,
        WeekCounterService weekCounter,
        TargetValueService targetValues,
        ILogger<GrowDashboardComposer> logger,
        AlertRuleRepository? alertRules = null,
        HydroSetupRepository? hydroSetups = null,
        SetpointProfileRepository? setpointProfiles = null,
        LightRepository? lights = null,
        GrowRepository? grows = null,
        HarvestRepository? harvests = null,
        LightCycleReader? lightCycles = null)
    {
        _chartService = chartService;
        _deviationAnalyzer = deviationAnalyzer;
        _weekCounter = weekCounter;
        _targetValues = targetValues;
        _alertRules = alertRules;
        _hydroSetups = hydroSetups;
        _setpointProfiles = setpointProfiles;
        _lights = lights;
        _grows = grows;
        _harvests = harvests;
        _lightCycles = lightCycles;
        _logger = logger;
    }

    /// <summary>
    /// Zielbereich je Messwert fuer die aktuelle Phase des Zelts.
    ///
    /// Bis hierher trug eine Kachel nur die Zahl. Die Anzeige zeichnet jetzt eine
    /// Skala mit Zielband darunter — dafuer muss sie wissen, wo das Band liegt.
    /// Nicht jeder Wert hat einen: Licht und Fuellstand haben keinen Sollbereich,
    /// die bekommen null und zeichnen keine Skala.
    /// </summary>
    private static (double? Min, double? Max) TargetFor(string key, HydroTargetValues? t)
    {
        if (t is null) return (null, null);
        return key switch
        {
            "reservoir-ph" => (t.PhMin, t.PhMax),
            "reservoir-ec" => (t.EcMin, t.EcMax),
            "orp" => (t.OrpMin, t.OrpMax),
            "vpd" => (t.VpdMin, t.VpdMax),
            "ppfd" => (t.PpfdMin, t.PpfdMax),
            "co2" => (t.Co2Min, t.Co2Max),
            // Wassertemperatur ist im Wissen als Tag/Nacht-Paar hinterlegt; als
            // Band gilt die Spanne dazwischen.
            "reservoir-temp" => (Math.Min(t.WaterTempNightC, t.WaterTempDayC), Math.Max(t.WaterTempNightC, t.WaterTempDayC)),
            _ => (null, null),
        };
    }

    public List<MetricCard> BuildTentMetrics(Tent tent, Dictionary<string, HomeAssistantState> states, IReadOnlyList<Measurement> measurements)
    {
        // Use the latest non-null value PER metric, so a partial measurement
        // (e.g. an auto-measurement that only captured temp/humidity) does not
        // blank out pH/EC/ORP that an earlier manual measurement recorded.
        var latest = BuildLatestComposite(measurements);

        // Fuer die Herkunft am Wert: die Verbund-Messung oben verschmilzt Werte
        // aus MEHREREN Messungen, traegt aber nur den juengsten Zeitstempel. Ein
        // pH von vor fuenf Tagen saehe damit aus wie von heute — genau die
        // Sorte Ehrlichkeit, um die es hier geht. Also je Messgroesse einzeln
        // nachsehen, aus welcher Messung der Wert wirklich stammt.
        var geordnet = measurements
            .OrderByDescending(measurement => measurement.TakenAt)
            .ThenByDescending(measurement => measurement.Id)
            .ToList();

        (double? Wert, DateTime? Zeit) LetzteMessung(Func<Measurement, double?> selector)
        {
            foreach (var messung in geordnet)
            {
                if (selector(messung) is { } wert) return (wert, messung.TakenAt);
            }

            return (null, null);
        }

        // Die Phase des aktivsten Grows im Zelt bestimmt die Sollwerte. Ohne
        // aktiven Grow gibt es keinen Zielbereich — dann zeigen die Kacheln nur
        // die Zahl, was richtig ist: ein leeres Zelt hat kein Ziel.
        //
        // Die Phase kommt aus dem GROW, nicht aus der letzten Messung. Vorher
        // hing hier alles an `latest`: wer noch nie von Hand gemessen hatte, sah
        // den ganzen Bildschirm ohne einen einzigen Zielbereich — grau, ohne
        // „im Ziel" — obwohl die Sensoren lieferten und oben „Veg · Tag 7" stand.
        // Eine erfasste Messung darf die Phase weiterhin überstimmen: wer sie
        // eingetragen hat, weiss es besser als jede Rechnung.
        var activeGrow = tent.ActiveGrows.FirstOrDefault();
        var stage = latest?.Stage ?? (activeGrow is null ? (GrowStage?)null : GrowStageResolver.Resolve(activeGrow, DateTime.Today));

        // Welches Profil gilt: der Grow, sonst sein Hydro-System, sonst der
        // Anbaustil. Ohne diese Kette griffe immer nur der Anbaustil, und ein
        // eigenes Profil bliebe wirkungslos.
        var resolved = activeGrow is null
            ? null
            : SetpointProfileResolver.Resolve(
                activeGrow.SetpointProfileId,
                SystemProfileFor(activeGrow),
                activeGrow.HydroStyle);

        var targets = activeGrow is null || stage is null || resolved is null
            ? null
            : _targetValues.GetTargets(resolved.ProfileId, stage.Value);

        MetricCard Build(string label, string key, Func<Measurement?, double?> fallback, string tone = "default", string? explicitUnit = null)
        {
            if (states.TryGetValue(key, out var state))
            {
                return new MetricCard
                {
                    Key = key,
                    Label = label,
                    Value = state.NumericValue.HasValue
                        ? FormatMetricValue(key, state.NumericValue.Value)
                        : state.State,
                    Unit = explicitUnit ?? state.UnitOfMeasurement,
                    Tone = tone,
                    Hint = state.FriendlyName,
                    NumericValue = state.NumericValue,
                    ValueSource = "live",
                    TargetMin = TargetFor(key, targets).Min,
                    TargetMax = TargetFor(key, targets).Max
                };
            }

            var (value, gemessenAm) = LetzteMessung(m => fallback(m));
            return new MetricCard
            {
                Key = key,
                Label = label,
                Value = value.HasValue ? FormatMetricValue(key, value.Value) : "–",
                Unit = explicitUnit,
                Tone = tone,
                Hint = value.HasValue
                    ? "Letzte Messung"
                    : states.Count == 0 ? "Noch nicht mit Home Assistant verbunden" : "Kein Entity gemappt",
                NumericValue = value,
                ValueSource = value.HasValue ? "hand" : null,
                // TakenAt ist Ortszeit (so wird sie erfasst und gelesen), also
                // gegen DateTime.Now rechnen — gegen UtcNow waere der Wert um
                // die Zeitzone zu alt. Die Falle aus beta.20, andersherum.
                MeasuredAgeMinutes = gemessenAm is { } zeit
                    ? Math.Max(0, (int)(DateTime.Now - zeit).TotalMinutes)
                    : null,
                TargetMin = TargetFor(key, targets).Min,
                TargetMax = TargetFor(key, targets).Max
            };
        }

        var cards = new List<MetricCard>
        {
            Build("Temperatur", "temperature", m => m?.AirTemperatureC, explicitUnit: "°C"),
            Build("Luftfeuchte", "humidity", m => m?.HumidityPercent, explicitUnit: "%"),
            BuildVpdMetric(tent, states, latest, targets),
            BuildLightCycleMetric(tent, states, _lightCycles),
            BuildPpfdMetric(tent, states, latest)
        };

        if (tent.Co2Available || measurements.Any(m => m.Co2Ppm.HasValue))
        {
            var co2 = Build("CO2", "co2", m => m?.Co2Ppm, explicitUnit: "ppm");

            // Ein Sensor misst nur. Ohne Anreicherung steht die Luft bei
            // ~400-500 ppm, und das Profilziel (800-1400) stuende fuer immer
            // „daneben" — die Kachel war dann dauerhaft rot, obwohl alles
            // normal ist. Ohne Brenner also kein Ziel, mit Erklaerung.
            if (!tent.HasCo2Enrichment)
            {
                co2.TargetMin = null;
                co2.TargetMax = null;
                co2.Hint = "Ohne CO₂-Anreicherung ist Umgebungsluft (~400–500 ppm) normal";
            }

            cards.Add(co2);
        }

        var hasActiveHydro = tent.ActiveGrows.Any(g => g.IrrigationType == IrrigationType.ActiveHydro);

        // A reservoir metric is shown when its sensor is mapped and Home Assistant returns a
        // live value (states.ContainsKey), OR there is an active hydro grow, OR a past
        // measurement recorded it. Keying on the mapped sensor is essential: without it, freshly
        // mapped pH/EC/water-temp sensors stayed blank on the live dashboard until a grow was
        // flagged active-hydro or a manual measurement existed.
        if (hasActiveHydro || states.ContainsKey("reservoir-ph") || measurements.Any(m => m.ReservoirPh.HasValue))
            cards.Add(Build("pH", "reservoir-ph", m => m?.ReservoirPh));

        if (hasActiveHydro || states.ContainsKey("reservoir-ec") || measurements.Any(m => m.ReservoirEc.HasValue))
            cards.Add(Build("EC", "reservoir-ec", m => m?.ReservoirEc, explicitUnit: "mS/cm"));

        if (hasActiveHydro || states.ContainsKey("orp") || measurements.Any(m => m.OrpMv.HasValue))
            cards.Add(Build("ORP", "orp", m => m?.OrpMv, explicitUnit: "mV"));

        if (hasActiveHydro || states.ContainsKey("dissolved-oxygen") || measurements.Any(m => m.DissolvedOxygenMgL.HasValue))
        {
            var doKarte = Build("DO", "dissolved-oxygen", m => m?.DissolvedOxygenMgL, explicitUnit: "mg/L");

            // Ohne DO-Messwert wenigstens die Physik: wie viel Sauerstoff das
            // Wasser bei seiner Temperatur ueberhaupt halten KANN. Das ist keine
            // Messung und heisst auch nicht so — aber es zeigt den oft
            // uebersehenen Hebel: warmes Wasser haelt wenig, egal wie gross die
            // Pumpe ist.
            var wasserTemp = states.TryGetValue("reservoir-temp", out var tempState)
                ? tempState.NumericValue
                : LetzteMessung(m => m.ReservoirWaterTempC).Wert;
            if (doKarte.NumericValue is null && wasserTemp is { } temp)
            {
                doKarte.Hint = $"max. ~{AerationCheck.SaettigungMgL(temp).ToString("0.0", AppCulture.German)} mg/L "
                    + $"bei {temp.ToString("0.0", AppCulture.German)} °C möglich (berechnet)";
            }

            cards.Add(doKarte);
        }

        // Water level can be measured in liters (scale/flow) or centimeters (distance
        // sensor) — two separate mapping slots so units are always unambiguous.
        if (states.ContainsKey("reservoir-level") || measurements.Any(m => m.ReservoirLevelLiters.HasValue))
        {
            states.TryGetValue("reservoir-level", out var levelState);
            cards.Add(new MetricCard
            {
                Key = "reservoir-level",
                Label = "Wasserstand",
                Value = levelState is not null
                    ? levelState.NumericValue?.ToString("0.0") ?? levelState.State
                    : latest?.ReservoirLevelLiters?.ToString("0.0") ?? "–",
                // Als Zahl, nicht nur als Text: sonst haelt die Kachel den Wert
                // fuer eine Beschriftung, laesst die Einheit weg — und „72"
                // ohne „L" ist neben einem cm-Sensor schlicht zweideutig.
                NumericValue = levelState?.NumericValue ?? latest?.ReservoirLevelLiters,
                Unit = levelState?.UnitOfMeasurement ?? "L",
                Tone = "info",
                Hint = levelState is not null
                    ? levelState.FriendlyName
                    : latest?.ReservoirLevelLiters.HasValue == true ? "Letzte Messung" : null
            });
        }

        if (states.ContainsKey("reservoir-level-cm") || measurements.Any(m => m.ReservoirLevelCm.HasValue))
        {
            states.TryGetValue("reservoir-level-cm", out var levelCmState);
            cards.Add(new MetricCard
            {
                Key = "reservoir-level-cm",
                Label = "Wasserstand",
                Value = levelCmState is not null
                    ? levelCmState.NumericValue?.ToString("0.0") ?? levelCmState.State
                    : latest?.ReservoirLevelCm?.ToString("0.0") ?? "–",
                NumericValue = levelCmState?.NumericValue ?? latest?.ReservoirLevelCm,
                Unit = levelCmState?.UnitOfMeasurement ?? "cm",
                Tone = "info",
                Hint = levelCmState is not null
                    ? levelCmState.FriendlyName
                    : latest?.ReservoirLevelCm.HasValue == true ? "Letzte Messung" : null
            });
        }

        if (hasActiveHydro || states.ContainsKey("reservoir-temp") || measurements.Any(m => m.ReservoirWaterTempC.HasValue))
            cards.Add(Build("Wassertemp.", "reservoir-temp", m => m?.ReservoirWaterTempC, explicitUnit: "°C"));

        // Ist im Zelt gerade Tag? Nachts ist PPFD 0 richtig, CO₂ bei
        // Umgebungsluft richtig, und VPD-Ziele gelten für die Lichtphase.
        // Vorher malte jede Nacht rote Kacheln, der Score sank grundlos, und
        // genau so entsteht Alarm-Müdigkeit. Unbekannt heisst: wie bisher —
        // lieber ein unnötiges Nacht-Urteil als ein unterdrücktes Tag-Urteil.
        states.TryGetValue("light-status", out var lightNow);
        var lights = LightClock.Resolve(lightNow, _lights?.GetActiveLightScheduleForTent(tent.Id), DateTime.UtcNow);

        if (lights == LightsNow.Off)
        {
            foreach (var card in cards.Where(card => LightClock.IsDaytimeOnly(card.Key)))
            {
                card.TargetMin = null;
                card.TargetMax = null;
                card.TargetNote = null;
                card.TargetDerived = false;
                card.Hint = card.Key switch
                {
                    "ppfd" => "Licht aus — 0 ist hier richtig",
                    "co2" => "Licht aus — nachts braucht die Pflanze kein CO₂",
                    _ => "Licht aus — das VPD-Ziel gilt bei Licht an",
                };
            }
        }
        else
        {
            // Die abgeleiteten Klima-Bänder hängen am VPD-Ziel und damit am Tag.
            ApplyClimateBands(cards, targets, tent.LeafTempOffsetC, stage);
        }

        // Nach der Ernte wird das Zelt zum Trockenraum — 7 bis 14 Tage, und es
        // ist das hoechste Schimmelrisiko des ganzen Zyklus. Vorher schaute die
        // App ab der Ernte einfach weg: Grow abgeschlossen, keine Ziele mehr,
        // obwohl die Sensoren weiter haengen und genau jetzt zaehlen.
        ApplyDryingTargets(cards, tent, activeGrow);

        // Der Profilname auf die Kacheln, solange es nicht das Mitgelieferte ist.
        ApplyProfileNote(cards, resolved);

        // Zuletzt und damit ueber allem: was der Nutzer selbst eingetragen hat.
        // Erst hier, damit es auch die zurueckgerechneten Klimabaender schlaegt.
        ApplyUserTargets(cards, tent.Id, lights);

        return cards;
    }

    /// <summary>
    /// Gibt Temperatur und Luftfeuchte ihr Zielband — zurückgerechnet aus dem
    /// VPD-Ziel und dem jeweils anderen gemessenen Wert.
    /// </summary>
    /// <remarks>
    /// Das Wissen kennt für Klima nur ein VPD-Band. Die beiden größten Kacheln
    /// des Bildschirms blieben dadurch immer ohne Bewertung, während die kleine
    /// VPD-Kachel daneben eine hatte. Gerechnet wird mit derselben Formel wie
    /// hin, nur nach der anderen Variablen aufgelöst; erfunden wird nichts.
    ///
    /// Ohne den Partnerwert gibt es kein Band: ein Temperaturziel ohne bekannte
    /// Feuchte wäre geraten.
    /// </remarks>
    /// <summary>Das Profil des Hydro-Systems, an dem der Grow hängt.</summary>
    private string? SystemProfileFor(GrowRun grow)
        => grow.SystemId is { } systemId ? _hydroSetups?.GetSystem(systemId)?.SetpointProfileId : null;

    /// <summary>
    /// Schreibt den Profilnamen auf die Kacheln, wenn ein eigenes Profil gilt.
    /// </summary>
    /// <remarks>
    /// Nur bei Abweichung vom Mitgelieferten. Stünde der Name überall, wäre er
    /// auf jeder Kachel Text, der nichts beantwortet — dieselbe Regel wie bei
    /// „dein Wert".
    /// </remarks>
    private void ApplyProfileNote(List<MetricCard> cards, ResolvedProfile? resolved)
    {
        if (resolved is null) return;
        if (SetpointProfile.IdFromReference(resolved.ProfileId) is not { } customId) return;

        var name = _setpointProfiles?.Get(customId)?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;

        foreach (var card in cards)
        {
            if (card.TargetMin is null && card.TargetMax is null) continue;
            if (card.TargetNote is not null) continue;   // zurueckgerechnete Baender behalten ihren Zusatz
            card.TargetNote = name;
        }
    }

    /// <summary>
    /// Der eingetragene Wert des Nutzers gewinnt — über dem Wissen und über
    /// jedem zurückgerechneten Band.
    /// </summary>
    /// <remarks>
    /// Und die Kachel sagt es: „dein Wert". Ohne den Zusatz stünde dort eine
    /// Zahl, die von den mitgelieferten abweicht, ohne dass jemand erkennen
    /// könnte warum — genau die Verwirrung, die das hier abstellt.
    /// </remarks>
    /// <summary>
    /// Trocknungs-Klima auf Temperatur und Feuchte, solange im Zelt frisch
    /// geerntet haengt.
    /// </summary>
    /// <remarks>
    /// Trocknung liegt vor, wenn kein Grow mehr laeuft, der letzte in den
    /// vergangenen drei Wochen geerntet wurde und noch kein Trockengewicht
    /// eingetragen ist — das Gewicht ist der natuerliche Abschluss: gewogen
    /// wird nach dem Trocknen.
    /// </remarks>
    private void ApplyDryingTargets(List<MetricCard> cards, Tent tent, GrowRun? activeGrow)
    {
        if (activeGrow is not null) return;
        if (DryingWindow.DayFor(_grows, _harvests, tent.Id, DateTime.Today) is not { } tag) return;

        if (cards.FirstOrDefault(card => card.Key == "temperature") is { } temperatur)
        {
            temperatur.TargetMin = MoldGuard.DryingTempMinC;
            temperatur.TargetMax = MoldGuard.DryingTempMaxC;
            temperatur.TargetNote = $"Trocknung · Tag {tag}";
            temperatur.TargetDerived = false;
        }

        if (cards.FirstOrDefault(card => card.Key == "humidity") is { } feuchte)
        {
            feuchte.TargetMin = MoldGuard.DryingHumidityMin;
            feuchte.TargetMax = MoldGuard.DryingHumidityMax;
            feuchte.TargetNote = $"Trocknung · Tag {tag}";
            feuchte.TargetDerived = false;
        }
    }

    private void ApplyUserTargets(List<MetricCard> cards, int tentId, LightsNow lights = LightsNow.Unknown)
    {
        var rules = _alertRules?.GetForTent(tentId);
        if (rules is null || rules.Count == 0) return;

        foreach (var card in cards)
        {
            // Auch der eigene Grenzwert für PPFD/CO₂/VPD meint den Tag — bei
            // Licht aus würde er jede Nacht anschlagen.
            if (lights == LightsNow.Off && LightClock.IsDaytimeOnly(card.Key)) continue;

            if (UserTargets.For(card.Key, rules) is not { } eigene) continue;

            card.TargetMin = eigene.Min;
            card.TargetMax = eigene.Max;
            card.TargetNote = UserTargets.SourceLabel;
            // Kein abgeleiteter Wert mehr: was der Nutzer setzt, zaehlt voll in
            // den Score. Sonst waere sein eigener Grenzwert der einzige, der
            // nicht bewertet wird.
            card.TargetDerived = false;
        }
    }

    private static void ApplyClimateBands(List<MetricCard> cards, HydroTargetValues? targets, double leafOffsetC, GrowStage? stage)
    {
        if (targets is null) return;

        var temperature = cards.FirstOrDefault(card => card.Key == "temperature");
        var humidity = cards.FirstOrDefault(card => card.Key == "humidity");

        // Beide Bänder aus den Werten VOR der Änderung rechnen, sonst hinge das
        // zweite am gerade gesetzten Ziel des ersten.
        var luft = temperature?.NumericValue;
        var feuchte = humidity?.NumericValue;

        // Der Schimmeldeckel der Phase begrenzt jede Feuchte-Empfehlung. Die
        // reine VPD-Rückrechnung kennt nur Physik — in der Blüte bei 32 °C käme
        // sonst „64–68 %" heraus, und ab ~60 % droht dort Grauschimmel.
        var deckel = stage is { } phase ? MoldGuard.MaxHumidityPercent(phase) : (double?)null;

        if (luft is { } l && humidity is not null && humidity.TargetMin is null)
        {
            var (min, max) = ClimateBandCalculator.HumidityBand(l, targets.VpdMin, targets.VpdMax, leafOffsetC, deckel);
            if (min is not null)
            {
                humidity.TargetMin = min;
                humidity.TargetMax = max;
                humidity.TargetNote = deckel is { } cap && max >= cap
                    ? $"bei {l.ToString("0.#", AppCulture.German)} °C · Schimmelschutz: höchstens {cap.ToString("0", AppCulture.German)} %"
                    : $"bei {l.ToString("0.#", AppCulture.German)} °C";
                humidity.TargetDerived = true;
            }
            else if (ClimateBandCalculator.HumidityBand(l, targets.VpdMin, targets.VpdMax, leafOffsetC).Min is not null)
            {
                // Ohne Deckel gäbe es ein Band — der Schimmelschutz hat es
                // geschluckt. Dann ist die ehrliche Ansage: nicht die Feuchte
                // hochziehen, sondern die Temperatur senken.
                humidity.Hint = $"Fürs VPD-Ziel bräuchte es mehr als {deckel!.Value.ToString("0", AppCulture.German)} % — ab da droht Schimmel. Besser die Temperatur senken.";
            }
        }

        if (feuchte is { } f && temperature is not null && temperature.TargetMin is null)
        {
            var (min, max) = ClimateBandCalculator.TemperatureBand(f, targets.VpdMin, targets.VpdMax, leafOffsetC);
            if (min is not null)
            {
                temperature.TargetMin = min;
                temperature.TargetMax = max;
                temperature.TargetNote = $"bei {f.ToString("0.#", AppCulture.German)} % RLF";
                temperature.TargetDerived = true;
            }
            else
            {
                // Es gibt keine Temperatur, die bei dieser Luftfeuchte das
                // VPD-Ziel trifft — bei 40 % RLF liegt das VPD schon bei 5 °C
                // über einem Ziel von 0,40 kPa, und tiefer wird nicht gesucht.
                //
                // Vorher blieb die Kachel dann einfach leer, und man rätselte,
                // warum ausgerechnet dort nichts steht. Die Rechnung kennt den
                // Grund; also sagt sie ihn. Und sie sagt gleich, an welcher
                // Schraube man wirklich drehen muss: die Luftfeuchte ist zu weit
                // weg, nicht die Temperatur.
                temperature.Hint = $"Kein Ziel möglich bei {f.ToString("0.#", AppCulture.German)} % RLF — erst die Luftfeuchte angehen.";
            }
        }
    }

    public ChartSeries BuildTentClimateChart(
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<TentSensorReading> recentReadings,
        IReadOnlyList<TentSensorDailyStat> dailyStats,
        DateTime chartFrom)
    {
        var useRecent = chartFrom >= DateTime.Today.AddDays(-7);

        if (useRecent)
        {
            var tempPoints     = recentReadings.Where(x => x.MetricKey == "temperature").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var humidityPoints = recentReadings.Where(x => x.MetricKey == "humidity").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var vpdPoints      = recentReadings.Where(x => x.MetricKey == "vpd").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();

            if (tempPoints.Count == 0)
                tempPoints = measurements.Where(x => x.AirTemperatureC.HasValue).Select(x => (x.TakenAt, x.AirTemperatureC)).ToList();
            if (humidityPoints.Count == 0)
                humidityPoints = measurements.Where(x => x.HumidityPercent.HasValue).Select(x => (x.TakenAt, x.HumidityPercent)).ToList();

            return _chartService.BuildSeries(
                "Klima-Verlauf",
                "Klima",
                ("Temperatur", "#8b5cf6", tempPoints),
                ("Luftfeuchte", "#22c55e", humidityPoints),
                ("VPD", "#f59e0b", vpdPoints));
        }
        else
        {
            // Tages-Perzentil-Bänder aus DailyStats
            var tempStats = dailyStats.Where(x => x.MetricKey == "temperature").ToList();
            var p5Points     = tempStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P5)).ToList();
            var medianPoints = tempStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.Median)).ToList();
            var p95Points    = tempStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P95)).ToList();

            if (medianPoints.Count == 0)
            {
                // Fallback auf manuelle Messungen
                medianPoints = measurements.Where(x => x.AirTemperatureC.HasValue).Select(x => (x.TakenAt, x.AirTemperatureC)).ToList();
            }

            return _chartService.BuildSeries(
                "Klima-Verlauf (Tage)",
                "Klima",
                ("Temp P5",     "#8b5cf6", p5Points),
                ("Temp Median", "#8b5cf6", medianPoints),
                ("Temp P95",    "#8b5cf6", p95Points));
        }
    }

    public ChartSeries BuildTentWaterChart(
        IReadOnlyList<Measurement> measurements,
        IReadOnlyList<TentSensorReading> recentReadings,
        IReadOnlyList<TentSensorDailyStat> dailyStats,
        DateTime chartFrom)
    {
        var useRecent = chartFrom >= DateTime.Today.AddDays(-7);

        if (useRecent)
        {
            var phPoints        = recentReadings.Where(x => x.MetricKey == "reservoir-ph").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var ecPoints        = recentReadings.Where(x => x.MetricKey == "reservoir-ec").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var levelPoints     = recentReadings.Where(x => x.MetricKey == "reservoir-level").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();
            var waterTempPoints = recentReadings.Where(x => x.MetricKey == "reservoir-temp").Select(x => (x.CapturedAtUtc.ToLocalTime(), (double?)x.Value)).ToList();

            if (phPoints.Count == 0)
                phPoints = measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh)).ToList();
            if (ecPoints.Count == 0)
                ecPoints = measurements.Where(x => x.ReservoirEc.HasValue).Select(x => (x.TakenAt, x.ReservoirEc)).ToList();
            if (levelPoints.Count == 0)
                levelPoints = measurements
                    .Where(x => x.ReservoirLevelLiters.HasValue || x.ReservoirLevelCm.HasValue)
                    .Select(x => (x.TakenAt, x.ReservoirLevelLiters ?? x.ReservoirLevelCm))
                    .ToList();
            if (waterTempPoints.Count == 0)
                waterTempPoints = measurements.Where(x => x.ReservoirWaterTempC.HasValue).Select(x => (x.TakenAt, x.ReservoirWaterTempC)).ToList();

            return _chartService.BuildSeries(
                "Wasser / Reservoir",
                "Reservoir",
                ("pH", "#38bdf8", phPoints),
                ("EC", "#22c55e", ecPoints),
                ("Level", "#f97316", levelPoints),
                ("Wassertemp.", "#ef4444", waterTempPoints));
        }
        else
        {
            // Tages-Perzentil-Bänder für pH
            var phStats = dailyStats.Where(x => x.MetricKey == "reservoir-ph").ToList();
            var p5Points     = phStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P5)).ToList();
            var medianPoints = phStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.Median)).ToList();
            var p95Points    = phStats.Select(x => (x.Date.ToDateTime(TimeOnly.MinValue), (double?)x.P95)).ToList();

            if (medianPoints.Count == 0)
            {
                medianPoints = measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh)).ToList();
            }

            return _chartService.BuildSeries(
                "Reservoir (Tage)",
                "Reservoir",
                ("pH P5",     "#38bdf8", p5Points),
                ("pH Median", "#38bdf8", medianPoints),
                ("pH P95",    "#38bdf8", p95Points));
        }
    }

    public ChartSeries BuildActivityChart(IReadOnlyList<Measurement> measurements)
    {
        var waterPoints = measurements.Where(x => x.WaterAmountMl.HasValue).Select(x => (x.TakenAt, x.WaterAmountMl)).ToList();
        var runoffPoints = measurements.Where(x => x.RunoffAmountMl.HasValue).Select(x => (x.TakenAt, x.RunoffAmountMl)).ToList();
        var heightPoints = measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm)).ToList();

        return _chartService.BuildSeries(
            "Aktivität & Entwicklung",
            "Aktivität",
            ("Wasser", "#60a5fa", waterPoints),
            ("Runoff", "#f59e0b", runoffPoints),
            ("Höhe", "#34d399", heightPoints));
    }



    public ChartSeries BuildGrowMainChart(GrowRun grow, IReadOnlyList<Measurement> measurements)
    {
        if (grow.Profile.IsHydro)
        {
            return _chartService.BuildSeries(
                grow.HydroStyle == HydroStyle.RDWC ? "RDWC Reservoir" : "Hydro Reservoir",
                "Hydro",
                ("pH", "#38bdf8", measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh))),
                ("EC", "#22c55e", measurements.Where(x => x.ReservoirEc.HasValue).Select(x => (x.TakenAt, x.ReservoirEc))),
                ("Wassertemp.", "#ef4444", measurements.Where(x => x.ReservoirWaterTempC.HasValue).Select(x => (x.TakenAt, x.ReservoirWaterTempC))));
        }

        if (grow.Profile.IsAutopot)
        {
            return _chartService.BuildSeries(
                "Autopot Reservoir",
                "Reservoir",
                ("Reservoir pH", "#38bdf8", measurements.Where(x => x.ReservoirPh.HasValue).Select(x => (x.TakenAt, x.ReservoirPh))),
                ("Reservoir EC", "#22c55e", measurements.Where(x => x.ReservoirEc.HasValue).Select(x => (x.TakenAt, x.ReservoirEc))),
                ("Wasserstand", "#f59e0b", measurements.Where(x => x.ReservoirLevelLiters.HasValue).Select(x => (x.TakenAt, x.ReservoirLevelLiters))));
        }

        return _chartService.BuildSeries(
            grow.Profile.IsCoco ? "Coco Klima & Wuchs" : "Klima & Wuchs",
            "Pflanze",
            ("Temperatur", "#8b5cf6", measurements.Where(x => x.AirTemperatureC.HasValue).Select(x => (x.TakenAt, x.AirTemperatureC))),
            ("Luftfeuchte", "#22c55e", measurements.Where(x => x.HumidityPercent.HasValue).Select(x => (x.TakenAt, x.HumidityPercent))),
            ("Höhe", "#f59e0b", measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm))));
    }

    public ChartSeries BuildGrowSecondaryChart(GrowRun grow, IReadOnlyList<Measurement> measurements)
    {
        if (grow.Profile.IsHydro)
        {
            return _chartService.BuildSeries(
                "Wasserstand & Addback",
                "Hydro",
                ("Wasserstand (L)", "#f59e0b", measurements.Where(x => x.ReservoirLevelLiters.HasValue).Select(x => (x.TakenAt, x.ReservoirLevelLiters))),
                ("Top-Off (L)", "#14b8a6", measurements.Where(x => x.TopOffLiters.HasValue).Select(x => (x.TakenAt, x.TopOffLiters))),
                ("Addback EC", "#fb7185", measurements.Where(x => x.AddbackEc.HasValue).Select(x => (x.TakenAt, x.AddbackEc))));
        }

        if (grow.Profile.IsAutopot)
        {
            return _chartService.BuildSeries(
                "Autopot Feed",
                "Reservoir",
                ("Top-Off", "#14b8a6", measurements.Where(x => x.TopOffLiters.HasValue).Select(x => (x.TakenAt, x.TopOffLiters))),
                ("Wassertemp.", "#ef4444", measurements.Where(x => x.ReservoirWaterTempC.HasValue).Select(x => (x.TakenAt, x.ReservoirWaterTempC))),
                ("Höhe", "#f59e0b", measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm))));
        }

        return _chartService.BuildSeries(
            grow.Profile.IsSoilOrganic ? "Bewässerung & pH" : "Input vs. Drain",
            "Medium",
            ("Gießmenge", "#14b8a6", measurements.Where(x => x.WaterAmountMl.HasValue).Select(x => (x.TakenAt, x.WaterAmountMl))),
            ("Input pH", "#38bdf8", measurements.Where(x => x.IrrigationPh.HasValue).Select(x => (x.TakenAt, x.IrrigationPh))),
            ("Drain pH", "#fb7185", measurements.Where(x => x.DrainPh.HasValue).Select(x => (x.TakenAt, x.DrainPh))),
            ("Drain EC", "#22c55e", measurements.Where(x => x.DrainEc.HasValue).Select(x => (x.TakenAt, x.DrainEc))));
    }

    public ChartSeries BuildGrowWateringChart(IReadOnlyList<Measurement> measurements)
    {
        return _chartService.BuildSeries(
            "Events & Aufwand",
            "Pflege",
            ("Runoff", "#a78bfa", measurements.Where(x => x.RunoffAmountMl.HasValue).Select(x => (x.TakenAt, x.RunoffAmountMl))),
            ("Wasser", "#14b8a6", measurements.Where(x => x.WaterAmountMl.HasValue).Select(x => (x.TakenAt, x.WaterAmountMl))),
            ("Höhe", "#f59e0b", measurements.Where(x => x.HeightCm.HasValue).Select(x => (x.TakenAt, x.HeightCm))));
    }

    private static Measurement? BuildLatestComposite(IReadOnlyList<Measurement> measurements)
    {
        var ordered = measurements
            .OrderByDescending(measurement => measurement.TakenAt)
            .ThenByDescending(measurement => measurement.Id)
            .ToList();
        if (ordered.Count == 0)
        {
            return null;
        }

        double? Pick(Func<Measurement, double?> selector)
        {
            foreach (var measurement in ordered)
            {
                var value = selector(measurement);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        var head = ordered[0];
        return new Measurement
        {
            Id = head.Id,
            GrowId = head.GrowId,
            TakenAt = head.TakenAt,
            Stage = head.Stage,
            Source = head.Source,
            AirTemperatureC = Pick(m => m.AirTemperatureC),
            HumidityPercent = Pick(m => m.HumidityPercent),
            ReservoirPh = Pick(m => m.ReservoirPh),
            ReservoirEc = Pick(m => m.ReservoirEc),
            ReservoirWaterTempC = Pick(m => m.ReservoirWaterTempC),
            ReservoirLevelLiters = Pick(m => m.ReservoirLevelLiters),
            ReservoirLevelCm = Pick(m => m.ReservoirLevelCm),
            DissolvedOxygenMgL = Pick(m => m.DissolvedOxygenMgL),
            OrpMv = Pick(m => m.OrpMv),
            PpfdMol = Pick(m => m.PpfdMol),
            Co2Ppm = Pick(m => m.Co2Ppm),
        };
    }

    private static MetricCard BuildLightCycleMetric(
        Tent tent, Dictionary<string, HomeAssistantState> states, LightCycleReader? lightCycles)
    {
        // Der gelernte Zyklus aus den beobachteten Flanken — „18/6", „12/12".
        // Das TODO von Sprint B1b ist damit erledigt: niemand traegt den Zyklus
        // ein, Grow OS liest ihn aus dem, was ohnehin aufgezeichnet wird.
        var cycle = lightCycles?.CycleFor(tent.Id, DateTime.UtcNow);
        // Show the live light on/off state from the mapped LightStatus entity.
        if (states.TryGetValue(TentSensorMetricKeyMap.Resolve(SensorMetricType.LightStatus), out var lightState))
        {
            var normalized = LightStateNormalizer.Normalize(lightState.State);
            if (normalized != LightState.Unknown)
            {
                var isOn = normalized == LightState.On;
                return new MetricCard
                {
                    Key = "light-cycle",
                    Label = "Licht",
                    Value = isOn ? "An" : "Aus",
                    Tone = isOn ? "accent" : "info",
                    Hint = cycle is not null
                        ? $"{cycle.Label} · an {cycle.OnAt:HH:mm}, aus {cycle.OffAt:HH:mm}"
                        : lightState.FriendlyName ?? (isOn ? "Licht eingeschaltet" : "Licht ausgeschaltet")
                };
            }
        }

        return new MetricCard
        {
            Key = "light-cycle",
            Label = "Lichtzyklus",
            Value = cycle?.Label ?? "–",
            Tone = "info",
            Hint = cycle is not null
                ? $"aus den letzten {cycle.Days} Schaltvorgängen gelesen"
                : states.Count == 0 ? "Nicht mit Home Assistant verbunden" : "Kein Licht-Sensor gemappt"
        };
    }

    private static MetricCard BuildPpfdMetric(Tent tent, IReadOnlyDictionary<string, HomeAssistantState> states, Measurement? latest)
    {
        if (states.TryGetValue("ppfd", out var state))
        {
            return new MetricCard
            {
                Key = "ppfd",
                Label = "PPFD",
                Value = state.NumericValue.HasValue
                    ? FormatMetricValue("ppfd", state.NumericValue.Value)
                    : state.State,
                Unit = state.UnitOfMeasurement ?? "µmol/m²/s",
                Tone = "accent",
                Hint = state.FriendlyName
            };
        }

        if (latest?.PpfdMol.HasValue == true)
        {
            return new MetricCard
            {
                Key = "ppfd",
                Label = "PPFD",
                Value = FormatMetricValue("ppfd", latest.PpfdMol.Value),
                Unit = "µmol/m²/s",
                Tone = "accent",
                Hint = "Letzte Messung"
            };
        }

        return new MetricCard
        {
            Key = "ppfd",
            Label = "PPFD",
            Value = "–",
            Unit = null,
            Tone = "accent",
            Hint = "Kein Sensor konfiguriert"
        };
    }

    public IReadOnlyList<GrowDeviation> BuildDeviationsForGrow(GrowRun grow, IReadOnlyList<Measurement> measurements)
    {
        try
        {
            var recent = measurements
                .Where(m => m.GrowId == grow.Id)
                .OrderByDescending(m => m.TakenAt)
                .Take(3)
                .ToList();

            var weekInfo = _weekCounter.Calculate(grow);
            var hydroDeviations = _deviationAnalyzer.Analyze(grow, recent);
            var germinationDeviations = _deviationAnalyzer.CheckGerminationAndRooting(grow, weekInfo);

            return hydroDeviations.Concat(germinationDeviations).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei Deviations-Berechnung für Grow {GrowId} ({GrowName})", grow.Id, grow.Name);
            return Array.Empty<GrowDeviation>();
        }
    }

    private static string FormatMetricValue(string key, double value)
    {
        return key switch
        {
            "temperature"     => value.ToString("0.0"),
            "humidity"        => value.ToString("0"),
            "vpd"             => value.ToString("0.00"),
            "co2"             => value.ToString("0"),
            "reservoir-ph"    => value.ToString("0.00"),
            "reservoir-ec"    => value.ToString("0.00"),
            "orp"             => value.ToString("0"),
            "dissolved-oxygen" => value.ToString("0.0"),
            "reservoir-temp"  => value.ToString("0.0"),
            "reservoir-level" => value.ToString("0.0"),
            "ppfd"            => value.ToString("0"),
            "ups-battery"     => value.ToString("0"),
            _                 => value.ToString("0.#")
        };
    }

    /// <summary>
    /// VPD is either read from a mapped entity, or derived. When deriving, prefer the LIVE
    /// temperature/humidity over the last stored measurement (which could be days old), and
    /// apply the tent's leaf-temperature offset so the number reflects leaf VPD.
    /// </summary>
    private MetricCard BuildVpdMetric(Tent tent, Dictionary<string, HomeAssistantState> states, Measurement? latest, HydroTargetValues? targets)
    {
        if (states.TryGetValue("vpd", out var mapped))
        {
            return new MetricCard
            {
                Key = "vpd",
                Label = "VPD",
                Value = mapped.NumericValue.HasValue ? FormatMetricValue("vpd", mapped.NumericValue.Value) : mapped.State,
                Unit = "kPa",
                Tone = "accent",
                Hint = mapped.FriendlyName,
                NumericValue = mapped.NumericValue,
                TargetMin = TargetFor("vpd", targets).Min,
                TargetMax = TargetFor("vpd", targets).Max
            };
        }

        var liveTemp = states.TryGetValue("temperature", out var t) ? t.NumericValue : null;
        var liveHumidity = states.TryGetValue("humidity", out var h) ? h.NumericValue : null;
        var fromLive = liveTemp.HasValue && liveHumidity.HasValue;

        var value = VpdCalculator.Calculate(
            fromLive ? liveTemp : latest?.AirTemperatureC,
            fromLive ? liveHumidity : latest?.HumidityPercent,
            tent.LeafTempOffsetC);

        var offsetHint = tent.LeafTempOffsetC > 0 ? $", Blatt −{tent.LeafTempOffsetC:0.#} °C" : string.Empty;
        return new MetricCard
        {
            Key = "vpd",
            Label = "VPD",
            Value = value.HasValue ? FormatMetricValue("vpd", value.Value) : "–",
            Unit = "kPa",
            Tone = "accent",
            Hint = value.HasValue
                ? (fromLive ? $"Berechnet aus Live-Werten{offsetHint}" : $"Berechnet aus letzter Messung{offsetHint}")
                : "Temperatur und Luftfeuchte fehlen",
            NumericValue = value,
            TargetMin = TargetFor("vpd", targets).Min,
            TargetMax = TargetFor("vpd", targets).Max
        };
    }

}
