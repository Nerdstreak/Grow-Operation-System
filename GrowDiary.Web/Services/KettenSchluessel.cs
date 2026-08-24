namespace GrowDiary.Web.Services;

/// <summary>
/// Die maschinenlesbaren Schlüssel der Steuerungs-Kette — einer je Glied.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass.</b> Ein Nutzer stand vor der Crop-Steering-Seite und
/// wusste nicht, wie er die Steuerung anschalten soll. Die Kette SAGTE zwar,
/// was fehlt („Der Schalter unten steht auf aus"), aber sie FÜHRTE nicht hin —
/// der Schalter, die Untergrenze und das Zielgerät liegen verstreut weiter
/// unten, der Flip auf einer anderen Seite.</para>
///
/// <para><b>Warum Schlüssel und nicht Titel.</b> Die Oberfläche hängt an jedes
/// gerissene Glied einen Knopf, der zur richtigen Stelle springt. Hinge diese
/// Zuordnung am deutschen Titel, wäre sie beim nächsten Umformulieren still
/// tot — genau die Falle „eine Erwähnung ist keine Verwendung". Der Schlüssel
/// ist die stabile Kennung; <c>ketten-aktionen.node.test.ts</c> zählt, dass
/// die Oberfläche jeden kennt.</para>
/// </remarks>
public static class KettenSchluessel
{
    /// <summary>Der Nachtabsenkungs-Schalter am Grow steht auf aus.</summary>
    public const string Absenkung = "absenkung";

    /// <summary>Kein Zielgerät (climate/number) eingetragen.</summary>
    public const string Zielgeraet = "zielgeraet";

    /// <summary>Die Kühler-Steuerung am Zelt ist aus.</summary>
    public const string KuehlerSteuerung = "kuehler-steuerung";

    /// <summary>Keine Steckdose am Zelt eingetragen.</summary>
    public const string Steckdose = "steckdose";

    /// <summary>Home Assistant nicht verbunden (oder Testbetrieb).</summary>
    public const string Verbindung = "verbindung";

    /// <summary>Der Plan steht — nichts zu tun.</summary>
    public const string PlanSteht = "plan-steht";

    // ---- Die Gründe, aus denen der PLAN leer sein kann. Sie entstehen in
    // ---- NachtabsenkungService.Rechnen; jede Leer-Stelle nennt ihren.

    /// <summary>Die Absenkung ist aus (nur ohne Vorschau sichtbar).</summary>
    public const string PlanAbgeschaltet = "plan-abgeschaltet";

    /// <summary>Das Profil hat keine Blüte-Wassertemperaturen.</summary>
    public const string PlanOhneProfil = "plan-ohne-profil";

    /// <summary>Die Untergrenze liegt über dem Blüte-Nachtwert.</summary>
    public const string PlanUntergrenzeZuHoch = "plan-untergrenze-zu-hoch";

    /// <summary>Noch keine Blüte — der Flip ist nicht eingetragen.</summary>
    public const string PlanVorDemFlip = "plan-vor-dem-flip";
}
