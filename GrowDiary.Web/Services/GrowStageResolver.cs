using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

/// <summary>
/// Welche Phase ein Grow HEUTE hat — aus dem Grow selbst, ohne dass jemand
/// gemessen haben muss.
/// </summary>
/// <remarks>
/// Vorher kam die Phase aus der letzten erfassten Messung. Wer noch nie von
/// Hand gemessen hatte, bekam deshalb auf dem ganzen Live-Bildschirm keinen
/// einzigen Zielbereich: keine Farbe, kein „im Ziel", kein „daneben" — obwohl
/// die Sensoren live lieferten und der Grow seinen Stand genau kennt. Der
/// Bildschirm zeigt oben „Veg · Tag 7" und darunter graue Kacheln; dieselbe
/// Auskunft zweimal, einmal richtig und einmal gar nicht.
///
/// Die Reihenfolge folgt dem, was der Nutzer festgehalten hat, nicht dem
/// Kalender: ein eingetragener Flip schlägt jede Rechnung, ein geplanter Flip
/// schlägt den Einstiegspunkt. Geraten wird nichts — wo nichts feststeht,
/// bleibt es beim Einstiegspunkt des Grows.
/// </remarks>
public static class GrowStageResolver
{
    /// <summary>
    /// Ab wann die Blüte als „Finish" gilt: die letzten zwei Wochen, in denen
    /// gespült wird und die Zielwerte andere sind.
    /// </summary>
    public const int FinishDaysBeforeHarvest = 14;

    /// <summary>Die ersten Tage nach dem Flip sind Übergang, noch nicht volle Blüte.</summary>
    public const int TransitionDays = 10;

    /// <summary>Solange eine Sämlingsphase dauert, wenn kein anderes Datum widerspricht.</summary>
    public const int SeedlingDays = 14;

    public static GrowStage Resolve(GrowRun grow, DateTime today)
    {
        var heute = today.Date;

        // 1. Geflippt ist geflippt — das Datum steht, da wird nichts gerechnet.
        if (grow.FlipDate is { } flip && heute >= flip.Date)
        {
            return FlowerStageFor(grow, flip.Date, heute);
        }

        // 2. Autoflower kennt keinen Flip. Sie geht nach Tagen seit der Keimung
        //    in die Blüte; der Richtwert steht im Grow, sonst 28 Tage.
        if (grow.SeedType == SeedType.Autoflower)
        {
            var keim = grow.GerminatedAt?.Date ?? grow.StartDate.Date;
            var tage = (heute - keim).Days + (grow.AutoflowerDaysSinceGermination ?? 0);
            if (tage >= 28) return FlowerStageFor(grow, keim.AddDays(28), heute);
            return SeedlingOrVeg(grow, heute, tage);
        }

        // 3. Noch nicht geflippt: hat der Nutzer eine Veg-Dauer geplant, ist der
        //    Flip-Termin bekannt — der Grow ist bis dahin vegetativ.
        var vegStart = grow.RootedAt?.Date ?? grow.GerminatedAt?.Date ?? grow.StartDate.Date;

        // 4. Vor Keimung/Bewurzelung: Sämling bzw. Klon.
        if (grow.StartMaterial == StartMaterial.Clone && !grow.CloneIsRooted && grow.RootedAt is null)
        {
            return GrowStage.Clone;
        }

        if (grow.StartMaterial == StartMaterial.Seed && grow.GerminatedAt is null)
        {
            // Ohne Keimdatum zählt der Einstiegspunkt: wer mitten im Lauf
            // einsteigt, hat nichts zu keimen.
            //
            // Für Keimung/Sämling wird NICHT einfach „Sämling" zurückgegeben:
            // das galt sonst ewig. Wer das Keimdatum nie eingetragen hat — und
            // das ist der Normalfall — steckte nach drei Monaten immer noch in
            // Sämlings-Zielen, mit einem EC, den eine ausgewachsene Pflanze
            // längst überholt hat. Stattdessen wird ab dem Startdatum
            // geschätzt, genau wie mit Keimdatum; nur der Anker ist ein anderer.
            switch (grow.EntryPoint)
            {
                case GrowEntryPoint.Veg:
                    return GrowStage.Veg;
                case GrowEntryPoint.Flower:
                    return GrowStage.Flower;
                case GrowEntryPoint.Flush:
                    return GrowStage.Finish;
            }
        }

        // 5. Die ersten Tage Sämling, danach Veg — eine Schätzung, kein Befund.
        //    Der echte Übergang hängt am Aussehen (echte gezackte Blätter statt
        //    Keimblättern, dickerer Stängel, regelmäßig neue Blattpaare), und
        //    der steht oben in VegStartedAt, sobald jemand hingesehen hat.
        //    Typisch sind ein bis drei Wochen; SeedlingDays liegt in der Mitte.
        var seitStart = (heute - vegStart).Days + (grow.DaysAlreadyInPhase ?? 0);
        return grow.EntryPoint is GrowEntryPoint.Germination or GrowEntryPoint.Seedling
            ? SeedlingOrVeg(grow, heute, seitStart)
            : GrowStage.Veg;
    }

    /// <summary>
    /// Sämling oder schon Veg — die einzige Stelle, an der das entschieden wird.
    /// </summary>
    /// <remarks>
    /// <para>Hat jemand den Übergang festgehalten, gilt der, und zwar in beide
    /// Richtungen: davor Sämling, ab dann Veg. Der Wechsel hängt nicht am
    /// Kalender, sondern am Aussehen — echte gezackte Blätter statt der zwei
    /// runden Keimblätter, dickerer Stängel, regelmäßig neue Blattpaare,
    /// Seitentriebe an den Knoten, spürbar mehr Wasserverbrauch. Wer davorsteht,
    /// weiss es besser als jede Tagesrechnung.</para>
    ///
    /// <para>Ohne Eintrag wird geschätzt. Typisch ist ein bis drei Wochen nach
    /// der Keimung; <see cref="SeedlingDays"/> liegt in der Mitte.</para>
    ///
    /// <para>Bewusst NICHT ganz oben in <see cref="Resolve"/>: dort hätte der
    /// Eintrag auch eine blühende Autoflower zurück in die Veg gezogen. Er
    /// entscheidet nur dort, wo Sämling und Veg wirklich auseinandergehen.</para>
    /// </remarks>
    private static GrowStage SeedlingOrVeg(GrowRun grow, DateTime heute, int tageSeitStart)
    {
        if (grow.VegStartedAt is { } vegAb)
        {
            return heute >= vegAb.Date ? GrowStage.Veg : GrowStage.Seedling;
        }

        return tageSeitStart < SeedlingDays ? GrowStage.Seedling : GrowStage.Veg;
    }

    /// <summary>Übergang, Blüte oder Finish — je nachdem, wie weit nach dem Flip.</summary>
    private static GrowStage FlowerStageFor(GrowRun grow, DateTime flip, DateTime heute)
    {
        var tageInBluete = (heute - flip).Days;
        if (tageInBluete < TransitionDays)
        {
            return GrowStage.Transition;
        }

        // Das Ende der Blüte kommt aus den Breeder-Wochen. Ohne sie wird nicht
        // geraten — dann bleibt es bei „Flower", und Finish setzt der Nutzer
        // selbst per Messung.
        var wochen = grow.BreederFlowerWeeksMax ?? grow.BreederFlowerWeeksMin;
        if (wochen is { } w && w > 0)
        {
            var ernte = flip.AddDays(w * 7);
            if (heute >= ernte.AddDays(-FinishDaysBeforeHarvest))
            {
                return GrowStage.Finish;
            }
        }

        return GrowStage.Flower;
    }
}
