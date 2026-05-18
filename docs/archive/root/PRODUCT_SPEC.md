# PRODUCT_SPEC – Grow Operation System

## Vision
Ein kostenloser, self-hosted Grow-Manager für Cannabis-Grower. Die App verbindet Home Assistant Sensordaten mit aktivem Dokumentieren und intelligenter Vorhersage – damit der Grower seinen Grow jederzeit im Griff hat, egal ob er davor steht oder im Urlaub ist.

## Was die App ist
Eine Remote Grow Management App. Observieren, Steuern und Dokumentieren in einem Tool.

## Was die App nicht ist
- Kein SaaS, kein Abo, keine Cloud
- Keine Konkurrenz zu Home Assistant – sie ist dessen Interface für Grower
- Kein Ersatz für Grower-Wissen – sie unterstützt und erinnert, entscheidet nicht

## Lizenz & Community
Open Source, kostenlos, Community-first. Kein kommzieller Hintergrund.

## Zielnutzer
Self-hosted, technisch minimal affin. Kann Home Assistant einrichten. Bereit für einmaliges Setup. Legt Wert auf Datenschutz und Kostenlosigkeit.

## Primäre Nutzungsszenarien
1. Morgens vor der Pflanze stehen – Messung eintragen, Tagesempfehlung sehen
2. Unterwegs oder im Urlaub – Status checken, Alarm bekommen, remote eingreifen
3. Terminal im Growraum – 24/7 Live-Dashboard, Alarme visuell und per Notification

## Interface-Modi
Die App hat zwei Modi mit denselben Daten aber verschiedenen Interfaces:

- **Dashboard-Modus** (Desktop/Terminal): Große Kacheln, Live-Daten, kein Scrollen nötig, läuft dauerhaft, für den Bildschirm im Growraum
- **Action-Modus** (Handy): Schnelle Eingabe, Kamera, Spracheingabe, eine Aufgabe pro Screen, für unterwegs

## Home Assistant
- Empfohlene und aktuell primaere Integrationsquelle fuer Sensor- und Statusdaten
- Fuer reine Dokumentation kann Grow OS technisch auch ohne HA starten, der Hauptnutzen entsteht aber mit HA
- Live-Daten, Sensorpipeline, LightTransitions und AutoMeasurements sind ohne HA stark eingeschraenkt
- Onboarding fuehrt frueh zur HA-Verbindung
- App funktioniert bei temporaer nicht erreichbarem HA, zeigt aber klar an was live ist und was cached
- Alarme laufen ueber HA-Automationen (Telegram, Pushover, Signal etc.)
- Die App definiert Schwellenwerte, HA liefert die Benachrichtigung

## Medium-Strategie
- **Aktueller Launch- und Produktfokus: RDWC und DWC** - vollstaendig mit Sollwerten, Vorhersage-Mechanik, Addback-Logik und Expertenregeln.
- Andere Medien und Hydro-Stile sind bewusst ausserhalb des aktuellen MVP-Flows.
- Im normalen UI gibt es keine Soil-, Coco-, NFT-, Aeroponic- oder sonstigen Nicht-DWC/RDWC-Optionen.
- Nie halbfertige Medien launchen - lieber weniger, aber vollstaendig.

## Vorhersage-Mechanik (Herzstück)
- Basiert auf echtem Expertenwissen: Growpläne, SOPs, Messprotokoll-Logik
- Zeigt pro Woche und Phase konkrete Sollwerte (pH, EC, ORP, DO, Wassertemp, VPD, PPFD, CO₂)
- Vergleicht Istwerte mit Sollwerten
- Gibt konkrete Handlungsempfehlungen – nicht "pH erhöht" sondern "pH seit 2 Tagen über 6.2 – jetzt korrigieren, Calciumaufnahme blockiert"
- Addback-Logik für RDWC/DWC: EC-Verlauf analysieren, Empfehlung berechnen

## Technologie
- ASP.NET Core 8, SQLite, Self-hosted
- Bestehendes Projekt wird refactored, nicht neu geschrieben
- Keine externen Abhängigkeiten außer Microsoft.Data.Sqlite

## Designprinzipien
1. Zwei Modi, eine App – Dashboard und Action haben verschiedene Interfaces aber dieselben Daten
2. HA ist die Wahrheit – die App ist das Interface, nicht die Datenquelle
3. Konkrete Handlungen statt abstrakte Warnungen
4. Mobile-first für Eingabe, Desktop-first für Übersicht
5. Setup einmal, danach läuft es – minimaler täglicher Aufwand
6. Lieber weniger Features, aber jedes Feature zu Ende gedacht

## Was als nächstes gebaut wird (Sprint 1 – Datenmodell)
- DWC/RDWC als verbindlicher MVP-Flow in UI, API und Validierung
- GrowthProfile auf DWC/RDWC-Regeln ausrichten und String-Checks entfernen
- PPFD und CO₂ in Measurement ergänzen
- Wasserquelle (RO / Leitungswasser / gemischt) als Grow-Eigenschaft
- Weitere Medien und Hydro-Stile erst modellieren, wenn die jeweiligen Workflows vollständig definiert sind
