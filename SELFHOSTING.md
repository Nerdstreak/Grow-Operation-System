# Selfhosting und Remote-Zugriff

Grow Operation System ist als selfhosted Community-App gedacht. Der Standardbetrieb ist lokal oder im eigenen Heimnetz. Remote-Zugriff ist optional und sollte nur bewusst mit zusätzlichem Schutz eingerichtet werden.

## 1. Grundprinzip

- Grow OS ist keine SaaS- oder Cloud-App.
- Die App läuft auf deinem eigenen Rechner, Server, Mini-PC oder Raspberry Pi.
- Daten liegen lokal unter `GrowDiary.Web/App_Data`.
- Die Standarddatenbank ist SQLite unter `GrowDiary.Web/App_Data/grow-diary.db`.
- Home Assistant ist optional, aber für Sensordaten im Zielbetrieb empfohlen.

## 2. Lokaler Zugriff

Für Entwicklung, Test und lokale Nutzung reicht der Start auf demselben Rechner:

```bash
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Danach ist die App lokal erreichbar unter:

- `http://localhost:5076`

Dieser Modus eignet sich für Entwicklung, erste Tests und lokale Einzelplatznutzung.

## 3. LAN-Zugriff

Laut `GrowDiary.Web/appsettings.json` nutzt das Backend standardmäßig:

- `http://0.0.0.0:5076`

Damit kann die App im Heimnetz typischerweise über die IP-Adresse des Servers geöffnet werden:

- `http://<server-ip>:5076`

Wichtig:

- Der Server und das Endgerät müssen im selben Netzwerk sein.
- Die lokale Firewall muss Port `5076` erlauben.
- Das ist für Heimnetz/LAN gedacht, nicht als öffentliche Internet-Freigabe.
- Stelle Port `5076` nicht direkt ungeschützt ins Internet.

## 4. PWA im Heimnetz

Für mobile Nutzung im Heimnetz:

1. Handy oder Tablet mit demselben WLAN verbinden.
2. `http://<server-ip>:5076/action` im Browser öffnen.
3. Im Browser-Menü `Zum Home-Bildschirm hinzufügen` oder `App installieren` wählen.

`/action` ist als mobile Startseite gedacht. Sie bündelt die wichtigsten schnellen Aktionen und ist die `start_url` im Manifest.

Hinweis: Für volle PWA-Installierbarkeit ist außerhalb von `localhost` in der Praxis HTTPS wichtig. Im reinen Heimnetz verhalten sich Browser je nach Plattform unterschiedlich streng.

## 5. Remote-Zugriff Optionen

### Option A: Tailscale oder VPN

Empfohlen für private Nutzung.

- Kein öffentlicher Port nötig.
- Handy, Laptop und Server werden ins private VPN eingebunden.
- Zugriff erfolgt anschließend über die interne VPN- oder Server-IP.
- Geeignet für Home-Server, Raspberry Pi oder Mini-PC im Heimnetz.

### Option B: Reverse Proxy mit eigener Domain

Möglich, aber nur mit Schutz davor.

- HTTPS verwenden.
- Einen Reverse Proxy wie Caddy, nginx, Traefik oder einen bestehenden Homeserver-Proxy vorschalten.
- Zugriff zusätzlich absichern, zum Beispiel mit Basic Auth, SSO oder einer anderen Proxy-Auth.
- Port `5076` nicht direkt ins Internet stellen.

### Option C: Cloudflare Tunnel

Möglich, aber nicht ungeschützt veröffentlichen.

- Tunnel nur mit Cloudflare Access oder vergleichbarer Auth verwenden.
- Keine öffentliche, frei erreichbare URL ohne Zugriffsschutz betreiben.
- Besonders auf sensible Daten wie Home Assistant Tokens, Fotos und Grow-Daten achten.

### Option D: Direktes Port Forwarding

Nicht empfohlen.

- Kein direkter Router-Portforward auf `5076`.
- Keine ungeschützte Veröffentlichung der App im Internet.
- Ohne vorgeschaltete Auth/HTTPS ist das für private Grow- und Sensordaten ungeeignet.

## 6. Admin- und Settings-Zugriff

Die bestehende Implementierung schützt administrative Bereiche lokal:

- `/settings`
- `/einstellungen`
- `/api/settings`

Standardmäßig sind diese Pfade nur von Loopback oder derselben lokalen Verbindung erreichbar. Es gibt zusätzlich die Umgebungsvariable:

- `GROWDIARY_ALLOW_REMOTE_ADMIN=true`

Setze diese Variable nur bewusst. Bei Remote-Zugriff darf sie nicht ohne vorgeschaltete Auth, VPN oder anderen Zugriffsschutz verwendet werden.

Wichtig: Daraus folgt keine vollständige Nutzerverwaltung. Grow OS hat aktuell keine allgemeine Login-/User-Authentifizierung für alle Bereiche.

## 7. Security-Hinweise

- Home Assistant Long-Lived Access Tokens sind sensibel.
- `GrowDiary.Web/App_Data` enthält private Daten und sollte nicht committed werden.
- SQLite-Datenbank, Knowledge-Anpassungen, Fotos, Snapshots und Runtime-Daten privat behandeln.
- Backups verschlüsselt oder anderweitig geschützt ablegen.
- Remote-Zugriff nur über VPN oder mit vorgeschalteter Auth/Reverse Proxy einrichten.
- Nicht davon ausgehen, dass eine interne Heimnetz-App automatisch internet-tauglich ist.

## 8. Beispiel-Topologien

| Topologie | Einsatz | Bewertung |
|---|---|---|
| Lokaler Windows-PC | Entwicklung, Test, Einzelplatz | Einfachster Start |
| Raspberry Pi im Heimnetz | Dauerbetrieb im LAN | Sinnvoll, wenn Performance reicht |
| Mini-PC oder Home-Server | Dauerbetrieb mit mehr Reserven | Gute Selfhosting-Basis |
| Pi/Server plus Tailscale | Privater Remote-Zugriff | Bevorzugte Remote-Variante |
| Server plus Reverse Proxy, HTTPS und Auth | Fortgeschrittener Remote-Betrieb | Möglich, aber sauber absichern |

## 9. Grenzen aktueller Stand

- Kein offiziell dokumentierter Docker-Betrieb.
- Kein offizieller `systemd` Service.
- Kein offizieller Windows Service.
- Keine vollständige eingebaute Login-/User-Authentifizierung.
- Kein Service Worker und kein Offline-Modus.
- Keine vollständige Security- oder Backup-/Restore-Dokumentation.

## 10. Links

- [README.md](README.md)
- [INSTALL.md](INSTALL.md)
- [SECURITY.md](SECURITY.md)
- [BACKUP_RESTORE.md](BACKUP_RESTORE.md)
- [HOME_ASSISTANT.md](HOME_ASSISTANT.md)
