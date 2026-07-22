namespace GrowDiary.Web.Models;

/// <summary>
/// Converts between a tent's newline-separated camera-entity string and a clean list.
/// A tent can have several cameras; the first is mirrored to <c>CameraEntityId</c> for
/// backward compatibility (snapshot automation, camera-proxy default).
/// </summary>
public static class TentCameraList
{
    public static IReadOnlyList<string> Parse(string? cameraEntityIds, string? fallbackSingle)
    {
        var list = (cameraEntityIds ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(fallbackSingle))
        {
            list.Add(fallbackSingle.Trim());
        }

        return list;
    }

    /// <summary>Cleans a camera list to (newline-joined storage string, first entry) — both null when empty.</summary>
    public static (string? Ids, string? First) Serialize(IEnumerable<string>? cameras)
    {
        var clean = (cameras ?? Enumerable.Empty<string>())
            .Select(camera => camera?.Trim() ?? string.Empty)
            .Where(camera => camera.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return clean.Count == 0 ? (null, null) : (string.Join('\n', clean), clean[0]);
    }
}
