using System.Collections.Concurrent;

namespace GrowDiary.Web.Infrastructure;

public sealed record CameraFrame(byte[] Bytes, string ContentType, DateTimeOffset CapturedAt);

/// <summary>
/// Keeps the last *valid* camera frame per camera entity in memory so the UI
/// always has a recent grow image even when Home Assistant momentarily fails
/// (timeouts, open circuit, empty/garbage responses).
/// </summary>
public sealed class CameraFrameCache
{
    private readonly ConcurrentDictionary<string, CameraFrame> _frames = new(StringComparer.Ordinal);

    public CameraFrame? Get(string entityId) =>
        _frames.TryGetValue(entityId, out var frame) ? frame : null;

    public void Set(string entityId, CameraFrame frame) => _frames[entityId] = frame;

    /// <summary>
    /// Validates that the bytes look like a real image (magic numbers + sane size),
    /// so we never cache or serve an error page / empty body as a "camera image".
    /// </summary>
    public static bool IsLikelyImage(byte[] bytes, string? contentType)
    {
        if (bytes is null || bytes.Length < 256)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(contentType) && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // JPEG
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;
        // PNG
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;
        // GIF
        if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return true;
        // WEBP (RIFF....WEBP)
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return true;
        // BMP
        if (bytes[0] == 0x42 && bytes[1] == 0x4D) return true;

        // Unknown magic but the server explicitly declared an image content type.
        return !string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}
