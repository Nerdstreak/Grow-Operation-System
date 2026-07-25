using System.Text.Json.Serialization;

namespace GrowDiary.Web.Models;

/// <summary>What a tile shows: a value Grow OS knows, or any Home Assistant entity.</summary>
public enum DashboardTileKind
{
    /// <summary>One of Grow OS's own metric keys (temperature, reservoir-ph, …).</summary>
    Metric,
    /// <summary>Any Home Assistant entity — including ones Grow OS knows nothing about.</summary>
    Entity,
    /// <summary>A camera entity, shown as a refreshing still image.</summary>
    Camera
}

public sealed class DashboardTile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DashboardTileKind Kind { get; set; } = DashboardTileKind.Metric;

    /// <summary>Set for <see cref="DashboardTileKind.Metric"/>.</summary>
    public string? MetricKey { get; set; }

    /// <summary>Set for <see cref="DashboardTileKind.Entity"/>.</summary>
    public string? EntityId { get; set; }

    /// <summary>Overrides the automatic caption; null keeps the default label.</summary>
    public string? Label { get; set; }

    /// <summary>Overrides the unit reported by Home Assistant.</summary>
    public string? Unit { get; set; }

    /// <summary>
    /// How many of the three grid columns the tile occupies. A value tile is fine at 1;
    /// a camera needs the room.
    /// </summary>
    public int Span { get; set; } = 1;
}

public sealed class DashboardSection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "Bereich";
    public List<DashboardTile> Tiles { get; set; } = [];
}

/// <summary>A tent's dashboard arrangement. Absent = the built-in default is used.</summary>
public sealed class DashboardLayout
{
    public int TentId { get; set; }
    public List<DashboardSection> Sections { get; set; } = [];

    [JsonIgnore]
    public bool IsEmpty => Sections.Count == 0 || Sections.All(section => section.Tiles.Count == 0);

    /// <summary>
    /// What Grow OS shows out of the box — the same climate and reservoir grouping the
    /// dashboard always had, now as an editable starting point rather than fixed code.
    /// </summary>
    public static DashboardLayout Default(int tentId) => new()
    {
        TentId = tentId,
        Sections =
        [
            new DashboardSection
            {
                Id = "climate",
                Title = "Klima",
                Tiles = Metrics("temperature", "humidity", "vpd", "light-cycle", "ppfd", "co2"),
            },
            new DashboardSection
            {
                Id = "reservoir",
                Title = "Reservoir",
                Tiles = Metrics("reservoir-ph", "reservoir-ec", "reservoir-temp", "reservoir-level", "reservoir-level-cm", "orp", "dissolved-oxygen"),
            },
        ],
    };

    private static List<DashboardTile> Metrics(params string[] keys) => keys
        .Select(key => new DashboardTile { Id = key, Kind = DashboardTileKind.Metric, MetricKey = key })
        .ToList();
}
