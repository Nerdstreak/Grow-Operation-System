namespace GrowDiary.Web.Models;

public enum GrowStatus
{
    Planning,
    Running,
    Completed,
    Aborted
}

public enum MediumType
{
    Hydro
}

public enum FeedingStyle
{
    None
}

public enum HydroStyle
{
    None,
    DWC,
    RDWC,
    NFT,
    Aeroponic,
    Other
}

public enum IrrigationType
{
    ActiveHydro
}

public enum WaterSource
{
    Tap,
    RO,
    Mixed
}

public enum GrowEnvironment
{
    Indoor,
    Outdoor,
    Greenhouse
}

public enum GrowStage
{
    Seedling,
    Clone,
    Veg,
    Transition,
    Flower,
    Finish,
    Dry,
    Cure
}

public enum ValueOrigin
{
    Manual,
    HomeAssistant,
    Imported,
    Derived
}

public enum JournalEntryType
{
    Note,
    Observation,
    Action,
    Problem,
    Solution,
    Training,
    Transplant,
    Feeding,
    ReservoirChange,
    GerminationConfirmed,
    CloneRooted,
    FlipToFlower
}

public enum GrowCounterState
{
    NoData,
    WaitingForGermination,
    WaitingForRooting,
    Vegetating,
    Flowering,
    Autoflowering
}

public enum GrowTaskStatus
{
    Open,
    Done,
    Skipped
}

public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}

public enum PhotoTag
{
    Overview,
    Canopy,
    Leaf,
    Root,
    Training,
    Flower,
    Problem,
    Comparison,
    Other
}

public enum SeedType
{
    Feminized,
    Autoflower,
    Regular
}

public enum StartMaterial
{
    Seed,
    Clone
}

public enum GerminationMethod
{
    PaperTowel,
    Rockwool,
    RapidRooter,
    DirectInSystem
}

public enum PropagationMedium
{
    Rockwool,
    Hydroton,
    RapidRooter,
    Neoprene
}

public enum GrowEntryPoint
{
    Germination,
    Seedling,
    Veg,
    Flower,
    Flush
}
