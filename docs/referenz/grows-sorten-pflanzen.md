# Grows, Phasen, Sorten und Pflanzen

> Ein Lauf von der Keimung bis zur Ernte: welche Phase heute gilt, woraus sie folgt, und wer im Zelt steht.

## Wo in der App

| Was | Wo |
|---|---|
| Grows (nur laufende und geplante) | Menü „Pflanzen → Grows", `/grows` |
| Grow anlegen / bearbeiten | `/grows/new`, `/grows/:growId/setup` |
| Grow-Überblick, Knöpfe Veg/Flip/Finish | `/grows/:growId` |
| Keimung und Bewurzelung bestätigen | `/messung` (`ManualMeasurementPage`) |
| Ernte eintragen | `/grows/:growId/harvest` |
| Sorten & Pheno-Hunt | Menü „Pflanzen → Sorten & Pheno", `/sorten` (alt: `/phenohunt`) |
| Pflanzen & Sorten dieses Grows, **je Pflanze Sorte und Topf** | Karte im Grow-Überblick (`GrowPlantsCard`) |
| Mutter klonen, Quarantäne entscheiden | `/zelte/:tentId`, an der Setup-Karte |
| Zeitstrahl, dieselbe Rechnung | Live (`/`), Grow-Karten, Grow-Überblick |
| Beendete Läufe | `/archiv` — nicht in `/grows` |

## Was es tut

**Phase.** `GrowStageResolver.Resolve` entscheidet in fester Reihenfolge: eingetragener
Flip schlägt jede Rechnung, Autoflower gehen nach Tagen seit der Keimung, sonst zählen
Einstiegspunkt (`GrowEntryPoint`) und mitgebrachte Tage (`DaysAlreadyInPhase`). Ergebnis
ist ein `GrowStage` — Seedling, Clone, Veg, Transition, Flower, Finish, Dry, Cure. Daran
hängen Zielwerte, Kacheln, Feedchart-Spalte (`MischplanService`) und Knöpfe. Nicht an der
letzten Messung.

**Beobachtet schlägt gerechnet.** Fünf Knöpfe schreiben je ein Datum und einen
Journal-Eintrag (`/api/grows/{id}/actions/…`): „Sämling ist durch" (`VegStartedAt`),
„Flip 12/12" (`FlipDate`) und „Finish beginnt" (`FinishStartedAt`) im Grow-Überblick,
Keimung (`GerminatedAt`) und Bewurzelung (`RootedAt`) auf `/messung`. Ohne Eintrag schätzt
der Resolver.

**Zeitstrahl** (`buildPhaseTimeline`): Keim → Sämling → Veg → Blüte → Trocknen → Aushärten,
mit Plan, laufendem Tag und Fortschritt; eine Rechnung für drei Seiten. Ohne Flipdatum
rechnet er den geplanten Flip aus `PlannedVegDays` ab Veg-Beginn („Flip geplant 09.06. ·
in 8 T", „Flip überfällig seit 3 T" — `flipLabel`).

**Wochenzähler** (`WeekCounterService`): Zustand und Beschriftung („Blüte Woche 3", „Keimt
seit 4 Tagen"). Benutzt wird er an zwei Stellen — beim Anlegen (`GrowsApiController.Create`
setzt den Lauf damit auf `Running`) und im `MischplanService`.

**Pflanzen einzeln** (`PlantInstance`): eigene `StrainId`, eigener **Topf** (`SiteIndex`,
ab 1 — dieselbe Nummer, die die Draufsicht des Hydro-Systems an ihre Sites zeichnet),
Rolle (Production, Mother, Clone, Quarantine), Status. Die Kachel „Sorte" im Überblick
sagt bei mehreren Sorten „gemischt (2)" statt einen einzelnen Namen zu behaupten.
Der Grow behält seine Hauptsorte für Listen und Strahl; wer gemischt
fährt, pflegt es in der Pflanzen-Karte → „3× RS11 · 1× Purple Lemonade". Ein Mutter-Setup
erzeugt Klone in die Quarantäne (Abstammung über `ParentPlantId`); von dort führt
„Freigeben" in ein Production-Setup, „Ablehnen" zu `PlantStatus.Culled`.

**Sorten und Pheno Hunt** auf `/sorten`: Bibliothek mit Züchterangaben und Filtern, Läufe
und Ø-Ertrag je Sorte, darunter je aktivem Grow der Kandidatenstreifen mit Bewertungsbogen
(`PhenoEvaluation`) und Keeper-Markierung.

## Die Zahlen und woher sie kommen

| Zahl | Wert | Woher |
|---|---|---|
| Sämlingsphase ohne Eintrag | 14 Tage | `GrowStageResolver.SeedlingDays`; Faustregel — typisch 1–3 Wochen, die Mitte gewählt (XML-Doku ebd.) |
| Übergang nach dem Flip | 10 Tage | `GrowStageResolver.TransitionDays` |
| Finish vor der Ernte | 14 Tage | `GrowStageResolver.FinishDaysBeforeHarvest` |
| Autoflower: Blütebeginn | 28 Tage nach Keimbasis | `GrowStageResolver.AutoflowerBluetenStart`; dieselbe 28 in `phase-timeline.ts` |
| Blütedauer ohne Breeder-Angabe | 8 Wochen | `phase-timeline.ts`; Schätzung, das Erntedatum trägt „~" |
| Trocknen | 10 Tage (Bereich 7–14) | `TROCKNEN_TAGE` in `phase-timeline.ts`; Quellen budtrainer.com und atmosiscience.com, dort zitiert |
| Aushärten | 30 Tage (Minimum 14, üblich 30–60) | `AUSHAERTEN_TAGE`, ebd., gleiche Quellen |
| Keimung / Bewurzelung überfällig | Warnung ab 7 T, kritisch ab 14 T | `DeviationAnalyzerService.CheckGerminationAndRooting` — **die Methode ruft niemand auf**; die Schwellen stehen im Code, die Warnung erscheint nirgends |
| Pheno-Gewichte | Ertrag 25, Qualität 25, Potenz 15, Robustheit 20, Struktur 15 | `PhenoWeights.Default` in `PhenoScoreCalculator.cs`; app-weit änderbar über `PUT /api/pheno/weights` |
| Bewertungsskalen | 1–10; Schädlings-Widerstand 1–5 (1 = stark befallen, 5 = unbehelligt) | `PhenoScoreCalculator.FromTen` / `FromFive`, Beschriftung in `PhenoSheetEditor.tsx` |

## Was es bewusst NICHT tut

- **Keine KI.** Phase, Punktzahl und Termine sind Arithmetik; jede Zahl steht oben.
- **Wo nichts feststeht, wird nicht geraten.** Ohne Breeder-Wochen kein gerechnetes Finish —
  es bleibt „Flower", bis jemand drückt (`FlowerStageFor`).
- **Ein bewurzelter Klon ist ab Tag 1 Veg**; die Sämlingsphase gehört den Samen.
- **Autoflower bekommen keinen Flip-Knopf** — der Server lehnt `flip-to-flower` mit 400 ab.
- **Ein Vorab-Flipdatum zählt noch nicht:** bis zum Tag X bleibt der Lauf vegetativ.
- **Der Topf ist eine Nummer, kein eigenes Ding.** `SiteIndex` zeigt auf die Zählung der
  Draufsicht (1..n, zeilenweise aus Topfzahl und Reihen). Es gibt keine Topf-Tabelle, keine
  Koordinaten und kein Ziehen — ein zweites Modell für dieselbe Wahrheit wäre teurer als der
  Nutzen. Wer anders bestückt, ändert die Nummer.
- **Ø-Ertrag nur bei reinsortigen Läufen.** Die Ernte wird als Gesamtgewicht erfasst; ein
  Mischzelt zeigt „— gemischt" (`StrainsPage`, `mixedOnly`).
- **Fehlende Bewertungsblöcke kosten keine Punkte** — sie fallen raus, die Gewichte werden
  über den Rest neu normiert. Ertrag und THC zählen relativ zu den Geschwistern desselben
  Hunts (`PhenoScoreCalculator`).
- **Züchterangaben sind Werbeangaben** und stehen so beschriftet (`Strain.cs`).
- **Umtopfen ist kein Vorgang**, nur ein Journal-Eintragstyp (`Transplant`).
- **Beendete Grows stehen nicht in `/grows`**, sondern nur im Archiv (`GrowsPage`).

## Im Code

| Aufgabe | Datei |
|---|---|
| Phase von heute | `GrowDiary.Web/Services/GrowStageResolver.cs` |
| Wochenzähler und Lauf-Zustand | `GrowDiary.Web/Services/WeekCounterService.cs` |
| Der Lauf selbst (Felder, viel XML-Doku) | `GrowDiary.Web/Models/GrowRun.cs` |
| Knöpfe Keimung/Bewurzelung/Veg/Flip/Finish, Ernte | `GrowDiary.Web/Api/Controllers/GrowWorkflowApiController.cs` |
| Anlegen, Ändern, Auto-Start auf „Running" | `GrowDiary.Web/Api/Controllers/GrowsApiController.cs` |
| Zeitstrahl, Flip-Beschriftung, Phasenkurzform | `GrowDiary.React/src/features/grows/phase-timeline.ts` |
| Liste, Überblick, Anlege-Assistent | `GrowDiary.React/src/pages/GrowsPage.tsx`, `GrowDetailPage.tsx`, `GrowSetupPage.tsx` |
| Pflanzen und ihre Sorten am Grow | `GrowDiary.React/src/features/grow-detail/GrowPlantsCard.tsx` |
| Klon, Freigabe, Ablehnung | `GrowDiary.React/src/features/plants/PlantActions.tsx`, `GrowDiary.Web/Api/Controllers/PlantsApiController.cs` |
| Sortenbibliothek und Pheno Hunt | `GrowDiary.React/src/pages/StrainsPage.tsx`, `GrowDiary.Web/Services/PhenoScoreCalculator.cs` |

## Fallen

- **Zwei Phasenmodelle nebeneinander.** Der Balken sagte „Veg Tag 8", die Kacheln zeigten
  Sämlings-Ziele (CHANGELOG beta.15).
- **Ein Samen-Grow ohne Keimdatum blieb ewig Sämling** — der Normalfall, und nach drei
  Monaten hätte eine ausgewachsene Pflanze noch Sämlings-EC bekommen (ebd.).
- **Der Zeitstrahl zählte anders als der Server, zweimal:** eine Autoflower erreichte nie
  die Blüte (Blütebeginn kam nur aus `flipDate`), und „Tage in Phase" wurde ignoriert
  (Kommentar in `phase-timeline.ts`).
- **Der Flip-Knopf wurde Autoflowern angeboten und scheiterte immer mit 400.** Der Kommentar
  neben der Bedingung wusste es — die Bedingung nicht. Der Ernte-Knopf hing an der letzten
  Handmessung und verschwand für jeden, der die Sensoren arbeiten lässt (`GrowDetailPage.tsx`).
- **„Blüte Woche 0"** bei vorab eingetragenem Flipdatum (`WeekCounterService`).
- **Das Startdatum schien nach dem Speichern weg** — die API liefert `2026-05-20T00:00:00`,
  `input[type="date"]` zeigt darauf leer (`nur-datum.ts`).
- **Ein Mischzelt schrieb alle Kandidaten der Hauptsorte gut** (`StrainsPage.tsx`); dort
  sortierte „Blütezeit kürzeste zuerst" auch genau rückwärts.
- **Im Balken stand „TROCKNE…" statt „Trocknen 10 T"** — die Zahl fiel beim Kürzen weg,
  deshalb setzt `balkenText` sie nach vorn.
- **Der Keim-/Bewurzelungs-Wächter läuft nicht.** `CheckGerminationAndRooting` ist die
  einzige Prüfung in `DeviationAnalyzerService`, die kein Aufrufer hat — kein Dienst, kein
  Endpunkt, kein Test. Ein Samen, der nach drei Wochen nicht keimt, wird nirgends
  angemahnt (Stand 2.0.0-beta.52, per Volltextsuche über `*.cs`/`*.ts`/`*.tsx`).
