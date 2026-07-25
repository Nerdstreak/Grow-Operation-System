# SOPs als Algorithmen — Analyse und Bauplan

Ziel: **die SOPs vollständig in ausführbare Logik überführen.** Dieses Dokument hält fest,
was die Quelldokumente vorschreiben, was die App davon heute abbildet, und wo sie
abweicht — Zeile für Zeile prüfbar.

Ergänzt `code-analyse.md` (Struktur des Codes) um die fachliche Sicht.

Stand: 2026-07-25.

## Quellen

Ordner `RDWC Wissen/` (gitignored, bleibt lokal). Für die Analyse extrahiert:

| Dokument | Umfang | In der App abgebildet als |
|---|---|---|
| **SOP-RDWC-CAN-N1** — pH- & Nährstoff-Stabilisierung | 43k Zeichen | `mixing-order-rdwc-ro`, `mixing-order-rdwc-soft-water`, `daily-measurement-routine` |
| **SOP-RDWC-CAN-S1** — Behandlung von Wurzelfäule | 17k | `root-rot-treatment` |
| **SOP-RDWC-CAN-C1** — Quarantäne von Stecklingen | 21k | `cuttings-quarantine` |
| **RDWC Growplan by SKX** (Athena + Canna Aqua) | — | `setpoints/rdwc-default.json`, `nutrient-programs/*` |
| **Manueller Addback** | — | `RecommendationEngine`, `AddbackCalculator` |
| **Häufige Anfängerfehler & Risikomanagement** | — | `wear/`-Katalog, Wartungserinnerungen |
| **RDWC MESSPROTOKOLL.xlsx** | — | Messgrößen der Messmaske |
| **VPD-Rechner (Ben Green)** | — | `VpdCalculator` |
| **RDWC Procedure (Metric)** — Tony Buckets Partnership | 36k | 6 Regeln, siehe unten |
| Workshop-Lehrmaterial, Easy Grow Guide | 94 MB / 13 MB | noch offen |

Die App hat **10 SOPs** und **24 Regel-Einträge**. SOP-S1 ist seit 1.7.2 originalgetreu
abgebildet (18 Schritte, davon 3 verzweigt und 9 je Pflanze).

---

# Teil 1 — Regelverstöße (behoben)

Diese Punkte wichen belegbar von den Dokumenten ab. Alle mit Test abgesichert.

## V1 — pH-Drift war überhaupt nicht implementiert ⛔ die wichtigste Lücke

**SOP-N1 §2.1** unterscheidet Schwankung und Drift **an der Geschwindigkeit**, nicht am Wert:

| Merkmal | Normale Schwankung | Kritischer Drift |
|---|---|---|
| Zeitraum | 0,1–0,4 pH/Tag | **≥ 0,5 pH in 12–24 h** |
| Richtung | leichte Absenkung bei Aufnahme | stetig über mehrere Tage |
| EC | stabil oder leicht sinkend | ansteigend oder stark schwankend |
| DO | bleibt > 7,5 mg/L | < 6,5 mg/L, stetiger Abfall |
| ORP | baut langsam ab, > 300 mV | rapider Abbau |

Die App kannte **nur Absolutwerte**. Ein Sprung von 5,8 auf 6,3 über Nacht bleibt komplett
im Zielband — nichts hat ihn gemeldet. Genau dieser Sprung ist laut SOP ein Fall für
Sofortmaßnahmen, weil er auf pathogene Aktivität, CO₂-Sättigung oder Ausfällungen hinweist.

**Umgesetzt:** `DeviationAnalyzerService.CheckPhDriftRate` vergleicht die letzten beiden
Messungen, wenn sie höchstens 24 h auseinanderliegen.
- ab 0,5 → **Critical** mit der Maßnahmenliste aus §2.2 (Wurzeln, Wasserprobe, ORP, DO,
  Filter; bei Befund NSL ablassen, HOCl-Spülung, Neuaufsetzen mit pH 5,8–6,0 / ORP > 400 mV)
- 0,2–0,4 → **Info** mit dem Hinweis, nur um 0,1–0,2 schrittweise nachzuregeln
- unter 0,2 → still, das ist die gewollte Aufnahme

## V2 — Sauerstoff-Schwelle lag unter dem Handlungspunkt der SOP

**SOP-N1 §2.2:** „DO-Wert messen: **< 6,5 mg/l** = erhöhte mikrobiologische Aktivität."
**SOP-N1 §2.1:** normal „bleibt stets **> 7,5 mg/l**".
**SOP-S1 §2.2:** Bestätigung Wurzelfäule bei **< 6 mg/L**.

Die App warnte erst unter **6,0** und stufte erst unter 4,0 als kritisch ein. Der Bereich
6,0–6,5, in dem die SOP bereits Maßnahmen verlangt, war stumm — und das ist genau der
Bereich, in dem Wurzelfäule beginnt, ohne dass etwas sichtbar ist.

**Umgesetzt:** Warnung unter 6,5 („mikrobiologische Aktivität", SOP-N1), kritisch unter 6,0
(„Wurzelfäule bestätigt", SOP-S1). Beide Texte nennen die Quelle.

## V3 — Der Flush wurde als Fehler gemeldet

Growplan-EC-Reihe endet auf **… 1,6 · 1,4 · 1,1 · 0,4**. Die letzten Werte sind Ausklang
und Flush. Die Sollwerte sagten für Finish **1,1–1,6** — also den Peak der Blüte.

Nachgestellt am laufenden System, Grow in Finish mit EC 0,4:

```
Warning | Reservoir-EC ist um -1,60 mS/cm gefallen. | Ziel 1.1 - 1.6
```

**Umgesetzt:** Finish auf **0,4–1,1** korrigiert.

Dazu ein Nebenbefund: Ein **bestehender Test hat den Fehler festgeschrieben** —
`RDWC_Finish_ECMaxGroesserGleichFlowerECMax` verlangte ausdrücklich, dass Finish über
Flower liegt. Die Zusicherung ist jetzt umgedreht und begründet.

## V4 — Zwei Zahlen für den DWC-Faktor

Growplan Punkt 1: „+30 % starten". `TargetValueService.DwcEcMultiplier = 1.3`,
`RecommendationEngine` rechnete mit **1.35** ohne Beleg. Vereinheitlicht auf die Konstante.

## V5 — pH-Schwellen zum dritten Mal als Literale

`RecommendationEngine` hatte `5.5 / 6.5 / 5.8 / 6.2` erneut ausgeschrieben. Ersetzt durch
die geteilten Konstanten aus `DeviationAnalyzerService`. Damit gibt es für pH **eine**
Quelle statt drei.

## Ergänzte Regel-Einträge

Acht neue Einträge in `knowledge-defaults/guidance/`, jeder mit Quellenangabe auf Dokument
und Abschnitt: `ph-drift-rate`, `ph-drift-response`, `ph-light-drift-response`,
`dissolved-oxygen-thresholds`, `orp-optimal-band`, `water-temperature-band`,
`root-rot-triage`, `root-rot-recovery-orp`.

Diese sind gleichzeitig die Belege, auf die sich ein späterer Assistent stützen muss.

---

# Teil 2 — Was die SOPs vorschreiben und die App noch nicht kann

Hier liegt die eigentliche Arbeit. Bewertet nach: bildet die App den **Ablauf** ab, oder nur
eine Zusammenfassung davon?

## SOP-N1 — pH- & Nährstoff-Stabilisierung

**Abgebildet:** Mischreihenfolge RO und Weichwasser als je 10 Schritte, inklusive der
pH-Eichung auf 6,0 an vier Stellen und der Standzeit für Silikat. Das ist nah an der Quelle.

**Nicht abgebildet:**

| SOP-Inhalt | Was fehlt |
|---|---|
| §2.1 Diagnosetabelle | Die Unterscheidung nutzt **fünf** Merkmale gleichzeitig (pH-Rate, Richtung, EC-Verhalten, Wasseroberfläche, DO, ORP). Die App prüft sie einzeln, nie als Muster. Ein Algorithmus, der alle fünf zusammen bewertet, würde „chemische Instabilität" von „normaler Aufnahme" viel sicherer trennen. |
| ORP-Nachdosierung alle 2–3 Tage | Kein Intervall, keine Erinnerung |
| HOCl-Dosierung 250–1000 ppm herstellerabhängig | Nicht als Parameter erfassbar |
| Ausgangswasser-Eignung | SOP gilt nur mit RO/VE-Wasser (0,0 mS). Die App fragt die Wasserquelle ab, prüft aber nicht, ob die SOP darauf überhaupt anwendbar ist. |

## SOP-S1 — Wurzelfäule

**Abgebildet:** 14 Schritte über 14 Tage, mit Foto-Dokumentation, HOCl-Bad, H₂O₂-Behandlung
und Erfolgs-Check.

**Nicht abgebildet — und das ist der größte strukturelle Unterschied:**

Die Quelle **verzweigt nach Befallsgrad**:
- §5.1 *passive Behandlung* für wenig/nicht befallene Pflanzen — Spülbad 1 (HOCl, ORP 750 mV),
  Spülbad 2 (RO) für **1–2 Minuten**
- §5.2 *aktive Behandlung* für stark befallene — vorher Wurzelschnitt (§4.3), dann dieselbe
  Abfolge mit **180 Sekunden** im zweiten Bad

Dazu §4.4: **Desinfektion nach jeder einzelnen Pflanze** (Schere, Gestell, Arbeitsfläche).
Unsere Version ist linear und kennt weder die Verzweigung noch die Pflanze-für-Pflanze-Schleife.

Weitere fehlende Details: Spülbehälter Nr. 1 auf **ORP 750 mV**, Sprühflasche auf **500 mV**,
System nach Reinigung auf **min. 400 mV**, erste Woche **min. 450 mV**, danach 400 mV.

## SOP-C1 — Stecklingsquarantäne

**Abgebildet:** 12 Schritte über 7–21 Tage, mit Wurzelwäsche, IPM, Fulvosäure (1 ml/L),
Kaliumsilikat (0,5 ml/L) und Freigabe-Check.

**Nicht abgebildet:**

| SOP-Inhalt | Was fehlt |
|---|---|
| §2 Substratträger (Steinwolle, EasyPlugz, Jiffies) | Sechs Unterabschnitte zu Vorbehandlung, Dekontaminationsoptionen, Trocknungsphase — in der App gar nicht vorhanden. Für RDWC ist das kritisch, weil Substratreste Staunässe und Keimherde ins System tragen. |
| §3 **3-Bad-Methode** | Bad 1 Insektizid/mechanisch, Bad 2 Desinfektion, Bad 3 neutrale Spülung. Unsere App fasst das zu *einem* Schritt „Wurzelwäsche" zusammen. |
| §4.3 Klima-Parameter der Quarantäne | Nur grob als „Klima einrichten" |
| §4.4 tägliche Kontrolle | Kein eigener wiederkehrender Schritt |
| §4.5 Quarantänedauer-Kriterien | Dauer steht im Titel, die Entscheidungskriterien fehlen |

## RDWC Procedure (Metric) — geprüft

Zweite, ergänzende Quelle (Tony Buckets Partnership). Sechs Regeln daraus ergänzt:

| Regel | Warum sie zählt |
|---|---|
| **ORP-Schock sieht aus wie ein Nährstoffmangel** | Vergilbtes, krustiges Laub. Wer das als Mangel liest und nachdüngt, verschlimmert es — die Aufnahme ist blockiert, nicht das Angebot zu klein. Erst ORP messen. |
| **Der Geruch als Diagnose** | Faulig = anaerob, frische Bohnensprossen = gesund, Chlor = ORP-Schock. Trennt die beiden Fehlerrichtungen ohne Messgerät. |
| Höchstens **500 ml je Komponente** pro Addback-Behälter | Sonst Ausfällungen, bevor sich die Lösung verteilt. Nährstoffe nie direkt in den Control Bucket. |
| Addback-Behälter zu 90 % füllen, einzeln zugeben, **zwischen jedem rühren** | Ohne Rühren reagieren die Konzentrate miteinander. |
| Betriebsvolumen einrechnen | ~40,1 L je 49,2-L-Modul. Ohne das stimmt die Konzentration nicht. |
| **EC über die Woche anheben**, nicht in einem Schritt | Das System verdünnt sich durch Top-off laufend selbst. |

Der `AddbackCalculator` rechnet korrekt auf das Systemvolumen — die Warnung der Procedure
greift dort also nicht. Die 500-ml-Regel und die Rührvorgabe betreffen den physischen
Mischvorgang und gehören in eine Addback-SOP, die noch fehlt.

## Workshop Lehrmaterial — geprüft

19 Folien, fast vollständig Bild. Der lesbare Text bestätigt Vorhandenes und liefert zwei
neue Punkte:

| Fund | Konsequenz |
|---|---|
| **Blatttemperatur mit −2 °C Offset** | Der Standard für neue Zelte stand auf **0**, rechnete also reines Luft-VPD. Zwei Quellen nennen −2 °C (Workshop + Ben-Green-Rechner mit Luft 28 / Blatt 26). Standard jetzt 2,0 °C. Bestehende Zelte bleiben unverändert — 0 könnte eine bewusste Wahl gewesen sein. |
| **RDWC transpiriert anders als Erde** | Das Material führt **zwei getrennte VPD-Empfehlungen**: eine reguläre und eine für DWC/RDWC. Als Regel erfasst; **die Zahlen selbst liegen in den Bildern** und sind ohne OCR nicht auslesbar. |
| pH Veg 6,0–6,2 / Blüte 5,6–6,0, EC-Konsum 0,05–0,10, Ziel-EC 1,4–1,5 | deckt sich mit Growplan und Wissensbasis ✓ |
| Wassertemperatur-Zonen 19–20 / 18–21 / 21–23 / >24 °C | deckt sich mit `RecommendationEngine` ✓ |

## Nicht auswertbar ohne OCR

- **Easy Grow Guide** (5 Seiten) — reines Bilddokument, Textextraktion liefert null Zeichen.
- **Workshop-VPD-Tabellen** — die eigentlichen Zahlen der RDWC-Empfehlung stehen als Grafik.

Beides bräuchte Tesseract auf dem Rechner (`pytesseract` allein reicht nicht, das Binary
fehlt). Bis dahin bleiben die RDWC-VPD-Zielwerte die aus dem Growplan.

---

# Teil 3 — Bauplan

Die SOPs vollständig als Algorithmen abzubilden heißt, das Schrittmodell zu erweitern. Heute
kennt `SopStepDefinition` die Typen `Action`, `Measurement`, `Wait`, `Confirmation`,
`Photo`, `SubSop`. Für die Quellen fehlen zwei Konzepte:

**1. Verzweigung.** SOP-S1 teilt nach Befallsgrad, SOP-C1 nach Substrattyp. Nötig wäre ein
Schritttyp `Branch` mit Bedingung und zwei Folgezweigen — oder schlanker: ein Schritt darf
eine `condition` tragen und wird übersprungen, wenn sie nicht zutrifft.

**2. Schleife über Objekte.** „Für jede Pflanze: entnehmen, spülen, desinfizieren" ist in
SOP-S1 und C1 der Kern. Ohne das bleibt es eine Textanweisung statt eines abhakbaren Ablaufs.

Umgesetzt (1.7.1):

1. ✅ **`condition` an Schritten** — `SopStepCondition` mit Schlüssel, erlaubten Werten und Frage
2. ✅ **`repeatFor`** — ein Schritt wiederholt sich je Pflanze, Eimer, Modul oder Steckling
3. ✅ **`SopStepPlanner`** — beantwortet „welche Schritte gelten für diesen Durchlauf" und
   nummeriert Wiederholungen („Pflanze 3 von 6"). Eine **unbeantwortete Frage behält alle
   Schritte** — eine Behandlung stillschweigend zu kürzen wäre die schlimmere Fehlerart.
4. ✅ **SOP-S1 originalgetreu** — 18 Schritte statt 14, davon 3 nach Befallsgrad verzweigt
   und 9 je Pflanze. Enthält jetzt die Zwischendesinfektion nach jeder Pflanze (§4.4), die
   Spülbad-Werte (ORP 750 mV / Sprühflasche 500 mV / System min. 400 mV) und die
   unterschiedlichen Spülzeiten (1–2 min passiv, 180 s aktiv).
5. ✅ Nebenbefund korrigiert: Der Schritt „Initial-Messung" nannte **DO < 4 mg/L** als
   Bestätigung — SOP-S1 §2.2 sagt **< 6 mg/L**.

Offen:

6. **SOP-C1 §2 und §3 ergänzen** — Substratträger (Steinwolle, EasyPlugz, Jiffies) und die
   3-Bad-Methode. Das Schrittmodell kann es jetzt, die Daten fehlen noch.
7. **Addback-SOP** aus der RDWC Procedure — 90 % füllen, einzeln zugeben, rühren, 500-ml-Grenze
8. **Musterdiagnose nach SOP-N1 §2.1** — fünf Merkmale zusammen bewerten statt einzeln
9. ✅ **Oberfläche (1.7.3)** — der SOP-Katalog holt vor dem Start die Fragen der SOP ab und
   zeigt sie inline. Beim Start werden nur die zutreffenden Schritte angelegt, je Pflanze
   durchnummeriert.

**Wichtige Korrektur beim Bauen:** Die erste Fassung des Planers hat *je Schritt* wiederholt —
erst alle Pflanzen entnehmen, dann alle schneiden, dann alle desinfizieren. Damit wäre die
Zwischendesinfektion wirkungslos gewesen, also genau die Maßnahme, die die Verschleppung
verhindert. Der Planer wiederholt jetzt den **Block**: Pflanze 1 komplett bis in die
Quarantäne, dann Pflanze 2. Aufgefallen ist das erst beim Durchlauf am laufenden System.

Erst danach lohnt der Assistent: Er soll die SOPs *erklären* und *anstoßen*, nicht sie
ersetzen. Solange der Ablauf selbst unvollständig ist, hätte er nichts Verlässliches, worauf
er verweisen könnte.
