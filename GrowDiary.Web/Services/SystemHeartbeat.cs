namespace GrowDiary.Web.Services;

/// <summary>
/// The pulse of the background machinery, so the watchdog can tell "everything is quiet
/// because all is well" from "everything is quiet because I stopped working". Held in
/// memory on purpose: after a restart the slate is clean and the next check re-evaluates.
/// </summary>
public sealed class SystemHeartbeat
{
    private readonly object _gate = new();

    /// <summary>
    /// Wann dieser Prozess gestartet ist.
    /// </summary>
    /// <remarks>
    /// Nach einem Neustart ist noch keine Runde gedreht. Ohne diesen Zeitpunkt
    /// las der Watchdog das als „Überwachung steht" und schlug Alarm — jedes
    /// Mal, wenn jemand das Add-on neu startet oder ein Update einspielt.
    /// </remarks>
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    private DateTime? _lastSnapshotRunUtc;
    private DateTime? _lastHaSuccessUtc;
    private string? _lastHaError;
    private string? _notifiedCode;

    /// <summary>Set after each completed snapshot-worker loop.</summary>
    public void MarkSnapshotRun(DateTime nowUtc)
    {
        lock (_gate) { _lastSnapshotRunUtc = nowUtc; }
    }

    /// <summary>Set whenever Home Assistant answered successfully.</summary>
    public void MarkHomeAssistantSuccess(DateTime nowUtc)
    {
        lock (_gate) { _lastHaSuccessUtc = nowUtc; _lastHaError = null; }
    }

    /// <summary>Set when a Home Assistant call failed, with a short reason.</summary>
    public void MarkHomeAssistantFailure(string reason)
    {
        lock (_gate) { _lastHaError = reason; }
    }

    public (DateTime? SnapshotRun, DateTime? HaSuccess, string? HaError) Read()
    {
        lock (_gate) { return (_lastSnapshotRunUtc, _lastHaSuccessUtc, _lastHaError); }
    }

    /// <summary>The problem code the user was last told about (null = last told all is well).</summary>
    public string? NotifiedCode
    {
        get { lock (_gate) { return _notifiedCode; } }
        set { lock (_gate) { _notifiedCode = value; } }
    }

    private readonly Dictionary<int, string> _pumpMeldungen = [];

    /// <summary>
    /// Welche Pumpen-Lage dem Betreiber je Zelt zuletzt gemeldet wurde.
    /// </summary>
    /// <remarks>
    /// Je Zelt getrennt, aus demselben Grund, aus dem der Watchdog die dunklen
    /// Zelte im Schlüssel führt: fällt im zweiten Zelt auch die Pumpe aus, ist
    /// das eine neue Lage und verdient eine eigene Nachricht, statt sich hinter
    /// der alten zu verstecken. Nur im Speicher — nach einem Neustart wird neu
    /// bewertet und im Zweifel einmal zu viel gewarnt.
    /// </remarks>
    public string? PumpMeldung(int tentId)
    {
        lock (_gate) { return _pumpMeldungen.TryGetValue(tentId, out var v) ? v : null; }
    }

    public void SetPumpMeldung(int tentId, string? schluessel)
    {
        lock (_gate)
        {
            if (schluessel is null) _pumpMeldungen.Remove(tentId);
            else _pumpMeldungen[tentId] = schluessel;
        }
    }
}
