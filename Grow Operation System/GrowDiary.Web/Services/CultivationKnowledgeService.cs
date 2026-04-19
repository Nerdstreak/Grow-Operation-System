using GrowDiary.Web.Models;

namespace GrowDiary.Web.Services;

public sealed class CultivationKnowledgeService
{
    private readonly List<NutrientProgram> _programs;
    private readonly List<MediumPlaybook> _mediumPlaybooks;

    public CultivationKnowledgeService()
    {
        _programs = BuildPrograms();
        _mediumPlaybooks = BuildMediumPlaybooks();
    }

    public IReadOnlyList<NutrientProgram> GetPrograms() => _programs;
    public IReadOnlyList<MediumPlaybook> GetMediumPlaybooks() => _mediumPlaybooks;

    public NutrientProgram? MatchProgram(string? nutrientText)
        => _programs.FirstOrDefault(x => x.Matches(nutrientText));

    public IReadOnlyList<string> GetProgramNames()
        => _programs.Select(x => x.Name).ToList();

    public IReadOnlyList<string> GetHydroProgramNames()
        => _programs.Select(p => p.Name).ToList();

    private static List<NutrientProgram> BuildPrograms()
    {
        return new List<NutrientProgram>
        {
            new()
            {
                Key = "athena",
                Name = "Athena Blended",
                Manufacturer = "Athena",
                Category = "Mineralisch • RDWC/DWC/Coco",
                Summary = "Athena Blended muss nach Systemtyp gelesen werden: Coco/Fertigation kann recht aggressiv gefahren werden, dein RDWC-Material und Athenas eigene RDWC-Prozedur sind für rezirkulierendes Hydro aber bewusst konservativer und trendbasiert aufgebaut.",
                BestFor = "Coco/Fertigation, Rockwool, saubere RDWC/DWC-Systeme mit RO/VE-Wasser",
                WaterGuidance = "Für RDWC sauber mit RO/VE-Wasser starten, wöchentlich Changeouts einplanen, Addbacks dokumentieren und Wasserstand plus EC/pH gemeinsam lesen.",
                PhGuidance = "RDWC-Zielbereich 5,8–6,2. Laut SKX-/Athena-Material erst hart korrigieren, wenn du Richtung 5,5 oder 6,5 driftest. In der späten Blüte darf der pH innerhalb der Safe-Zone natürlicher arbeiten.",
                EcGuidance = "Athena-Coco/Fertigation kann in Veg grob um 2,1 EC Input liegen. Im RDWC-Profil startet die Linie dagegen sehr niedrig und endet meist nur bei etwa 1,5–1,6 EC in der späten Blüte. DWC benötigt oft 30–70 % mehr EC als RDWC.",
                ScheduleStyle = "A/B-Basen plus Addback-Logik, Changeout-Rhythmus und Cleanse/HOCl-Steuerung über ORP",
                OfficialHighlights = "Athena verweist offiziell auf Wasserqualität, Temperatur und Addback-Management als Haupttreiber für pH-/EC-Schwankungen. Das RDWC-Procedure arbeitet mit wöchentlichen Changeouts, einem separaten Addback-Prozess und phasenabhängig dosiertem Cleanse statt blindem Nachkippen.",
                PracticeNotes = "Für RDWC nicht das fertigationartige Athena-Coco-Schema kopieren. Besser: konservatives Reservoir, tägliche Checks von pH/EC/Temp/ORP/DO, Addback dokumentieren und nur über Trends eskalieren.",
                SearchTerms = new() { "athena", "athena blended", "grow a", "grow b", "bloom a", "bloom b", "balance", "cleanse" },
                Stages = new()
                {
                    new() { Stage = "Clone / frisch integriert", Dose = "sehr sanfter Start", Target = "EC ca. 0,2–0,4", Notes = "Keine Transplantation in zu kaltes Wasser; System möglichst nicht unter ca. 18,9 °C." },
                    new() { Stage = "frühe Veg", Dose = "A/B sehr konservativ", Target = "EC ca. 0,3–0,6", Notes = "Wurzeln etablieren, pH in der Safe-Zone lassen, nicht hektisch nachregeln." },
                    new() { Stage = "mittlere / späte Veg", Dose = "A/B abgestuft erhöhen", Target = "EC ca. 0,6–1,2", Notes = "Nach 3 Veg-Wochen ist laut RDWC-Procedure ein Partial-Changeout ein klassischer Kontrollpunkt." },
                    new() { Stage = "Transition / Stretch", Dose = "Bloom A/B einführen, Addback sauber lesen", Target = "EC ca. 1,2–1,4", Notes = "Bei längerer Veg vor dem Flip eher Full-Changeout einplanen." },
                    new() { Stage = "späte Blüte", Dose = "stabil, nicht überziehen", Target = "EC ca. 1,4–1,6", Notes = "Strainabhängig: sativalastig oft 1,0–1,4, indica-lastig teils 1,4–1,8 tolerierbar." },
                    new() { Stage = "Finish", Dose = "EC herunterfahren", Target = "EC ca. 0,4–0,6", Notes = "4–10 Tage Finish/Flush je nach Reifegrad und Ziel." },
                    new() { Stage = "Cleanse / HOCl", Dose = "Initial / Changeout 10,6 mL pro 10 L; Veg W1 0,4; Veg W2-3 0,5; Veg W4 0,7; Flower/Finish 2,1 mL pro 10 L pro Tag", Target = "ORP meist 350–450 mV", Notes = "Nicht überdosieren: zu hoher ORP kann ORP-Schock mit gelblichem, trocken-krustigem Laub triggern." }
                },
                Tips = new()
                {
                    "RDWC mit Athena ist ein Trend-System: Wasserstand, pH und EC immer zusammen lesen, nicht isoliert.",
                    "Steigen pH und EC gleichzeitig, ist das laut Addback-Logik ein Stresssignal - nicht einfach weiter hochfüttern.",
                    "Wenn ORP unter 350 mV fällt, eher kleine HOCl-/Cleanse-Korrekturen und Re-Check nach 48 h statt Schockbehandlung.",
                    "HOCl und H2O2 nicht gleichzeitig fahren; die SOPs behandeln das ausdrücklich als schlechte Kombination.",
                    "DWC darf deutlich höher liegen als RDWC. Erst +30 % testen und nur bei echtem Hunger weiter steigern.",
                    "Wenn du Athena in RDWC fährst, ist die Systemart wichtiger als der Markenname: RDWC ≠ Coco-Chart.",
                    "Stark steigender pH und steigender EC zusammen sind in rezirkulierenden Systemen ein ernstes Warnsignal."
                }
            },
            new()
            {
                Key = "hydro-research-vbx",
                Name = "Hydroponic Research VBX + Shine",
                Manufacturer = "Hydroponic Research",
                Category = "Mineralisch • All-in-one / Bloom Additive",
                Summary = "VBX ist als einfache Pulverbasis gedacht, Shine kommt ab der frühen Blüte dazu. Das offizielle Chart ist klar phasenbasiert und deutlich moderater als manche aggressiven Community-Runs.",
                BestFor = "Hydro, Coco, Substratläufe mit einfacher Pulverbasis unter LED",
                WaterGuidance = "Sauberes Wasser, Reservoir wöchentlich wechseln und die Chart-EC als Orientierung nutzen statt frei zu schätzen.",
                PhGuidance = "Im offiziellen Chart konstant um pH 5,8 geführt.",
                EcGuidance = "Offizielles VBX/SHINE-Chart: Seedling/Clone ca. 1,0 EC, Early Veg 1,3, Late Veg 1,4, Early Flower 1,6, Mid/Late Flower 1,8, Ripening/Flush etwa 0,6.",
                ScheduleStyle = "eine Pulverbasis plus Shine als Bloom-Additiv in einem klaren Stufenmodell",
                OfficialHighlights = "Das aktuelle VBX-/Shine-Chart fährt 3/6/10/12 g VBX pro 10 L in der frühen Entwicklung bis frühen Blüte und ergänzt Shine mit 2,5 g pro 10 L ab der frühen Blüte. Das Chart nennt explizit einen wöchentlichen Reservoirwechsel.",
                PracticeNotes = "Wenn du VBX vergleichst, ist die Phase wichtig. Ein EC, der in Veg zu hoch wäre, kann in Mid Flower noch im offiziellen Rahmen liegen - oder umgekehrt. Deshalb nutzt die App für VBX einen eigenen Zielkorridor.",
                SearchTerms = new() { "hydroponic research", "vbx", "shine", "veg+bloom" },
                Stages = new()
                {
                    new() { Stage = "Seedling / Clone", Dose = "VBX 3 g / 10 L", Target = "EC ca. 1,0", Notes = "Shine noch nicht einsetzen." },
                    new() { Stage = "Early Veg", Dose = "VBX 6 g / 10 L", Target = "EC ca. 1,3", Notes = "sanfte Aufbauphase" },
                    new() { Stage = "Late Veg", Dose = "VBX 10 g / 10 L", Target = "EC ca. 1,4", Notes = "kräftige, aber noch kontrollierte Veg" },
                    new() { Stage = "Early Flower", Dose = "VBX 12 g + Shine 2,5 g / 10 L", Target = "EC ca. 1,6", Notes = "Shine-Start in der frühen Blüte" },
                    new() { Stage = "Mid / Late Flower", Dose = "VBX 12 g + Shine 2,5 g / 10 L", Target = "EC ca. 1,8", Notes = "stabile Hauptblüte" },
                    new() { Stage = "Ripening / Flush", Dose = "VBX 0–2,5 g + Shine 0–2,5 g / 10 L je nach Phase", Target = "EC ca. 0,6", Notes = "Reservoir weiter wöchentlich erneuern" }
                },
                Tips = new()
                {
                    "VBX ist kein Freifahrtschein für 2,2+ EC in jeder Veg - das aktuelle offizielle Chart liegt deutlich darunter.",
                    "Shine ist ein Blüte-Additiv, kein permanenter Veg-Bestandteil.",
                    "Wenn du bewusste Abweichungen vom Chart fährst, dokumentiere sie in der App mit Grund und Pflanzenreaktion.",
                    "Wenn du VBX fährst, sind hohe Veg-ECs nicht automatisch falsch – das Chart ist phasenabhängig und nicht identisch mit konservativen RDWC-Plänen.",
                    "Zum Finish ist das kontrollierte Absenken Teil des Programms und kein Zufall."
                }
            },
            new()
            {
                Key = "canna-aqua",
                Name = "Canna Aqua",
                Manufacturer = "Canna",
                Category = "Mineralisch • RDWC/DWC/NFT/Rezirkulierend",
                Summary = "Canna Aqua Vega und Aqua Flores sind speziell für geschlossene rezirkulierende Hydrosysteme entwickelt. Das System arbeitet mit einem engeren pH-Fenster als Soil-Linien und legt Wert auf saubere Wasserqualität und regelmäßige Wechsel.",
                BestFor = "RDWC, DWC, NFT, geschlossene Hydroponics-Systeme",
                WaterGuidance = "Sauberes Wasser ist Voraussetzung. Wöchentliche Wechsel für stabile Nährstoffbalance empfohlen. Mit Canna Rhizotonic in der Bewurzelungsphase kombinieren.",
                PhGuidance = "Canna Aqua zielt auf pH 5,2–6,2, Optimal 5,5–5,8 – etwas niedriger als typische Soil-Linien. In RDWC regelmäßig kontrollieren und Drift frühzeitig abfangen.",
                EcGuidance = "Veg: EC ca. 1,0–1,4 als Startrahmen. Flower: EC ca. 1,4–2,0 je nach Phase und Strain. Abschließend auf 0,5–0,8 EC herunterfahren. Immer mit der Pflanzenreaktion und dem System kalibrieren.",
                ScheduleStyle = "A/B-Basen in Veg und Flower-Versionen, ergänzt durch Cannazym, Rhizotonic, Boost und PK 13/14",
                OfficialHighlights = "Canna Aqua Vega und Flores sind A/B-Flaschen für rezirkulierende Systeme. Cannazym fördert die Wurzelzone durch Enzymaktivität. Canna PK 13/14 wird in der frühen Blüte für einen gezielten P/K-Schub eingesetzt. Canna Boost ist ein optionaler Blüteakzelerator.",
                PracticeNotes = "Für RDWC mit Canna Aqua ist der pH-Bereich besonders wichtig – das System ist etwas empfindlicher als Systeme mit mehr pH-Puffer. Regelmäßige Checks von EC, pH, DO und Wassertemperatur sind essentiell. Cannazym und Rhizotonic in Bewurzelungs- und Stresszeiten gezielt einsetzen.",
                SearchTerms = new() { "canna aqua", "canna", "aqua vega", "aqua flores", "cannazym", "rhizotonic", "canna boost", "pk 13/14", "pk13" },
                Stages = new()
                {
                    new() { Stage = "Bewurzelung / Seedling", Dose = "Rhizotonic 4 ml/L + Aqua Vega sehr schwach", Target = "EC ca. 0,4–0,8", Notes = "Rhizotonic fördert Wurzelentwicklung und verringert Transplant-Stress." },
                    new() { Stage = "frühe Veg", Dose = "Aqua Vega A/B nach Schema", Target = "EC ca. 1,0–1,2", Notes = "pH auf 5,5–5,8 einhalten; Cannazym wöchentlich zugeben." },
                    new() { Stage = "späte Veg", Dose = "Aqua Vega A/B leicht erhöhen", Target = "EC ca. 1,2–1,4", Notes = "Vor dem Flip Teilwechsel einplanen." },
                    new() { Stage = "frühe Blüte / Transition", Dose = "auf Aqua Flores A/B wechseln; PK 13/14 einmalig oder kurz", Target = "EC ca. 1,4–1,6", Notes = "PK 13/14 ist ein gezielter P/K-Schub, nicht dauerhaft fahrbar." },
                    new() { Stage = "Hauptblüte", Dose = "Aqua Flores A/B + optional Boost", Target = "EC ca. 1,6–2,0", Notes = "Strain-abhängig; Boost als optionaler Akzelerator." },
                    new() { Stage = "Finish / Flush", Dose = "EC herunterfahren, nur noch Wasser oder sehr schwache Lösung", Target = "EC ca. 0,5–0,8", Notes = "7–14 Tage je nach Reife- und Fade-Ziel." }
                },
                Tips = new()
                {
                    "Canna Aqua läuft am saubersten mit wöchentlichen oder bi-wöchentlichen Teilwechseln.",
                    "Rhizotonic nach Wasserwechseln und nach Stressphasen zugeben – nicht als Dauerpfleger.",
                    "Cannazym hilft bei sauberer Wurzelzone, aber ist kein Ersatz für gute Systemhygiene.",
                    "PK 13/14 ist kein Wundermittel – zu früh oder zu hoch dosiert schadet mehr als es nützt.",
                    "pH 5,5–5,8 als Zielbereich einhalten; Canna Aqua ist nicht für den Soil-pH-Korridor ausgelegt."
                }
            },
        };
    }

    private static List<MediumPlaybook> BuildMediumPlaybooks()
    {
        return new List<MediumPlaybook>
        {
            new()
            {
                Key = "hydro",
                Title = "Hydro / DWC / RDWC",
                Summary = "In RDWC/DWC zählt nie nur ein Einzelwert. Entscheidend sind Reservoir-pH, Reservoir-EC, Wasserstand, Wassertemperatur, DO, ORP, Addback und die Frage, ob sich diese Werte logisch miteinander bewegen.",
                FocusPoints = new() { "Reservoir-pH 5,8–6,2", "Reservoir-EC passend zur Phase", "Wasserstand in L/cm", "Wassertemperatur ideal 19–20 °C", "DO ideal >= 7 mg/L", "ORP meist 350–450 mV", "Addback-EC & Top-Off", "wöchentliche Changeouts" },
                RedFlags = new() { "pH und EC steigen gleichzeitig", "Wassertemperatur > 21–22 °C", "DO < 7 mg/L", "ORP < 350 mV oder > 500 mV", "EC steigt bei sinkendem Wasserstand", "hoher Buffer-Bedarf trotz Korrektur", "biofilmiger / fauliger Geruch" }
            },
        };
    }
}
