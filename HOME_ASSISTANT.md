# Home Assistant

Diese Anleitung beschreibt die Home-Assistant-Anbindung von Grow Operation System. Home Assistant liefert Sensor- und Statuswerte; Grow OS nutzt sie für Live-Ansichten, Historie und Automatisierung.

## 1. Zweck der Integration

Grow OS nutzt Home Assistant Werte für:

- Live Dashboard unter `/live`
- Zelte und TentDetail-Ansichten
- `TentSensorReadings`
- tägliche Sensorstatistiken
- AutoMeasurements
- LightTransitions
- Kamera-Snapshots, falls eine Kamera-Entity konfiguriert ist
- Risk-/Hardware-Kontext später

Home Assistant ist technisch nicht für jeden App-Start zwingend, aber für den vollen Nutzen empfohlen.

## 2. Voraussetzungen

- laufende Home Assistant Instanz
- Netzwerkzugriff vom Grow-OS-Server auf Home Assistant
- Long-Lived Access Token
- vorhandene Entitäten/Sensoren in Home Assistant
- korrekte Uhrzeit und Zeitzone auf Grow-OS-Server und Home Assistant

Wichtig: Bei PWA- oder Remote-Nutzung muss der Grow-OS-Server Home Assistant erreichen. Es reicht nicht, dass das Handy Home Assistant erreichen kann.

## 3. Long-Lived Access Token erstellen

Der genaue Menüpfad kann je nach Home-Assistant-Version abweichen. Allgemein:

1. In Home Assistant das eigene Profil öffnen.
2. Bereich für Long-Lived Access Tokens suchen.
3. Neuen Token erstellen.
4. Token direkt kopieren.
5. Token sicher speichern.

Teile den Token nicht in Git, Issues, Screenshots, Logs oder Chat-Ausgaben. Wenn ein Token versehentlich öffentlich wurde, in Home Assistant widerrufen und neu erstellen.

## 4. Verbindung in Grow OS konfigurieren

1. Grow OS öffnen.
2. `/settings` öffnen.
3. Bereich `Home Assistant` öffnen.
4. Base URL eintragen.
5. Access Token eintragen.
6. Speichern.

Beispiele für Base URLs:

- `http://homeassistant.local:8123`
- `http://192.168.x.x:8123`

Die URL muss aus Sicht des Grow-OS-Servers erreichbar sein. Bei späterem Docker-, Reverse-Proxy- oder VPN-Betrieb können andere Netzwerkpfade nötig sein.

## 5. Sensor-Mapping

Sensoren werden pro Zelt gemappt:

- Entity ID aus Home Assistant übernehmen, zum Beispiel `sensor.grow_tent_temperature`.
- Passenden `MetricType` im Zelt auswählen.
- Optional ein Label setzen.
- Mapping aktivieren oder deaktivieren.

Aktuelle `SensorMetricType` Namen:

| MetricType | Typischer Zweck |
|---|---|
| `AirTemperature` | Lufttemperatur |
| `Humidity` | Luftfeuchte |
| `Vpd` | VPD |
| `Co2` | CO2 |
| `Ppfd` | PPFD |
| `LightStatus` | Lichtstatus für LightTransitions |
| `ReservoirPh` | Reservoir pH |
| `ReservoirEc` | Reservoir EC |
| `ReservoirOrp` | Reservoir ORP |
| `ReservoirDissolvedOxygen` | gelöster Sauerstoff / DO |
| `ReservoirWaterTemp` | Reservoir-Wassertemperatur |
| `ReservoirLevel` | Wasserstand |
| `PumpCirculation` | Umwälzpumpe |
| `PumpAir` | Luftpumpe |
| `Chiller` | Chiller |
| `UpsBattery` | UPS Batterie |
| `UpsStatus` | UPS Status |

Nicht-numerische States werden nicht als numerische SensorReadings gespeichert. Für `LightStatus` werden States separat für LightTransitions ausgewertet.

## 6. Empfohlene Entity-Beispiele

Die folgenden Entity IDs sind Beispiele. Sie setzen keinen bestimmten Hersteller voraus.

| Zweck | Beispiel-Entity |
|---|---|
| Lufttemperatur | `sensor.grow_tent_temperature` |
| Luftfeuchte | `sensor.grow_tent_humidity` |
| VPD | `sensor.grow_tent_vpd` |
| Lichtstatus | `switch.light` oder `sensor.light_status` |
| PPFD | `sensor.ppfd` |
| Reservoir pH | `sensor.reservoir_ph` |
| Reservoir EC | `sensor.reservoir_ec` |
| Reservoir Temperatur | `sensor.reservoir_temp` |
| DO | `sensor.dissolved_oxygen` |
| ORP | `sensor.orp` |
| Wasserstand | `sensor.reservoir_level` |
| Pumpe | `switch.circulation_pump` oder `binary_sensor.pump_status` |
| Chiller | `switch.chiller` oder `binary_sensor.chiller_status` |
| UPS | `sensor.ups_battery` / `sensor.ups_status` |
| Kamera | `camera.main_tent` |

Entity IDs findest du in Home Assistant typischerweise in den Entwicklerwerkzeugen bei den Zuständen oder in den Entitätsdetails.

## 7. LightStatus

`LightStatus` kann LightTransitions erzeugen. Die App normalisiert typische Zustände wie `on`, `off`, `true`, `false`, `1` und `0`, soweit sie vom Code erkannt werden.

LightTransitions dienen als Grundlage für zeitversetzte AutoMeasurements, zum Beispiel nach Licht an oder Licht aus. LightSchedules können zusätzlich genutzt werden, um Lichtpläne zu dokumentieren.

Es gibt aktuell keine automatische Recovery-Steuerung für Licht oder Geräte.

## 8. AutoMeasurements

AutoMeasurements nutzen gespeicherte Sensorwerte aus dem Zelt-Mapping.

- Konfigurationen werden im GrowDetail-Bereich `Automatisierung` eingerichtet.
- Trigger können `Manual`, `LightOnDelay` oder `LightOffDelay` sein.
- `DelayMinutes` verschiebt die Ausführung nach der LightTransition.
- Mappings verbinden Sensor-Metriken mit Measurement-Feldern.
- Aggregation kann `Latest`, `Median` oder `Average` sein.
- Required Mapping: Fehlt ein Pflichtwert, wird der Run übersprungen.
- Optional Mapping: Fehlende oder verworfene Werte blockieren den Run nicht alleine.
- Der Hard-Limit Guard verwirft offensichtliche Sensorfehler.

Aktuelle AutoMeasurement-Felder:

- `AirTemperatureC`
- `HumidityPercent`
- `ReservoirPh`
- `ReservoirEc`
- `ReservoirWaterTempC`
- `ReservoirLevelLiters`
- `ReservoirLevelCm`
- `DissolvedOxygenMgL`
- `OrpMv`
- `PpfdMol`
- `Co2Ppm`

## 9. Kamera und Snapshots

Pro Zelt kann eine Kamera-Entity eingetragen werden, zum Beispiel `camera.main_tent`.

Wenn Home Assistant konfiguriert ist und die Kamera erreichbar ist, kann Grow OS Kamera-Snapshots abrufen. Der Background Worker speichert lokale tägliche Snapshots unter `GrowDiary.Web/App_Data/snapshots/{tentId}`. Manuell hochgeladene Fotos/Uploads liegen aktuell unter `GrowDiary.Web/wwwroot/uploads`.

Fotos und Snapshots sind private Daten. `App_Data`, Uploads und echte lokale Konfigurationen nicht committen.

## 10. Troubleshooting

| Problem | mögliche Ursache | Lösung |
|---|---|---|
| Verbindung fehlgeschlagen | falsche URL, Home Assistant nicht erreichbar, Firewall blockiert | URL aus Sicht des Grow-OS-Servers prüfen, Port `8123` prüfen, Firewall prüfen |
| `401 Unauthorized` | Token falsch, abgelaufen oder widerrufen | neuen Long-Lived Access Token erstellen und in Grow OS speichern |
| Entity liefert keine Werte | falsche Entity ID, Sensor `unavailable`, Integration offline | Entity ID in Home Assistant prüfen, State ansehen, Integration reparieren |
| Werte werden nicht gespeichert | Mapping inaktiv, nicht-numerischer State, Worker noch nicht gelaufen | Mapping aktivieren, numerischen Sensor verwenden, einige Minuten warten |
| LightTransitions entstehen nicht | `LightStatus` nicht gemappt oder State nicht normalisierbar | passende LightStatus-Entity mappen, State auf `on/off` oder ähnliche klare Werte prüfen |
| AutoMeasurements laufen nicht | Config deaktiviert, kein Trigger, keine LightTransition, Pflichtwert fehlt | Config im GrowDetail prüfen, LightStatus prüfen, Required Mappings prüfen |
| PWA/Remote sieht HA nicht | Grow-OS-Server erreicht Home Assistant nicht | Netzwerkpfad vom Server zu HA prüfen, nicht vom Handy aus testen |
| Uhrzeiten wirken falsch | Serverzeit oder Zeitzone weicht ab | Uhrzeit/Zeitzone auf Server und Home Assistant prüfen |
| Kamera zeigt nichts | falsche Kamera-Entity, Token ohne Zugriff, Kamera liefert keinen Snapshot | Entity prüfen, HA-Kamera in Home Assistant testen, Token erneuern |

## 11. Security-Hinweise

- Home Assistant Tokens sind sensibel.
- Tokens nicht committen.
- Tokens nicht in Screenshots oder Issues posten.
- Bei Remote-Zugriff Grow OS absichern.
- `ha-config.json`, Datenbank, Snapshots und Uploads privat behandeln.

Weitere Hinweise stehen in [SECURITY.md](SECURITY.md).

## 12. Links

- [README.md](README.md)
- [INSTALL.md](INSTALL.md)
- [SELFHOSTING.md](SELFHOSTING.md)
- [SECURITY.md](SECURITY.md)
- [BACKUP_RESTORE.md](BACKUP_RESTORE.md)
