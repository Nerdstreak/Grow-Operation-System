using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GrowDiary.Web.Services;

/// <summary>
/// Der Takt hinter der Dosierung — zwei Aufgaben, eine Schleife.
/// </summary>
/// <remarks>
/// <para><b>Erstens: die Wirkung nachtragen.</b> Ohne das lernt keine Pumpe je
/// etwas, weil bei jeder Dosis nur der Wert davor festgehalten wurde. Das ist
/// die Voraussetzung für den Vorschlag — und für alles, was darauf aufbaut.</para>
///
/// <para><b>Zweitens: die Automatik.</b> Für Pumpen, bei denen sie eingeschaltet
/// ist, wird gerechnet, geprüft und gegebenenfalls dosiert. Die Anschläge dafür
/// stehen in <see cref="DosingGuard.EvaluateAutomatic"/> und sind schärfer als
/// von Hand: wer selbst drückt, steht daneben.</para>
///
/// <para>Jede unbeaufsichtigte Dosis geht als Nachricht raus. Eine Pumpe, die
/// von allein Säure ins Becken gedrückt hat, ist keine Randnotiz im
/// Protokoll — das gehört aufs Handy.</para>
/// </remarks>
public sealed class DosingWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DosingWorker> _logger;

    public DosingWorker(IServiceProvider serviceProvider, ILogger<DosingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Versetzt zu den anderen Schleifen starten, damit nicht alle im selben
        // Moment auf Home Assistant losgehen.
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dosier-Durchlauf fehlgeschlagen.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dosing = scope.ServiceProvider.GetRequiredService<DosingRepository>();
        var situations = scope.ServiceProvider.GetRequiredService<DosingContextBuilder>();
        var service = scope.ServiceProvider.GetRequiredService<DosingService>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var nowUtc = DateTime.UtcNow;

        // Zuerst die ausstehenden zweiten Haelften: A steht schon im Becken, B
        // fehlt noch. Das hat Vorrang vor allem, was neu dazukaeme.
        await GivePendingAsync(dosing, service, situations, pump: null, nowUtc, cancellationToken);

        foreach (var pump in dosing.GetPumps())
        {
            var situation = situations.Build(pump, nowUtc);

            RecordEffects(dosing, pump, situation, nowUtc);

            if (pump.AutomationEnabled)
            {
                await DoseIfNeededAsync(dosing, service, notifications, pump, situation, nowUtc, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Die zweite Hälfte eines Zweikomponenten-Düngers geben, sobald die
    /// Trennzeit um ist.
    /// </summary>
    /// <remarks>
    /// Der Eintrag wird <b>vor</b> dem Schalten entfernt. Bliebe er stehen und
    /// das Add-on stürzte nach dem Schalten ab, käme B beim nächsten Start ein
    /// zweites Mal — im Becken stünde dann doppelt so viel B wie A. Andersherum
    /// fehlt B im schlimmsten Fall einmal, und das ist die harmlosere Hälfte des
    /// Risikos.
    ///
    /// Die üblichen Anschläge werden hier <b>nicht</b> gefragt: die Mischpause
    /// hat gerade erst A gesehen, und sie würde B genau deshalb ablehnen. B ist
    /// keine neue Entscheidung — es ist die Vollendung einer schon getroffenen.
    /// Die harte Sekundengrenze in <see cref="DosingService.RunForSecondsAsync"/>
    /// gilt weiterhin.
    /// </remarks>
    private async Task GivePendingAsync(
        DosingRepository dosing,
        DosingService service,
        DosingContextBuilder situations,
        DosingPump? pump,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        foreach (var pending in dosing.GetDuePending(nowUtc))
        {
            var ziel = pump?.Id == pending.PumpId ? pump : dosing.GetPump(pending.PumpId);
            if (ziel is null)
            {
                dosing.DeletePending(pending.Id);
                continue;
            }

            var sekunden = DosingCalculator.SecondsFor(pending.Ml, ziel.MlPerMinute ?? 0);
            if (sekunden <= 0)
            {
                _logger.LogError(
                    "Zweite Hälfte für {Pump} nicht möglich: keine Fördermenge. {Ml:0.##} ml von Hand nachgeben.",
                    ziel.Name, pending.Ml);
                dosing.DeletePending(pending.Id);
                continue;
            }

            dosing.DeletePending(pending.Id);

            var ok = await service.RunForSecondsAsync(ziel, sekunden, cancellationToken);
            dosing.InsertEvent(new DoseEvent
            {
                PumpId = ziel.Id,
                TentId = ziel.TentId,
                OccurredAtUtc = nowUtc,
                Trigger = DoseTrigger.Partner,
                Outcome = ok ? DoseOutcome.Done : DoseOutcome.Failed,
                RequestedMl = pending.Ml,
                DosedMl = ok ? pending.Ml : 0,
                SecondsRun = ok ? sekunden : 0,
                ValueBefore = situations.Build(ziel, nowUtc).Context.Reading,
                Simulated = ziel.SimulationMode,
                Reason = ok
                    ? pending.Reason ?? "Zweite Hälfte."
                    : "Zweite Hälfte: Home Assistant hat die Pumpe nicht geschaltet.",
            });

            if (ok)
            {
                _logger.LogInformation("Zweite Hälfte: {Pump} hat {Ml:0.##} ml gegeben.", ziel.Name, pending.Ml);
            }
            else
            {
                _logger.LogError("Zweite Hälfte: {Pump} liess sich nicht schalten — {Ml:0.##} ml fehlen im Becken.", ziel.Name, pending.Ml);
            }
        }
    }

    /// <summary>Den Wert nach einer Dosis eintragen, sobald sie durchmischt ist.</summary>
    private void RecordEffects(DosingRepository dosing, DosingPump pump, DosingSituation situation, DateTime nowUtc)
    {
        if (situation.Context.Reading is not { } jetzt) return;

        foreach (var dose in dosing.GetEvents(pumpId: pump.Id, limit: 50))
        {
            // Die Liste kommt neu zuerst. Ist das Fenster einer Dosis zu, sind
            // alle aelteren erst recht durch — dann lohnt kein Weiterschauen.
            if (DosingFollowUp.WindowHasClosed(dose, pump.MinIntervalMinutes, nowUtc)) break;
            if (!DosingFollowUp.IsReadyForEffect(dose, pump.MinIntervalMinutes, nowUtc)) continue;

            dosing.SetValueAfter(dose.Id, jetzt);
            _logger.LogInformation(
                "Wirkung nachgetragen: Pumpe {Pump}, {Ml:0.##} ml, {Before:0.00} → {After:0.00}.",
                pump.Name, dose.DosedMl, dose.ValueBefore, jetzt);
        }
    }

    private async Task DoseIfNeededAsync(
        DosingRepository dosing,
        DosingService service,
        NotificationService notifications,
        DosingPump pump,
        DosingSituation situation,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (situation.Context.Reading is not { } ist || situation.Target is not { } ziel) return;

        var gelernt = DosingCalculator.LearnedChangePerMl(dosing.GetEvents(pumpId: pump.Id, limit: 50));
        if (DosingCalculator.MlToReach(ist, ziel, gelernt) is not { } ml) return;

        var decision = DosingGuard.EvaluateAutomatic(pump, ml, situation.Context, nowUtc);
        if (!decision.Allowed)
        {
            // Kein Protokolleintrag: die Automatik prueft jede Minute, und
            // „Mischpause laeuft noch" waere sonst 18 Zeilen pro Dosis.
            _logger.LogDebug("Automatik {Pump}: {Reason}", pump.Name, decision.Reason);
            return;
        }

        var ok = await service.RunForSecondsAsync(pump, decision.Seconds, cancellationToken);
        dosing.InsertEvent(new DoseEvent
        {
            PumpId = pump.Id,
            TentId = pump.TentId,
            OccurredAtUtc = nowUtc,
            Trigger = DoseTrigger.Automatic,
            Outcome = ok ? DoseOutcome.Done : DoseOutcome.Failed,
            RequestedMl = decision.Ml,
            DosedMl = ok ? decision.Ml : 0,
            SecondsRun = ok ? decision.Seconds : 0,
            ValueBefore = ist,
            TargetValue = ziel,
            Simulated = pump.SimulationMode,
            Reason = ok
                ? (pump.SimulationMode ? "Automatik im Testbetrieb — es ist nichts geflossen." : "Automatik.")
                : "Automatik: Home Assistant hat die Pumpe nicht geschaltet.",
        });

        if (!ok)
        {
            _logger.LogError("Automatik {Pump}: Home Assistant hat nicht geschaltet.", pump.Name);
            return;
        }

        _logger.LogInformation("Automatik {Pump}: {Ml:0.##} ml, {Ist:0.00} → Ziel {Ziel:0.00}.", pump.Name, decision.Ml, ist, ziel);

        await notifications.SendAsync(
            NotificationCategory.System,
            $"{pump.Name} hat dosiert",
            $"{decision.Ml:0.##} ml automatisch gegeben. Wert war {ist:0.00}, Ziel {ziel:0.00}."
                + (pump.SimulationMode ? " (Testbetrieb — es ist nichts geflossen.)" : string.Empty),
            cancellationToken);
    }
}
