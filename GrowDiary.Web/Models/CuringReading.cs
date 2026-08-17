namespace GrowDiary.Web.Models;

/// <summary>Woher der Feuchtewert stammt.</summary>
public enum CuringReadingSource
{
    /// <summary>Vom Hygrometer abgelesen und eingetippt.</summary>
    Manual,

    /// <summary>Von einem Home-Assistant-Sensor im Glas geholt.</summary>
    HomeAssistant,
}

/// <summary>
/// Eine Ablesung am Glas — Feuchte, Lüften, oder beides.
/// </summary>
/// <remarks>
/// <para>Beim Aushärten fallen zwei Dinge zusammen, die man trotzdem
/// auseinanderhalten muss: was das Hygrometer zeigt, und ob gelüftet wurde. Wer
/// lüftet, ohne abzulesen, hat den Rhythmus eingehalten aber nichts gelernt;
/// wer abliest, ohne zu lüften, weiß Bescheid und hat nichts getan. Beide Felder
/// sind deshalb einzeln erlaubt.</para>
///
/// <para>Die Herkunft steht dabei, wie bei den Messwerten seit beta.26: ein
/// Handwert von gestern ist etwas anderes als ein Sensorwert von vor fünf
/// Minuten, und wer das nicht sieht, hält beides für gleich sicher.</para>
/// </remarks>
public sealed class CuringReading
{
    public int Id { get; set; }

    public int JarId { get; set; }

    public DateTime ReadAtUtc { get; set; }

    /// <summary>Was das Hygrometer im Glas zeigt, in Prozent relativer Feuchte.</summary>
    public double? HumidityPercent { get; set; }

    /// <summary>Wie lange gelüftet wurde, in Minuten. <c>null</c> = nicht gelüftet.</summary>
    public int? BurpedMinutes { get; set; }

    public string? Note { get; set; }

    public CuringReadingSource Source { get; set; } = CuringReadingSource.Manual;

    public DateTime CreatedAtUtc { get; set; }
}
