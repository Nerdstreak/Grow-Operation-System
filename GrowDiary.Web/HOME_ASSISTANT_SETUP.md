# Home Assistant Setup

## 1. Home Assistant URL und Token

In Home Assistant:

1. Profil öffnen
2. **Long-Lived Access Token** erstellen
3. URL deiner Instanz notieren, z. B.
   - `http://homeassistant.local:8123`
   - oder `http://192.168.x.x:8123`

Diese beiden Werte trägst du in der Grow-App unter **Einstellungen** ein.

## 2. Hauptzelt mit AC Infinity

Empfohlene Richtung:

- AC Infinity Integration in Home Assistant einbinden
- danach die relevanten Entities im **Hauptzelt** mappen

Sinnvolle Entities:

- Temperatur
- Luftfeuchte
- VPD
- Lichtstatus
- optional weitere Sensoren aus deinem Setup

## 3. Anzuchtzelt mit Govee

Für das Anzuchtzelt reicht meistens:

- Temperatur Entity
- Luftfeuchte Entity

Wenn Home Assistant den Govee-Sensor sauber erkennt, tauchen diese Entitäten dort auf und können im **Anzuchtzelt** hinterlegt werden.

## 4. RDWC / Hydro Sensoren

Diese Werte nur in dem Zelt mappen, in dem das RDWC-System tatsächlich läuft:

- Reservoir pH
- Reservoir EC
- Wasserstand
- Wassertemperatur

## 5. Entity-IDs finden

In Home Assistant:

1. **Entwicklerwerkzeuge** öffnen
2. bei **Zustände** nach `ac_infinity`, `govee`, `ph`, `ec`, `water`, `temp`, `humidity` suchen
3. die passenden Entity-IDs in die Grow-App übernehmen

## 6. Danach testen

- Grow-App neu laden
- Zelt öffnen
- prüfen, ob Werte auf den Karten auftauchen
- danach sammelt die App bei Aufrufen kleine Snapshot-Historien für Charts
