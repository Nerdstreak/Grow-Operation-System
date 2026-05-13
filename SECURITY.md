# Security

Grow Operation System ist ein selfhosted Community-Projekt. Diese Hinweise beschreiben den aktuellen Sicherheitsstand und sichere Betriebsgrundsätze. Sie ersetzen kein eigenes Hardening für öffentlich erreichbare Server.

## 1. Security-Modell

- Grow OS ist für lokalen Betrieb und LAN-Betrieb gedacht.
- Es ist keine öffentliche SaaS- oder Cloud-App.
- Es gibt aktuell keine vollständige eingebaute Nutzerverwaltung und keine allgemeine Login-Authentifizierung für alle Bereiche.
- Remote-Zugriff sollte nur über VPN oder mit vorgeschalteter Auth/Reverse Proxy erfolgen.
- Stelle die App nicht direkt und ungeschützt ins Internet.

## 2. Sensible Daten

Behandle diese Daten als privat:

- Home Assistant Long-Lived Access Token.
- SQLite-Datenbank unter `GrowDiary.Web/App_Data/grow-diary.db`.
- SQLite-WAL/SHM-Dateien wie `grow-diary.db-wal` und `grow-diary.db-shm`.
- Lokale Runtime-Daten unter `GrowDiary.Web/App_Data`.
- Knowledge-Anpassungen unter `GrowDiary.Web/App_Data/knowledge`.
- DataProtection-Keys unter `GrowDiary.Web/App_Data/DataProtectionKeys`.
- Snapshots unter `GrowDiary.Web/App_Data/snapshots`, falls vorhanden.
- Uploads und Fotos. Aktuell speichert der Code Uploads unter `GrowDiary.Web/wwwroot/uploads`.
- Logs, falls du sie lokal aktivierst oder über einen Prozessmanager sammelst.

## 3. Remote-Zugriff

Empfohlene Reihenfolge:

1. Tailscale oder VPN für private Nutzung.
2. Reverse Proxy mit eigener Domain, HTTPS und vorgeschalteter Auth.
3. Cloudflare Tunnel nur mit Cloudflare Access oder vergleichbarer Auth.

Nicht empfohlen:

- Direktes Port Forwarding auf `5076`.
- Öffentliche, frei erreichbare URL ohne Zugriffsschutz.
- Remote-Admin ohne VPN, Proxy-Auth oder vergleichbaren Schutz.

Weitere Details stehen in [SELFHOSTING.md](SELFHOSTING.md).

## 4. Settings und Admin-Bereiche

Die bestehende Implementierung schützt administrative Pfade lokal:

- `/settings`
- `/einstellungen`
- `/api/settings`

Standardmäßig sind diese Pfade nur lokal erreichbar. Die Umgebungsvariable `GROWDIARY_ALLOW_REMOTE_ADMIN=true` kann Remote-Zugriff auf diese Bereiche erlauben.

Setze `GROWDIARY_ALLOW_REMOTE_ADMIN=true` nur bewusst und nicht bei ungeschütztem Remote-Betrieb. Die Variable ersetzt keine vollständige Authentifizierung.

## 4.1 Linux-Dienstbetrieb

Wenn Grow OS unter Linux oder auf einem Raspberry Pi dauerhaft läuft, sollte der Dienst nicht als `root` ausgeführt werden. Verwende einen eigenen Service-Nutzer, zum Beispiel `growos`, und gib diesem Nutzer nur Schreibrechte auf den Datenordner wie `/var/lib/grow-os`.

Das `systemd`-Beispiel in [DEPLOYMENT.md](DEPLOYMENT.md) nutzt einen eigenen Nutzer und trennt App-Dateien unter `/opt/grow-os/app` von lokalen Daten unter `/var/lib/grow-os`.

## 5. Git- und Repo-Sicherheit

Nicht committen:

- `GrowDiary.Web/App_Data`
- `GrowDiary.Web/App_Data/ha-config.json`
- SQLite-Dateien wie `grow-diary.db`, `grow-diary.db-wal`, `grow-diary.db-shm`
- Fotos, Uploads und Snapshots
- echte Logs mit privaten Daten
- `.claude/settings.local.json`

Beispielkonfigurationen sind nur dann geeignet, wenn sie keine echten Secrets, Tokens, privaten IPs oder privaten URLs enthalten.

## 6. Home Assistant Token

- Verwende einen Long-Lived Access Token nur für die benötigte Home Assistant Instanz.
- Nutze nach Möglichkeit einen dedizierten HA-Benutzer mit möglichst geringem Zugriff.
- Erneuere den Token, wenn er versehentlich geteilt oder kompromittiert wurde.
- Teile Tokens nicht in GitHub Issues, Screenshots, Logs oder Chat-Ausgaben.
- Speichere `ha-config.json` nur lokal und nicht im Repository.

## 7. Updates

- Vor Updates ein Backup erstellen.
- Release Notes oder Changelog lesen, sobald sie vorhanden sind.
- Keine fremden Forks, Releases oder Skripte ohne Prüfung ausführen.
- Nach Updates prüfen, ob App, Settings und Home Assistant Verbindung noch funktionieren.

Backup-Hinweise stehen in [BACKUP_RESTORE.md](BACKUP_RESTORE.md).

## 8. Security Issues melden

Aktuell gibt es noch keinen formalen Security-Policy-Prozess und keine separate Security-Kontaktadresse in diesem Repository.

Bis ein Prozess dokumentiert ist:

- Melde sicherheitsrelevante Probleme nicht mit echten Tokens, Datenbanken oder privaten Screenshots.
- Nutze GitHub Issues nur ohne sensible Details.
- Falls ein privater Kontaktweg im Projekt ergänzt wird, diesen für vertrauliche Meldungen verwenden.
