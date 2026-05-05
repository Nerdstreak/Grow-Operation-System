# Knowledge-Base – Standard-Inhalte

Dieses Verzeichnis enthält die mitgelieferten Standard-Definitionen für die Wissensdatenbank.

## Ordnerstruktur

| Ordner             | Inhalt                                                      |
|--------------------|-------------------------------------------------------------|
| `treatments/`      | Treatment-Definitionen (Spray, Reservoir-Additive, IPM …)   |
| `sops/`            | Standard-Operating-Procedures (lineare, mehrtägige, wiederkehrende) |
| `nutrient-programs/` | Nährstoffprogramme (Athena, HESI, GHF …)                 |
| `setpoints/`       | Sollwert-Profile (pH, EC, ORP, VPD, PPFD, CO₂ …)           |
| `pathogens/`       | Pathogen-Definitionen (Pilze, Bakterien, Insekten …)        |
| `symptoms/`        | Symptom-Definitionen mit möglichen Ursachen und Maßnahmen   |
| `wear/`            | Verschleiß-Templates (Sensoren, Pumpen, Filter …)           |

## Editieren

**Nicht direkt hier editieren.** Beim ersten App-Start werden alle Dateien nach
`App_Data/knowledge/` kopiert. Eigene Anpassungen gehören ausschließlich dorthin.

## Schema-Versionen

Jede JSON-Datei enthält ein `schemaVersion`-Feld (z.B. `"1.0"`).
Schema-Änderungen sind additiv – neue optionale Felder werden ergänzt, bestehende
Felder bleiben kompatibel. Die App migriert automatisch beim Start.
