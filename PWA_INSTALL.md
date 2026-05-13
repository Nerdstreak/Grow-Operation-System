# PWA Installation

Grow OS ist als Progressive Web App gedacht. Das bedeutet: Die Web-App kann auf Smartphone, Tablet oder Desktop wie eine App gestartet werden, ohne App Store und ohne native iOS-/Android-App.

## Voraussetzungen

- Grow OS muss im Browser erreichbar sein.
- Lokal funktioniert `http://localhost:5076`.
- Im Heimnetz kann `http://<server-ip>:5076` funktionieren, je nach Browser und Plattform.
- Fuer Remote-Zugriff ist in der Praxis HTTPS noetig, zum Beispiel ueber VPN, Tailscale oder einen Reverse Proxy mit TLS.
- Die App darf nicht ungeschuetzt ins Internet gestellt werden.

## iPhone und iPad

1. Safari oeffnen.
2. Die Grow-OS-URL oeffnen, idealerweise direkt `/action`.
3. Teilen-Menue oeffnen.
4. `Zum Home-Bildschirm` waehlen.
5. Namen bestaetigen.

iOS nutzt Safari fuer diese Installation. Andere Browser koennen sich anders verhalten.

## Android und Chrome

1. Chrome oder einen kompatiblen Browser oeffnen.
2. Die Grow-OS-URL oeffnen, idealerweise direkt `/action`.
3. Im Browser-Menue `App installieren` oder `Zum Startbildschirm hinzufuegen` waehlen.
4. Installation bestaetigen.

## Desktop

Moderne Browser zeigen bei installierbaren Web-Apps oft ein Installationssymbol in der Adressleiste oder einen Menuepunkt `App installieren`.

## Was offline funktioniert

PWA-2 ergaenzt ein vorsichtiges Offline-Minimum:

- App-Shell und Grundoberflaeche koennen aus dem Cache geladen werden.
- Statische Assets, Icons und die Offline-Hinweisseite werden gecacht.
- Bei fehlender Verbindung kann eine Offline-Seite angezeigt werden.

## Was offline nicht funktioniert

Grow OS ist kein vollstaendiger Offline-Client. Offline werden keine Grow-, Sensor- oder Home-Assistant-Daten vorgetaeuscht.

Nicht offline verfuegbar:

- API-Aufrufe
- Home Assistant Livewerte
- Speichern von Messungen, Journal, Tasks oder SOP-Schritten
- SOP-Updates
- RiskEvents
- Uploads, Fotos und Snapshots
- Hintergrund-Synchronisation
- Push Notifications

## Cache-Hinweise

Wenn eine installierte PWA nach Updates alte Dateien zeigt:

1. Browser-App-Daten fuer Grow OS loeschen oder PWA deinstallieren.
2. Browser neu oeffnen.
3. Grow OS erneut oeffnen und bei Bedarf neu installieren.

Der Service Worker cacht keine `/api`-Antworten, keine Uploads und keine lokalen Runtime-Daten.

## Links

- [README.md](README.md)
- [INSTALL.md](INSTALL.md)
- [SELFHOSTING.md](SELFHOSTING.md)
- [SECURITY.md](SECURITY.md)
- [DEPLOYMENT.md](DEPLOYMENT.md)
