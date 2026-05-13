# Roadmap

Diese Roadmap beschreibt den aktuellen Stand und die geplante Richtung von Grow Operation System. Sie ist nicht final; Community-Feedback ist willkommen.

## 1. Projektziel

Grow OS ist ein selfhosted Grow Operation System für lokale Nutzung zuerst.

- Selfhosting statt SaaS- oder Cloud-Pflicht.
- Lokaler Betrieb und LAN-Betrieb als Standard.
- Mobile Nutzung als PWA statt native App-Store-App.
- Home Assistant als zentrale Integrationsquelle für Sensor- und Statuswerte.
- Fokus auf RDWC/DWC, mit Erweiterung auf weitere Medien erst bei ausreichend sauberem Fachmodell.

## 2. Aktueller MVP-Stand

Erledigt oder im aktuellen MVP vorhanden:

- Operations Dashboard als Tageszentrale
- Mobile Action Hub unter `/action`
- Live Dashboard unter `/live`
- Zelte und Setups
- Grow-Dokumentation
- Messungen
- AutoMeasurements
- LightTransitions
- Deviation/Diagnose
- Treatment- und SOP-Empfehlungen
- ausführbare SOPs mit Steps, Scheduling und Reminder-Projektion
- Hardware-Inventar
- MaintenanceEvents
- CalibrationEvents
- RiskEvents
- RiskEvent zu Emergency-SOP
- Knowledge-Browser
- Basic PWA Installability
- Selfhosting-Dokumentation

## 3. Kurzfristige nächste Schritte

- PWA-2: Service Worker und App-Shell Cache vorsichtig umsetzen.
- echte Icons und Branding finalisieren.
- Mobile Flows vertiefen:
  - Messung eintragen
  - SOP-Step abarbeiten
  - Foto hinzufügen
  - Risk bestätigen
- Dashboard-Modus mit echten Live-Testdaten prüfen.
- HardwarePage auf Mobile bei Formularen weiter entspannen.
- CI/GitHub Actions einrichten.
- Release-/ZIP-Struktur definieren.

## 4. Mittelfristig

- Docker-Dokumentation und später Docker-Setup.
- `systemd` Service für Linux/Raspberry Pi.
- Windows Service oder klarer Prozessmanager-Ansatz.
- Backup/Restore UI oder Export.
- Home Assistant Auto-Detection für RiskEvents.
- SensorTrustScore.
- bessere Sensor-Calibration-Intervalle aus WearTemplates.
- Grow-Vergleich und Analyse erweitern.
- Kamera- und Galerie-Verbesserungen.
- Community Knowledge Packs.

## 5. Langfristig

- Multi-user/Auth-Konzept oder Reverse-Proxy-kompatible Auth-Dokumentation.
- Plugin-System.
- bessere PWA Offline-Erfahrung.
- optionale AI/Knowledge Assistenz.
- mehr Grow-Medien außer RDWC/DWC.
- Docker Compose oder One-click Selfhost.
- echte Release-Channels.

## 6. Nicht-Ziele aktuell

- keine native App-Store-App.
- kein SaaS-Zwang.
- keine Cloud-Pflicht.
- keine automatische gefährliche Hardware-Steuerung ohne Nutzerkontrolle.
- keine ungeprüfte automatische Recovery.

## 7. Status-Hinweis

Die Roadmap ist eine Arbeitsrichtung, kein festes Release-Versprechen. Prioritäten können sich durch Tests, reale Selfhosting-Erfahrungen und Community-Feedback ändern.
