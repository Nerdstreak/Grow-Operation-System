# Referenz — was die App tut und warum

> Zehn Nachschlagseiten, eine je Bereich. Jede hat dieselben Abschnitte in
> derselben Reihenfolge: **Wo in der App · Was es tut · Die Zahlen und woher sie
> kommen · Was es bewusst NICHT tut · Im Code · Fallen.**

Stand 2026-08-20 · Grow OS 2.0.0-beta.52 · Grow MCP 0.1.8

## Wenn du wissen willst …

| Wenn du wissen willst … | dann hier |
|---|---|
| Warum Diagnose und Live-Kachel schon einmal verschiedene EC-Ziele zeigten | [sollwerte-und-wissen.md](sollwerte-und-wissen.md) |
| Woher ein Zielwert überhaupt kommt (Grow → Hydro-System → Anbaustil → eigener Grenzwert) | [sollwerte-und-wissen.md](sollwerte-und-wissen.md) |
| Wie ein Wissens-Update auf eine Installation kommt, die es schon gibt | [sollwerte-und-wissen.md](sollwerte-und-wissen.md) |
| Woher die Posten der Einkaufsliste stammen | [sollwerte-und-wissen.md](sollwerte-und-wissen.md) |
| Warum auf der Kachel „Hand · vor 2 Std" steht statt eines Sensorwerts | [live-und-messen.md](live-und-messen.md) |
| Wie der Grow-Score rechnet und wofür er Punkte abzieht | [live-und-messen.md](live-und-messen.md) |
| Was „den Wert kann es nicht geben" im Messprotokoll bedeutet | [live-und-messen.md](live-und-messen.md) |
| Warum nachts für PPFD, CO₂ und VPD kein Ziel gilt | [live-und-messen.md](live-und-messen.md) |
| Wie viele Milliliter eine Pumpe geben soll — und woher die App das weiß | [dosierung-und-addback.md](dosierung-und-addback.md) |
| Warum gerade keine Pumpe dosiert, obwohl der Wert danebenliegt | [dosierung-und-addback.md](dosierung-und-addback.md) |
| Warum B erst Minuten nach A läuft | [dosierung-und-addback.md](dosierung-und-addback.md) |
| Wie viel Stocklösung der Addback verlangt und wie das gerechnet wird | [dosierung-und-addback.md](dosierung-und-addback.md) |
| Warum der Kühler steht, obwohl das Wasser über dem Nachtwert liegt | [crop-steering.md](crop-steering.md) |
| Um wie viel die Nachtabsenkung je Blütewoche fällt und wo sie aufhört | [crop-steering.md](crop-steering.md) |
| Warum die App bei pH 6,15 nicht warnt, obwohl der Sollwert 6,0 ist | [diagnose-und-risiken.md](diagnose-und-risiken.md) |
| Wo man anfängt, wenn man etwas an der Pflanze *sieht* statt misst | [diagnose-und-risiken.md](diagnose-und-risiken.md) |
| Was über Tage auffällt, was eine einzelne Messung nicht zeigt | [diagnose-und-risiken.md](diagnose-und-risiken.md) |
| Warum ein erledigtes Risiko nicht sofort wiederkommt | [diagnose-und-risiken.md](diagnose-und-risiken.md) |
| Welche Phase der Grow heute hat und was welche Regel schlägt | [grows-sorten-pflanzen.md](grows-sorten-pflanzen.md) |
| Warum der Lauf vegetativ bleibt, obwohl ein Flipdatum eingetragen ist | [grows-sorten-pflanzen.md](grows-sorten-pflanzen.md) |
| Wie die Pheno-Punktzahl gewichtet wird | [grows-sorten-pflanzen.md](grows-sorten-pflanzen.md) |
| Wann ein Glas gelüftet werden muss und wie lange | [ernte-trocknen-aushaerten.md](ernte-trocknen-aushaerten.md) |
| Was in der Kostenzahl im Archiv steckt — und was bewusst nicht | [ernte-trocknen-aushaerten.md](ernte-trocknen-aushaerten.md) |
| Warum die Reservoir-Alarme nach der Ernte schweigen | [ernte-trocknen-aushaerten.md](ernte-trocknen-aushaerten.md) |
| Wie aus Zentimetern am eTape ein Literwert wird | [zelte-hydro-wasser.md](zelte-hydro-wasser.md) |
| Ob die Luftpumpe für das Beckenvolumen reicht | [zelte-hydro-wasser.md](zelte-hydro-wasser.md) |
| Warum Calcium und Magnesium im Wasserprofil keine Ampel bekommen | [zelte-hydro-wasser.md](zelte-hydro-wasser.md) |
| Warum `switch.kuehler` im Zustands-Wörterbuch nicht zu finden ist | [home-assistant-und-automatik.md](home-assistant-und-automatik.md) |
| Welcher Hintergrunddienst in welchem Takt läuft | [home-assistant-und-automatik.md](home-assistant-und-automatik.md) |
| Warum kein Push kam, obwohl der Grenzwert überschritten war | [home-assistant-und-automatik.md](home-assistant-und-automatik.md) |
| Warum ein Stellbefehl ein paar Sekunden dauert, statt sofort „gespeichert" zu melden | [home-assistant-und-automatik.md](home-assistant-und-automatik.md) |
| Warum „gesendet" nicht „angekommen" heißt, wenn ein Gerät hinter einer Hersteller-Wolke hängt | [home-assistant-und-automatik.md](home-assistant-und-automatik.md) |
| In welcher Reihenfolge die Aufgabenseite sortiert und woher jede Zeile kommt | [aufgaben-journal-mcp.md](aufgaben-journal-mcp.md) |
| Warum überhaupt keine Erinnerungen mehr kommen | [aufgaben-journal-mcp.md](aufgaben-journal-mcp.md) |
| Was eine eigene KI von Grow OS lesen darf — und warum sie nichts schalten kann | [aufgaben-journal-mcp.md](aufgaben-journal-mcp.md) |

## Die zehn Seiten

| Seite | Worum es geht |
|---|---|
| [live-und-messen.md](live-und-messen.md) | Live-Bildschirm, Messformular, Messprotokoll — Bewertung, Herkunft, Score |
| [dosierung-und-addback.md](dosierung-und-addback.md) | Dosierpumpen, gelernte Mengen, A+B, Addback, Wasserwechsel, Pumpen-Wächter |
| [crop-steering.md](crop-steering.md) | Nachtabsenkungs-Rampe und der Kühler-Regler mit Kompressorschutz |
| [sollwerte-und-wissen.md](sollwerte-und-wissen.md) | Woher jeder Zielwert kommt, Knowledge-Sync, Einkaufsliste |
| [diagnose-und-risiken.md](diagnose-und-risiken.md) | Abweichungen, Behandlungen, Risiko-Ereignisse, Trends über Tage |
| [grows-sorten-pflanzen.md](grows-sorten-pflanzen.md) | Phasen, Flip, Zeitstrahl, Pflanzen je Sorte, Sortenbibliothek, Pheno Hunt |
| [ernte-trocknen-aushaerten.md](ernte-trocknen-aushaerten.md) | Ernte-Formular, Trocknungsfenster, Glas und Lüft-Rhythmus, Archiv und Kosten |
| [zelte-hydro-wasser.md](zelte-hydro-wasser.md) | Die Anlage: Zelte, Hydro-Systeme, eTape-Kalibrierung, Wasserprofil |
| [home-assistant-und-automatik.md](home-assistant-und-automatik.md) | Anbindung, Entitäts-Zuordnung, Grenzwerte, Push, die fünf Hintergrunddienste |
| [aufgaben-journal-mcp.md](aufgaben-journal-mcp.md) | Aufgabenseite, Journal und Fotos, Grow MCP, Mappe für eigene KI |

## Wenn nichts davon passt

`grow-os/CHANGELOG.md` (2325 Zeilen, englisch) hält jede Änderung mit ihrem Grund
fest — die beste Antwort auf „warum ist das so". Für das zweite Add-on:
`grow-mcp/CHANGELOG.md`.
