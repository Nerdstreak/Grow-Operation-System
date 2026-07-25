namespace GrowDiary.Web.Services;

/// <summary>
/// The pulse of the background machinery, so the watchdog can tell "everything is quiet
/// because all is well" from "everything is quiet because I stopped working". Held in
/// memory on purpose: after a restart the slate is clean and the next check re-evaluates.
/// </summary>
public sealed class SystemHeartbeat
{
    private readonly object _gate = new();

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
}
