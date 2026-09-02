using System.Globalization;
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;

public sealed class AuditRepository
{
    private readonly AppPaths _paths;

    public AuditRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public void Add(AuditEntry entry)
    {
        entry.CreatedAtUtc = DateTime.UtcNow;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO AuditEntries (GrowId, EntityType, EntityId, Action, Summary, CreatedAtUtc) VALUES ($growId, $entityType, $entityId, $action, $summary, $createdAtUtc);";
        command.Parameters.AddWithValue("$growId", entry.GrowId);
        command.Parameters.AddWithValue("$entityType", entry.EntityType);
        command.Parameters.AddWithValue("$entityId", (object?)entry.EntityId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", entry.Action);
        command.Parameters.AddWithValue("$summary", entry.Summary);
        command.Parameters.AddWithValue("$createdAtUtc", entry.CreatedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    /// <summary>Die Chronik eines Grows, das Neueste zuerst.</summary>
    /// <remarks>
    /// <para><b>Der Anlass (02.09.2026).</b> Diese Klasse hatte bis dahin genau
    /// eine öffentliche Methode: <see cref="Add"/>. Vier Controller schrieben
    /// hinein — Grows, Messungen, Journal, Abläufe —, und es gab einen Index
    /// (<c>IX_AuditEntries_GrowId_CreatedAtUtc</c>) für eine Abfrage, die
    /// niemand stellte.</para>
    ///
    /// <para>Die App sammelte damit seit Monaten die Geschichte jedes Grows,
    /// ohne dass jemand herankam. Ein Protokoll, das man nicht lesen kann, ist
    /// kein Protokoll, sondern Schreibarbeit bei jeder Änderung — und genau
    /// diese Zeilen beantworten „wann habe ich eigentlich geflippt".</para>
    /// </remarks>
    /// <param name="growId">Nur dieser Grow — die Chronik eines fremden gehört nicht dazu.</param>
    /// <param name="limit">
    /// Wie viele Zeilen höchstens. Eine Chronik wächst unbegrenzt; ohne Grenze
    /// wird die Antwort mit den Monaten still immer langsamer.
    /// </param>
    public IReadOnlyList<AuditEntry> GetForGrow(int growId, int limit = 200)
    {
        var sichereGrenze = Math.Clamp(limit, 1, 1000);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, GrowId, EntityType, EntityId, Action, Summary, CreatedAtUtc
            FROM AuditEntries
            WHERE GrowId = $growId
            ORDER BY CreatedAtUtc DESC, Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$growId", growId);
        command.Parameters.AddWithValue("$limit", sichereGrenze);

        var zeilen = new List<AuditEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            zeilen.Add(new AuditEntry
            {
                Id = Convert.ToInt32(reader["Id"], CultureInfo.InvariantCulture),
                GrowId = Convert.ToInt32(reader["GrowId"], CultureInfo.InvariantCulture),
                EntityType = reader["EntityType"]?.ToString() ?? string.Empty,
                EntityId = reader["EntityId"] is DBNull ? null
                    : Convert.ToInt32(reader["EntityId"], CultureInfo.InvariantCulture),
                Action = reader["Action"]?.ToString() ?? string.Empty,
                Summary = reader["Summary"]?.ToString() ?? string.Empty,
                CreatedAtUtc = DateTime.Parse(
                    reader["CreatedAtUtc"]?.ToString() ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }

        return zeilen;
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = _paths.DatabasePath };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}
