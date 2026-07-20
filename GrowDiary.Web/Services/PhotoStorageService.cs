using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrowDiary.Web.Services;

public sealed class PhotoStorageService
{
    private const long MaxPhotoSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly AppPaths _paths;
    private readonly Infrastructure.GrowRepository _repository;

    public PhotoStorageService(AppPaths paths, Infrastructure.GrowRepository repository)
    {
        _paths = paths;
        _repository = repository;
    }

    public void ValidatePhotos(IEnumerable<IFormFile>? photos, ModelStateDictionary modelState, string key = "photos")
    {
        if (photos is null)
        {
            return;
        }

        foreach (var photo in photos)
        {
            if (photo.Length <= 0)
            {
                continue;
            }

            if (photo.Length > MaxPhotoSizeBytes)
            {
                modelState.AddModelError(key, "Fotos duerfen maximal 10 MB gross sein.");
            }

            var extension = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedPhotoExtensions.Contains(extension))
            {
                modelState.AddModelError(key, "Erlaubte Fotoformate sind JPG, PNG und WEBP.");
            }

            if (string.IsNullOrWhiteSpace(photo.ContentType)
                || !photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                modelState.AddModelError(key, "Uploads muessen Bilddateien sein.");
            }
        }
    }

    /// <summary>
    /// Persists raw camera snapshot bytes (e.g. from an auto-measurement trigger) as a
    /// grow photo so it shows up in the grow's photo timeline.
    /// </summary>
    public async Task<PhotoAsset> SaveSnapshotAsync(GrowRun grow, int? measurementId, byte[] bytes, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_paths.UploadRootPath, grow.Id.ToString());
        Directory.CreateDirectory(directory);

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.jpg";
        await File.WriteAllBytesAsync(Path.Combine(directory, fileName), bytes, cancellationToken);

        var saved = new PhotoAsset
        {
            GrowId = grow.Id,
            MeasurementId = measurementId,
            RelativePath = $"/uploads/{grow.Id}/{fileName}".Replace("\\", "/"),
            Caption = "Auto-Snapshot",
            Tag = PhotoTag.Overview,
            Source = ValueOrigin.HomeAssistant,
            TakenAtUtc = DateTime.UtcNow
        };

        _repository.AddPhoto(saved);
        return saved;
    }

    public async Task<List<PhotoAsset>> SaveMeasurementPhotosAsync(
        GrowRun grow,
        int measurementId,
        IEnumerable<IFormFile>? photos,
        PhotoTag tag,
        string? caption,
        bool useAsReferenceShot,
        ValueOrigin source)
    {
        var created = new List<PhotoAsset>();
        if (photos is null)
        {
            return created;
        }

        var directory = Path.Combine(_paths.UploadRootPath, grow.Id.ToString());
        Directory.CreateDirectory(directory);

        foreach (var photo in photos)
        {
            if (photo.Length <= 0)
            {
                continue;
            }

            var extension = Path.GetExtension(photo.FileName);
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{safeExtension}";
            var physicalPath = Path.Combine(directory, fileName);

            await using (var stream = File.Create(physicalPath))
            {
                await photo.CopyToAsync(stream);
            }

            var saved = new PhotoAsset
            {
                GrowId = grow.Id,
                MeasurementId = measurementId,
                RelativePath = $"/uploads/{grow.Id}/{fileName}".Replace("\\", "/"),
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                Tag = tag,
                Source = source,
                IsReferenceShot = useAsReferenceShot,
                TakenAtUtc = DateTime.UtcNow
            };

            _repository.AddPhoto(saved);
            created.Add(saved);
        }

        return created;
    }
}
