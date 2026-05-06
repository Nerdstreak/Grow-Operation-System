using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Api.Mapping;

public static class StrainPlantMapping
{
    public static StrainDto ToDto(this Strain strain) => new(
        Id: strain.Id,
        Name: strain.Name,
        Breeder: strain.Breeder,
        Dominance: strain.Dominance,
        FlowerWeeksMin: strain.FlowerWeeksMin,
        FlowerWeeksMax: strain.FlowerWeeksMax,
        Notes: strain.Notes,
        NutrientDemandFactor: strain.NutrientDemandFactor,
        StretchFactor: strain.StretchFactor,
        VpdPreferenceShift: strain.VpdPreferenceShift,
        CreatedAtUtc: strain.CreatedAtUtc,
        UpdatedAtUtc: strain.UpdatedAtUtc
    );

    public static Strain ToModel(this CreateStrainRequest request) => new()
    {
        Name = request.Name.Trim(),
        Breeder = Normalize(request.Breeder),
        Dominance = request.Dominance,
        FlowerWeeksMin = request.FlowerWeeksMin,
        FlowerWeeksMax = request.FlowerWeeksMax,
        Notes = Normalize(request.Notes),
        NutrientDemandFactor = request.NutrientDemandFactor,
        StretchFactor = request.StretchFactor,
        VpdPreferenceShift = request.VpdPreferenceShift
    };

    public static void ApplyTo(this UpdateStrainRequest request, Strain strain)
    {
        strain.Name = request.Name.Trim();
        strain.Breeder = Normalize(request.Breeder);
        strain.Dominance = request.Dominance;
        strain.FlowerWeeksMin = request.FlowerWeeksMin;
        strain.FlowerWeeksMax = request.FlowerWeeksMax;
        strain.Notes = Normalize(request.Notes);
        strain.NutrientDemandFactor = request.NutrientDemandFactor;
        strain.StretchFactor = request.StretchFactor;
        strain.VpdPreferenceShift = request.VpdPreferenceShift;
    }

    public static PlantInstanceDto ToDto(this PlantInstance plant) => new(
        Id: plant.Id,
        StrainId: plant.StrainId,
        SetupId: plant.SetupId,
        GrowId: plant.GrowId,
        ParentPlantId: plant.ParentPlantId,
        Label: plant.Label,
        PlantRole: plant.PlantRole,
        PlantStatus: plant.PlantStatus,
        PhenoLabel: plant.PhenoLabel,
        StartedAt: plant.StartedAt,
        EndedAt: plant.EndedAt,
        Notes: plant.Notes,
        StrainName: plant.StrainName,
        CreatedAtUtc: plant.CreatedAtUtc,
        UpdatedAtUtc: plant.UpdatedAtUtc
    );

    public static PlantInstance ToModel(this CreatePlantInstanceRequest request) => new()
    {
        StrainId = request.StrainId,
        SetupId = request.SetupId,
        GrowId = request.GrowId,
        ParentPlantId = request.ParentPlantId,
        Label = request.Label.Trim(),
        PlantRole = request.PlantRole,
        PlantStatus = request.PlantStatus,
        PhenoLabel = Normalize(request.PhenoLabel),
        StartedAt = request.StartedAt,
        EndedAt = request.EndedAt,
        Notes = Normalize(request.Notes)
    };

    public static void ApplyTo(this UpdatePlantInstanceRequest request, PlantInstance plant)
    {
        plant.StrainId = request.StrainId;
        plant.SetupId = request.SetupId;
        plant.GrowId = request.GrowId;
        plant.ParentPlantId = request.ParentPlantId;
        plant.Label = request.Label.Trim();
        plant.PlantRole = request.PlantRole;
        plant.PlantStatus = request.PlantStatus;
        plant.PhenoLabel = Normalize(request.PhenoLabel);
        plant.StartedAt = request.StartedAt;
        plant.EndedAt = request.EndedAt;
        plant.Notes = Normalize(request.Notes);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
