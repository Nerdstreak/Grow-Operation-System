# Zelte, Hydro-Systeme und Wasser

> Die Anlage, wie sie physisch dasteht: Raum, Becken, Wasser — einmal einrichten, danach lesen alle anderen Bereiche daraus.

## Wo in der App

| Was | Wo |
|---|---|
| Zelte & Räume | Menü *Einrichtung* → `/zelte`, Formular `/zelte/new` |
| Ein Zelt im Detail | `/zelte/:tentId` — Zeltwerte, Reservoir, Hydro-Systeme, Grows, Setups & Pflanzen |
| Hydro-Systeme | Menü *Einrichtung* → `/hydro`; Karte je System zeigt das Belüftungs-Urteil |
| System anlegen / bearbeiten | `/hydro/new`, `/hydro/:id/edit` (eine Seite, kein Assistent) |
| Ein System im Detail | `/hydro/:setupId` — Details, **Volumen-Kalibrierung**, verknüpfte Grows |
| Wasser | Menü *Einrichtung* → `/wasser`; dort die Ampel „Taugt dein Wasser?" |
| Ausgangswasser | Abschnitt im Lagebericht (`AgentContextBuilder`, MCP + Mappe für eigene KI) |

## Was es tut

**Zelt.** Raum, Maße, Licht, Ab-/Umluft, CO₂, Kamera(s), Blatt-Offset für VPD.
Aus Maßen und Abluft rechnet die Kachel den Luftwechsel (`airChanges` in
`TentsPage.tsx`), aus Maßen und Pflanzenzahl die Fläche je Pflanze. Der
Lichtzyklus wird **nicht** eingetragen, sondern aus den An/Aus-Flanken gelernt.
Die Kühler-Felder liegen am Zelt, gehören aber zu Crop Steering.

**Hydro-System.** DWC oder RDWC, Sites, Liter je Site, Tank und Tankposition,
Umwälz- und Luftpumpe, Chiller, UV-Klärer. Volumen = Sites × Liter je Site +
Tank (`CalculateTotalVolumeLiters`); daran hängen Dosier-Rechnung und
Belüftungs-Urteil. Ein Zelt ist Pflicht — `CreateHydroSetup` **und**
`UpdateHydroSetup` prüfen mit `requireTent: true`, ein System kann also auch
nachträglich nicht zeltlos werden.

**Volumen-Kalibrierung (cm → Liter).** Ein eTape liefert Zentimeter, gebraucht
werden Liter. Der Assistent liest den Sensor im Sekundentakt mit, während man
füllt: ruhiger Stand im leeren System = Nullpunkt, dann füllen, dann fragt Grow
OS „voll?" und man trägt die Liter von der Wasseruhr ein. Aus den zwei Punkten
entsteht eine Gerade (`ReservoirVolume`); die Umrechnung sitzt an der Quelle
(`HomeAssistantService.AddLitersFromCentimeters`), sodass Kacheln, Historie,
Schwellen und Dosierfaktor dieselbe Zahl sehen. Kalibriert wird mit **laufender
Umwälzung** — so wird später auch gemessen.

**Wasserprofil.** Ein Profil für die ganze App, aufgebaut wie ein deutscher
Trinkwasserbericht, plus zwei Felder für das Wasser **nach** Osmose oder
Entsalzung. Die Ampel macht daraus Sätze mit Quelle. Wirkung: Start-EC im
Lagebericht und die vorbeantwortete Frage „Womit mischst du an?" beim SOP-Start.

## Die Zahlen und woher sie kommen

| Zahl | Wert | Woher |
|---|---|---|
| Blatt kühler als Luft (neues Zelt) | 2,0 °C | `Tent.DefaultLeafTempOffsetC` — Workshop-Material RDWC / Ben-Green-Rechner |
| Zelt-Ersatzmaß im Systemplan | 120 × 120 cm | `system-plan-model.ts`, `DEFAULT_TENT` |
| RDWC-Sites / Hinweis „Tank dünn" | 2–12, 1–3 Reihen / Tank < 25 % des Eimervolumens | `HydroEditorPage.tsx`, `tankThin` |
| Nullpunkt / Vollstand: Wert ruhig | 15 s / 60 s | `LevelStability.EmptySeconds`, `.FullSeconds` (RDWC gleicht sich erst über die Verrohrung aus) |
| Ruhe-Band, Mindestablesungen | ±0,3 cm, 3 Werte | `LevelStability.ToleranceCm`, `.StableValue` |
| Ablesung / Sitzung verfällt | 3 min / 30 min | `LevelCalibrationService.Window`, `.Abandoned` |
| Belüftung Optimum / untere Kante | 0,5 / 0,10 L/min je Liter | `AerationCheck.OptimumJeLiter` — SKX, Autor der RDWC-Abläufe; die untere Kante aus allgemeiner DWC-Literatur |
| Stufen „zu wenig / knapp / mehr als nötig / sehr hoch" | < 0,05 / ab 0,05 / > 0,75 (Optimum × 1,5) / ≥ 1,0 | `AerationCheck.Beurteilen`, `.ToleranzFaktor` |
| O₂-Sättigung, z. B. bei 20 °C | 9,09 mg/L (Tabelle 10–32 °C) | `AerationCheck.Saettigung` — USGS-Löslichkeitstabelle |
| Härte-Umrechnung | 1 °dH = 17,848 mg/L CaCO₃ | `WasserAmpelService.MgProDh` |
| Karbonathärte gut | 30–100 mg/L CaCO₃, Problem > 150 | `WasserAmpelService` — Penn State Extension |
| Gesamthärte | weich < 8,4 °dH, hart > 14 °dH | `WasserAmpelService` — WRMG § 9 |
| Leitfähigkeit | gut < 0,5, Hinweis < 1,0, Warnung ab 1,0 mS/cm | ebd. — Penn State Extension |
| pH des Leitungswassers | 5,0–7,0 unbedenklich | ebd. |
| Natrium / Chlorid | Warnung > 50 mg/L / gut ≤ 30, Hinweis ≤ 100 | ebd. |
| Lichtzyklus: Rückblick / An-Phasen | 5 Tage / mind. 2 | `LightCycleReader.LookbackDays`, `LightCycleLearner.MinPhases` |
| „18/6"-Toleranz; Blüte / Veg | ±0,5 h; ≤ 13 h / ≥ 16 h Licht | `LearnedCycle.IsClose`, `.LooksLikeFlower`, `.LooksLikeVeg` |

## Was es bewusst NICHT tut

- **Kein mg/L aus der Pumpengröße.** `AerationCheck` liefert eine Einordnung,
  keine Messung: „ein erfundener mg/L-Wert … sähe aus wie ein Sensorwert und
  würde Alarme füttern". Beide Faustregeln (0,10 und 0,50) bleiben nebeneinander
  stehen, keine ersetzt die andere.
- **Der Assistent entscheidet „voll" nicht selbst** — eine Füllpause sieht für
  den Sensor aus wie fertig (`LevelCalibrationService`). Der Lauf lebt nur im
  Speicher; ein Neustart bricht ihn ab, weil das Becken danach anders dasteht.
- **Ein Punkt reicht nicht.** Eine Gerade durch den Ursprung wäre unten am
  stärksten daneben, also genau dort, wo der Füllstand zählt. Über „voll" hinaus
  wird weitergerechnet, nach unten bei 0 gekappt (`ReservoirVolume`).
- **Nur DWC und RDWC.** `HydroStyle` kennt auch NFT und Aeroponic;
  `HydroSetupRepository.ValidateHydroSetup` weist sie ab.
- **Ein Wasserprofil, nicht eins je Grow** — die Leitung ändert sich nicht je
  Lauf; der Grow entscheidet über `GrowRun.WaterSource`, ob es gilt. Gespeichert
  als JSON in `AppSettings` statt in eigener Tabelle (`WaterProfileStore`).
- **Calcium, Magnesium und Nitrat bekommen keine Ampel** — im Kreislauf ist der
  Dünger die Quelle, nicht das Wasser. Weiches Wasser ist hier kein Mangel,
  sondern der Idealfall (`duengerLiefertCalMag`).
- **Kein Lichtplan als Wahrheit**: beobachtete Flanken *sind* die richtige Uhr,
  ein Plan in der falschen Zeitzone geht daneben (`LightCycleLearner`).
- **Zelte mit aktiven Abhängigkeiten werden nicht gelöscht** (HTTP 409,
  `SettingsApiController.HasBlockingTentDependencies`); Zelttyp und Setup-Typ
  müssen zusammenpassen, nur `MultiPurpose` nimmt alles
  (`SetupTentCompatibilityPolicy`).

## Im Code

| Aufgabe | Datei |
|---|---|
| Zelt-Modell (Technik, Kamera, Blatt-Offset, Kühlerfelder); Löschen + Abhängigkeiten | `GrowDiary.Web/Models/Tent.cs`, `Api/Controllers/SettingsApiController.cs` |
| Hydro-System: Modell, Prüfung, Gesamtvolumen ins DTO | `Models/GrowSystem.cs`, `Infrastructure/HydroSetupRepository.cs`, `Api/Mapping/HydroSetupMapping.cs` |
| cm → Liter (die Gerade) | `GrowDiary.Web/Services/ReservoirVolume.cs` |
| Stillstand + Ablaufsteuerung der Kalibrierung | `Services/LevelStability.cs`, `LevelCalibrationService.cs` |
| Liter-Zustand in die Live-Werte | `Services/HomeAssistantService.cs` (`AddLitersFromCentimeters`) |
| Belüftung + O₂-Sättigung | `GrowDiary.Web/Services/AerationCheck.cs` |
| Wasserprofil speichern / bewerten | `Services/WaterProfileStore.cs`, `WasserAmpelService.cs` |
| Seiten (`GrowDiary.React/src/pages/`) | `TentsPage.tsx`, `TentDetailPage.tsx`, `HydroPage.tsx`, `HydroDetailPage.tsx`, `WaterProfilePage.tsx` |
| Editor, Kalibrierpanel, Zahl-Helfer | `src/features/hydro/HydroEditorPage.tsx`, `LevelCalibrationPanel.tsx`, `src/features/water/wasser-zahlen.ts` |

## Fallen

- **Zelt speichern entwaffnete die Nachtabsenkung.** Jeder Speichervorgang — auch
  nur ein umgemapptes Sensorfeld — löschte das Zielgerät der Rampe; der Schalter
  am Grow sagte weiter „an", geschrieben wurde nichts. Ältere Zelte verloren dabei
  zusätzlich ihre Kamera. (beta.38)
- **Der Tausenderpunkt fraß den Wasserwert.** 1234 µS/cm wurde als „1.234"
  angezeigt und als 1,234 zurückgelesen — ein Tausendstel, still. Seither liegen
  die Zahl-Helfer in `src/features/water/wasser-zahlen.ts`. (beta.38)
- **Neue Zelte rechneten Luft-VPD statt Blatt-VPD**, weil der Offset auf 0 stand,
  während jede RDWC-Empfehlung für den Blattwert gezeichnet ist. Bestehende Zelte
  behielten ihren Wert — 0 kann Absicht gewesen sein. (1.8.1)
- **Der Wasserstand kam als Text statt als Zahl an.** Die Kachel hielt ihn für ein
  Etikett, ließ die Einheit weg und zeichnete keine Trendlinie. (beta.19)
- **Menü „Wasser", Seite „Leitungswasser".** Hält seither
  `GrowDiary.React/e2e/wegweiser.spec.ts` zusammen. (beta.42)
- **E2E verschmutzte den Bestand**: jeder Lauf ließ ein Zelt „Rundweg HH:MM:SS"
  zurück; nach drei Läufen gewann ein leeres Testzelt die Vorauswahl und die
  halbe Live-Seite sah leer aus. (beta.52)
