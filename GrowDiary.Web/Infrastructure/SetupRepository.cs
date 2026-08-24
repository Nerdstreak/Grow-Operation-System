using System.Globalization;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class SetupRepository : RepositoryBase
{
    public SetupRepository(AppPaths paths) : base(paths)
    {
    }

    public Setup CreateSetup(Setup setup)
    {
        ValidateSetupTentCompatibility(setup.TentId, setup.SetupType);

        setup.CreatedAtUtc = DateTime.UtcNow;
        setup.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Setups (
                TentId, Name, SetupType, Status, Notes,
                CloneCounterTotal, LastCloneCutAt, MotherHealthStatus,
                QuarantineStartedAt, QuarantinePlannedEndAt, QuarantineResult,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $tentId, $name, $setupType, $status, $notes,
                $cloneCounterTotal, $lastCloneCutAt, $motherHealthStatus,
                $quarantineStartedAt, $quarantinePlannedEndAt, $quarantineResult,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddSetupParameters(command, setup);
        setup.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return setup;
    }

    public Setup? GetSetup(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Setups WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapSetup(reader) : null;
    }

    public List<Setup> GetSetups()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM Setups
            ORDER BY CASE Status WHEN 'Active' THEN 0 WHEN 'Planning' THEN 1 ELSE 2 END, Name, Id;
        """;

        var list = new List<Setup>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapSetup(reader));
        }
        return list;
    }

    public List<Setup> GetSetupsForTent(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM Setups
            WHERE TentId = $tentId
            ORDER BY CASE Status WHEN 'Active' THEN 0 WHEN 'Planning' THEN 1 ELSE 2 END, Name, Id;
        """;
        command.Parameters.AddWithValue("$tentId", tentId);

        var list = new List<Setup>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapSetup(reader));
        }
        return list;
    }

    public void UpdateSetup(Setup setup)
    {
        ValidateSetupTentCompatibility(setup.TentId, setup.SetupType);

        setup.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Setups SET
                TentId = $tentId,
                Name = $name,
                SetupType = $setupType,
                Status = $status,
                Notes = $notes,
                CloneCounterTotal = $cloneCounterTotal,
                LastCloneCutAt = $lastCloneCutAt,
                MotherHealthStatus = $motherHealthStatus,
                QuarantineStartedAt = $quarantineStartedAt,
                QuarantinePlannedEndAt = $quarantinePlannedEndAt,
                QuarantineResult = $quarantineResult,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddSetupParameters(command, setup);
        command.Parameters.AddWithValue("$id", setup.Id);
        command.ExecuteNonQuery();
    }

    public Strain CreateStrain(Strain strain)
    {
        strain.CreatedAtUtc = DateTime.UtcNow;
        strain.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Strains (
                Name, Breeder, Dominance, FlowerWeeksMin, FlowerWeeksMax, Notes,
                NutrientDemandFactor, StretchFactor, VpdPreferenceShift,
                SeedKind, ThcPercent, CbdPercent, SativaPercent, Taste, Effect, Aroma,
                YieldIndoorGm2, HeightIndoorCm,
                CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $name, $breeder, $dominance, $flowerWeeksMin, $flowerWeeksMax, $notes,
                $nutrientDemandFactor, $stretchFactor, $vpdPreferenceShift,
                $seedKind, $thcPercent, $cbdPercent, $sativaPercent, $taste, $effect, $aroma,
                $yieldIndoorGm2, $heightIndoorCm,
                $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddStrainParameters(command, strain);
        strain.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return strain;
    }

    public void UpdateStrain(Strain strain)
    {
        strain.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Strains SET
                Name = $name,
                Breeder = $breeder,
                Dominance = $dominance,
                FlowerWeeksMin = $flowerWeeksMin,
                FlowerWeeksMax = $flowerWeeksMax,
                Notes = $notes,
                NutrientDemandFactor = $nutrientDemandFactor,
                StretchFactor = $stretchFactor,
                VpdPreferenceShift = $vpdPreferenceShift,
                SeedKind = $seedKind,
                ThcPercent = $thcPercent,
                CbdPercent = $cbdPercent,
                SativaPercent = $sativaPercent,
                Taste = $taste,
                Effect = $effect,
                Aroma = $aroma,
                YieldIndoorGm2 = $yieldIndoorGm2,
                HeightIndoorCm = $heightIndoorCm,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddStrainParameters(command, strain);
        command.Parameters.AddWithValue("$id", strain.Id);
        command.ExecuteNonQuery();
    }

    public Strain? GetStrain(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Strains WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapStrain(reader) : null;
    }

    public List<Strain> GetStrains()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Strains ORDER BY Name, Breeder, Id;";
        var list = new List<Strain>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapStrain(reader));
        }
        return list;
    }

    public PlantInstance CreatePlant(PlantInstance plant)
    {
        plant.CreatedAtUtc = DateTime.UtcNow;
        plant.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PlantInstances (
                StrainId, SetupId, GrowId, SiteIndex, ParentPlantId, Label, PlantRole, PlantStatus,
                PhenoLabel, StartedAt, EndedAt, Notes, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $strainId, $setupId, $growId, $siteIndex, $parentPlantId, $label, $plantRole, $plantStatus,
                $phenoLabel, $startedAt, $endedAt, $notes, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddPlantParameters(command, plant);
        plant.Id = Convert.ToInt32((long)command.ExecuteScalar()!);
        return GetPlant(plant.Id) ?? plant;
    }

    public PlantInstance CreateCloneFromMother(PlantInstance clone, int? motherSetupId, DateTime cutAt)
    {
        clone.CreatedAtUtc = DateTime.UtcNow;
        clone.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO PlantInstances (
                StrainId, SetupId, GrowId, SiteIndex, ParentPlantId, Label, PlantRole, PlantStatus,
                PhenoLabel, StartedAt, EndedAt, Notes, CreatedAtUtc, UpdatedAtUtc
            )
            VALUES (
                $strainId, $setupId, $growId, $siteIndex, $parentPlantId, $label, $plantRole, $plantStatus,
                $phenoLabel, $startedAt, $endedAt, $notes, $createdAtUtc, $updatedAtUtc
            );
            SELECT last_insert_rowid();
        """;
        AddPlantParameters(insertCommand, clone);
        clone.Id = Convert.ToInt32((long)insertCommand.ExecuteScalar()!);

        if (motherSetupId.HasValue)
        {
            using var setupCommand = connection.CreateCommand();
            setupCommand.Transaction = transaction;
            setupCommand.CommandText = """
                UPDATE Setups
                SET CloneCounterTotal = COALESCE(CloneCounterTotal, 0) + 1,
                    LastCloneCutAt = $cutAt,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $setupId AND SetupType = 'Mother';
            """;
            setupCommand.Parameters.AddWithValue("$cutAt", ToStorage(cutAt));
            setupCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
            setupCommand.Parameters.AddWithValue("$setupId", motherSetupId.Value);
            setupCommand.ExecuteNonQuery();
        }

        PlantInstance created;
        using (var getCommand = connection.CreateCommand())
        {
            getCommand.Transaction = transaction;
            getCommand.CommandText = """
                SELECT p.*, s.Name AS StrainName
                FROM PlantInstances p
                LEFT JOIN Strains s ON s.Id = p.StrainId
                WHERE p.Id = $id
                LIMIT 1;
            """;
            getCommand.Parameters.AddWithValue("$id", clone.Id);
            using var reader = getCommand.ExecuteReader();
            created = reader.Read() ? MapPlant(reader) : clone;
        }

        transaction.Commit();
        return created;
    }

    public PlantInstance DecideQuarantinePlant(PlantInstance plant, int quarantineSetupId, string quarantineResult)
    {
        plant.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var plantCommand = connection.CreateCommand();
        plantCommand.Transaction = transaction;
        plantCommand.CommandText = """
            UPDATE PlantInstances SET
                StrainId = $strainId,
                SetupId = $setupId,
                GrowId = $growId,
                SiteIndex = $siteIndex,
                ParentPlantId = $parentPlantId,
                Label = $label,
                PlantRole = $plantRole,
                PlantStatus = $plantStatus,
                PhenoLabel = $phenoLabel,
                StartedAt = $startedAt,
                EndedAt = $endedAt,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddPlantParameters(plantCommand, plant);
        plantCommand.Parameters.AddWithValue("$id", plant.Id);
        plantCommand.ExecuteNonQuery();

        using var setupCommand = connection.CreateCommand();
        setupCommand.Transaction = transaction;
        setupCommand.CommandText = """
            UPDATE Setups
            SET QuarantineResult = $quarantineResult,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $setupId AND SetupType = 'Quarantine';
        """;
        setupCommand.Parameters.AddWithValue("$quarantineResult", quarantineResult);
        setupCommand.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(DateTime.UtcNow));
        setupCommand.Parameters.AddWithValue("$setupId", quarantineSetupId);
        setupCommand.ExecuteNonQuery();

        PlantInstance updated;
        using (var getCommand = connection.CreateCommand())
        {
            getCommand.Transaction = transaction;
            getCommand.CommandText = """
                SELECT p.*, s.Name AS StrainName
                FROM PlantInstances p
                LEFT JOIN Strains s ON s.Id = p.StrainId
                WHERE p.Id = $id
                LIMIT 1;
            """;
            getCommand.Parameters.AddWithValue("$id", plant.Id);
            using var reader = getCommand.ExecuteReader();
            updated = reader.Read() ? MapPlant(reader) : plant;
        }

        transaction.Commit();
        return updated;
    }

    public void UpdatePlant(PlantInstance plant)
    {
        plant.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PlantInstances SET
                StrainId = $strainId,
                SetupId = $setupId,
                GrowId = $growId,
                SiteIndex = $siteIndex,
                ParentPlantId = $parentPlantId,
                Label = $label,
                PlantRole = $plantRole,
                PlantStatus = $plantStatus,
                PhenoLabel = $phenoLabel,
                StartedAt = $startedAt,
                EndedAt = $endedAt,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
        """;
        AddPlantParameters(command, plant);
        command.Parameters.AddWithValue("$id", plant.Id);
        command.ExecuteNonQuery();
    }

    public PlantInstance? GetPlant(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.*, s.Name AS StrainName
            FROM PlantInstances p
            LEFT JOIN Strains s ON s.Id = p.StrainId
            WHERE p.Id = $id
            LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapPlant(reader) : null;
    }

    public List<PlantInstance> GetPlants()
        => GetPlantsByWhere(string.Empty, null, null);

    public List<PlantInstance> GetPlantsBySetup(int setupId)
        => GetPlantsByWhere("WHERE p.SetupId = $setupId", setupId, null);

    public List<PlantInstance> GetPlantsByGrow(int growId)
        => GetPlantsByWhere("WHERE p.GrowId = $growId", null, growId);

    private List<PlantInstance> GetPlantsByWhere(string whereClause, int? setupId, int? growId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT p.*, s.Name AS StrainName
            FROM PlantInstances p
            LEFT JOIN Strains s ON s.Id = p.StrainId
            {whereClause}
            ORDER BY CASE p.PlantStatus WHEN 'Active' THEN 0 WHEN 'Planned' THEN 1 ELSE 2 END, p.Label, p.Id;
        """;

        if (setupId.HasValue)
        {
            command.Parameters.AddWithValue("$setupId", setupId.Value);
        }
        if (growId.HasValue)
        {
            command.Parameters.AddWithValue("$growId", growId.Value);
        }

        var plants = new List<PlantInstance>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            plants.Add(MapPlant(reader));
        }
        return plants;
    }

    private void ValidateSetupTentCompatibility(int tentId, SetupType setupType)
    {
        var tentType = GetTentType(tentId);
        if (!tentType.HasValue)
        {
            throw new InvalidOperationException($"Tent with id {tentId} does not exist.");
        }

        if (!SetupTentCompatibilityPolicy.IsCompatible(tentType.Value, setupType))
        {
            throw new InvalidOperationException($"Setup type {setupType} is not supported in tent type {tentType.Value}.");
        }
    }

    private TentType? GetTentType(int tentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT TentType FROM Tents WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", tentId);
        var raw = command.ExecuteScalar()?.ToString();
        return raw is null ? null : ParseEnum(raw, TentType.MultiPurpose);
    }

    private static Setup MapSetup(SqliteDataReader reader)
    {
        return new Setup
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            TentId = Convert.ToInt32(reader["TentId"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            SetupType = ParseEnum(reader["SetupType"]?.ToString(), SetupType.Production),
            Status = ParseEnum(reader["Status"]?.ToString(), SetupStatus.Planning),
            Notes = NullString(reader["Notes"]),
            CloneCounterTotal = reader["CloneCounterTotal"] is DBNull or null ? null : Convert.ToInt32(reader["CloneCounterTotal"], CultureInfo.InvariantCulture),
            LastCloneCutAt = ParseStoredDateTime(reader["LastCloneCutAt"]?.ToString()),
            MotherHealthStatus = NullString(reader["MotherHealthStatus"]),
            QuarantineStartedAt = ParseStoredDateTime(reader["QuarantineStartedAt"]?.ToString()),
            QuarantinePlannedEndAt = ParseStoredDateTime(reader["QuarantinePlannedEndAt"]?.ToString()),
            QuarantineResult = NullString(reader["QuarantineResult"]),
            CreatedAtUtc = ParseStoredUtcDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredUtcDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static Strain MapStrain(SqliteDataReader reader)
    {
        return new Strain
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            Name = reader["Name"]?.ToString() ?? string.Empty,
            Breeder = NullString(reader["Breeder"]),
            Dominance = ParseEnum(reader["Dominance"]?.ToString(), StrainDominance.Unknown),
            FlowerWeeksMin = reader["FlowerWeeksMin"] is DBNull or null ? null : Convert.ToInt32(reader["FlowerWeeksMin"], CultureInfo.InvariantCulture),
            FlowerWeeksMax = reader["FlowerWeeksMax"] is DBNull or null ? null : Convert.ToInt32(reader["FlowerWeeksMax"], CultureInfo.InvariantCulture),
            Notes = NullString(reader["Notes"]),
            NutrientDemandFactor = NullableDouble(reader["NutrientDemandFactor"]),
            StretchFactor = NullableDouble(reader["StretchFactor"]),
            VpdPreferenceShift = NullableDouble(reader["VpdPreferenceShift"]),
            SeedKind = HasColumn(reader, "SeedKind") && reader["SeedKind"] is not (DBNull or null) && Enum.TryParse<SeedKind>(reader["SeedKind"]?.ToString(), out var art) ? art : null,
            ThcPercent = HasColumn(reader, "ThcPercent") ? NullableDouble(reader["ThcPercent"]) : null,
            CbdPercent = HasColumn(reader, "CbdPercent") ? NullableDouble(reader["CbdPercent"]) : null,
            SativaPercent = HasColumn(reader, "SativaPercent") && reader["SativaPercent"] is not (DBNull or null) ? Convert.ToInt32(reader["SativaPercent"], CultureInfo.InvariantCulture) : null,
            Taste = HasColumn(reader, "Taste") ? NullString(reader["Taste"]) : null,
            Effect = HasColumn(reader, "Effect") ? NullString(reader["Effect"]) : null,
            Aroma = HasColumn(reader, "Aroma") ? NullString(reader["Aroma"]) : null,
            YieldIndoorGm2 = HasColumn(reader, "YieldIndoorGm2") && reader["YieldIndoorGm2"] is not (DBNull or null) ? Convert.ToInt32(reader["YieldIndoorGm2"], CultureInfo.InvariantCulture) : null,
            HeightIndoorCm = HasColumn(reader, "HeightIndoorCm") && reader["HeightIndoorCm"] is not (DBNull or null) ? Convert.ToInt32(reader["HeightIndoorCm"], CultureInfo.InvariantCulture) : null,
            CreatedAtUtc = ParseStoredUtcDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredUtcDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static PlantInstance MapPlant(SqliteDataReader reader)
    {
        return new PlantInstance
        {
            Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
            StrainId = reader["StrainId"] is DBNull or null ? null : Convert.ToInt32(reader["StrainId"], CultureInfo.InvariantCulture),
            SetupId = reader["SetupId"] is DBNull or null ? null : Convert.ToInt32(reader["SetupId"], CultureInfo.InvariantCulture),
            GrowId = reader["GrowId"] is DBNull or null ? null : Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
            SiteIndex = reader["SiteIndex"] is DBNull or null ? null : Convert.ToInt32(reader["SiteIndex"], CultureInfo.InvariantCulture),
            ParentPlantId = reader["ParentPlantId"] is DBNull or null ? null : Convert.ToInt32(reader["ParentPlantId"], CultureInfo.InvariantCulture),
            Label = reader["Label"]?.ToString() ?? string.Empty,
            PlantRole = ParseEnum(reader["PlantRole"]?.ToString(), PlantRole.Production),
            PlantStatus = ParseEnum(reader["PlantStatus"]?.ToString(), PlantStatus.Planned),
            PhenoLabel = NullString(reader["PhenoLabel"]),
            StartedAt = ParseStoredDateTime(reader["StartedAt"]?.ToString()),
            EndedAt = ParseStoredDateTime(reader["EndedAt"]?.ToString()),
            Notes = NullString(reader["Notes"]),
            StrainName = NullString(reader["StrainName"]),
            CreatedAtUtc = ParseStoredUtcDateTime(reader["CreatedAtUtc"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAtUtc = ParseStoredUtcDateTime(reader["UpdatedAtUtc"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static void AddSetupParameters(SqliteCommand command, Setup setup)
    {
        command.Parameters.AddWithValue("$tentId", setup.TentId);
        command.Parameters.AddWithValue("$name", setup.Name);
        command.Parameters.AddWithValue("$setupType", setup.SetupType.ToString());
        command.Parameters.AddWithValue("$status", setup.Status.ToString());
        command.Parameters.AddWithValue("$notes", (object?)setup.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$cloneCounterTotal", (object?)setup.CloneCounterTotal ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastCloneCutAt", setup.LastCloneCutAt.HasValue ? ToStorage(setup.LastCloneCutAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$motherHealthStatus", (object?)setup.MotherHealthStatus ?? DBNull.Value);
        command.Parameters.AddWithValue("$quarantineStartedAt", setup.QuarantineStartedAt.HasValue ? ToStorage(setup.QuarantineStartedAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$quarantinePlannedEndAt", setup.QuarantinePlannedEndAt.HasValue ? ToStorage(setup.QuarantinePlannedEndAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$quarantineResult", (object?)setup.QuarantineResult ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(setup.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(setup.UpdatedAtUtc));
    }

    private static void AddStrainParameters(SqliteCommand command, Strain strain)
    {
        command.Parameters.AddWithValue("$name", strain.Name);
        command.Parameters.AddWithValue("$breeder", (object?)strain.Breeder ?? DBNull.Value);
        command.Parameters.AddWithValue("$dominance", strain.Dominance.ToString());
        command.Parameters.AddWithValue("$flowerWeeksMin", (object?)strain.FlowerWeeksMin ?? DBNull.Value);
        command.Parameters.AddWithValue("$flowerWeeksMax", (object?)strain.FlowerWeeksMax ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)strain.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$nutrientDemandFactor", (object?)strain.NutrientDemandFactor ?? DBNull.Value);
        command.Parameters.AddWithValue("$stretchFactor", (object?)strain.StretchFactor ?? DBNull.Value);
        command.Parameters.AddWithValue("$vpdPreferenceShift", (object?)strain.VpdPreferenceShift ?? DBNull.Value);
        command.Parameters.AddWithValue("$seedKind", (object?)strain.SeedKind?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$thcPercent", (object?)strain.ThcPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$cbdPercent", (object?)strain.CbdPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$sativaPercent", (object?)strain.SativaPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$taste", (object?)strain.Taste ?? DBNull.Value);
        command.Parameters.AddWithValue("$effect", (object?)strain.Effect ?? DBNull.Value);
        command.Parameters.AddWithValue("$aroma", (object?)strain.Aroma ?? DBNull.Value);
        command.Parameters.AddWithValue("$yieldIndoorGm2", (object?)strain.YieldIndoorGm2 ?? DBNull.Value);
        command.Parameters.AddWithValue("$heightIndoorCm", (object?)strain.HeightIndoorCm ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(strain.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(strain.UpdatedAtUtc));
    }

    private static void AddPlantParameters(SqliteCommand command, PlantInstance plant)
    {
        command.Parameters.AddWithValue("$strainId", (object?)plant.StrainId ?? DBNull.Value);
        command.Parameters.AddWithValue("$setupId", (object?)plant.SetupId ?? DBNull.Value);
        command.Parameters.AddWithValue("$growId", (object?)plant.GrowId ?? DBNull.Value);
        command.Parameters.AddWithValue("$siteIndex", (object?)plant.SiteIndex ?? DBNull.Value);
        command.Parameters.AddWithValue("$parentPlantId", (object?)plant.ParentPlantId ?? DBNull.Value);
        command.Parameters.AddWithValue("$label", plant.Label);
        command.Parameters.AddWithValue("$plantRole", plant.PlantRole.ToString());
        command.Parameters.AddWithValue("$plantStatus", plant.PlantStatus.ToString());
        command.Parameters.AddWithValue("$phenoLabel", (object?)plant.PhenoLabel ?? DBNull.Value);
        command.Parameters.AddWithValue("$startedAt", plant.StartedAt.HasValue ? ToStorage(plant.StartedAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$endedAt", plant.EndedAt.HasValue ? ToStorage(plant.EndedAt.Value) : (object)DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)plant.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", ToStorageUtc(plant.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", ToStorageUtc(plant.UpdatedAtUtc));
    }
}
