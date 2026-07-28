using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Zweikomponenten-Dünger: A und B im Verhältnis, aber nie gleichzeitig.
/// </summary>
/// <remarks>
/// <para>Warum getrennt: konzentriert fällt das Calcium aus Komponente A mit den
/// Sulfaten und Phosphaten aus B als Gips aus. Was ausgeflockt ist, kommt bei
/// der Pflanze nie an — im Becken schwimmen weisse Flocken, der EC steigt trotz
/// Dünger kaum, und der naheliegende Schluss („zu wenig gegeben") führt dazu,
/// dass noch mehr nachgekippt wird.</para>
///
/// <para>Deshalb: A läuft, die Trennzeit vergeht, dann B. Verdünnt im ganzen
/// Beckenvolumen ist die Begegnung harmlos — genau darum geht es bei der Zeit
/// dazwischen.</para>
/// </remarks>
public static class PartnerDosing
{
    /// <summary>Kleinste sinnvolle Trennzeit in Minuten.</summary>
    /// <remarks>
    /// Unter einer Minute ist es keine Trennung mehr, sondern zwei Pumpen, die
    /// praktisch zusammen laufen — der Fall, den das hier verhindern soll.
    /// </remarks>
    public const int MinDelayMinutes = 1;

    /// <summary>Was der Partner für diese Menge bekommt.</summary>
    /// <remarks>
    /// Null, wenn kein Partner eingerichtet ist oder das Verhältnis unbrauchbar
    /// ist. Lieber gar keine zweite Dosis als eine geratene: bei A ohne B fehlt
    /// ein Nährstoff, bei falschem Verhältnis stimmt das ganze Profil nicht.
    /// </remarks>
    public static double? PartnerMl(DosingPump pump, double dosedMl)
    {
        if (pump.PartnerPumpId is null or <= 0) return null;
        if (pump.PartnerRatio <= 0) return null;
        if (dosedMl <= 0) return null;

        return Math.Round(dosedMl * pump.PartnerRatio, 2);
    }

    /// <summary>Wann der Partner frühestens laufen darf.</summary>
    public static DateTime PartnerDueAt(DosingPump pump, DateTime dosedAtUtc)
        => dosedAtUtc.AddMinutes(Math.Max(pump.PartnerDelayMinutes, MinDelayMinutes));

    /// <summary>
    /// Darf diese Pumpe jetzt laufen, oder wartet noch ein Partner auf sie?
    /// </summary>
    /// <remarks>
    /// Der Riegel gegen die eigentliche Gefahr: solange für <b>eine der beiden</b>
    /// Pumpen des Paares noch etwas aussteht, darf keine von beiden erneut
    /// starten. Sonst gäbe eine zweite Anforderung A ein zweites Mal, während
    /// das erste B noch wartet — und irgendwann treffen sich zwei frische Dosen
    /// doch.
    /// </remarks>
    public static bool IsBlockedByPending(IReadOnlyList<PendingDose> pendingForPair)
        => pendingForPair.Count > 0;

    /// <summary>
    /// Prüft die Einrichtung eines Paares.
    /// </summary>
    /// <returns>Ein Klartext-Fehler, oder null wenn es passt.</returns>
    public static string? Validate(DosingPump pump, DosingPump? partner)
    {
        if (pump.PartnerPumpId is null) return null;

        if (partner is null)
        {
            return "Die Partnerpumpe existiert nicht.";
        }

        if (partner.Id == pump.Id)
        {
            return "Eine Pumpe kann nicht ihr eigener Partner sein.";
        }

        if (partner.TentId != pump.TentId)
        {
            // Zwei Becken, ein Paar — dann liefe B in ein anderes Reservoir als A.
            return "Partnerpumpen müssen im selben Zelt sein.";
        }

        if (pump.PartnerRatio <= 0)
        {
            return "Das Verhältnis muss über null liegen.";
        }

        if (pump.PartnerDelayMinutes < MinDelayMinutes)
        {
            return $"Die Trennzeit muss mindestens {MinDelayMinutes} Minute betragen — sonst treffen sich A und B konzentriert.";
        }

        return null;
    }
}
