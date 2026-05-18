# Backup und Restore

Grow OS speichert lokale Betriebsdaten primär im Projekt unter `GrowDiary.Web/App_Data`. Backups sollten regelmäßig und besonders vor Updates erstellt werden.

## 1. Was muss gesichert werden?

Mindestens sichern:

- `GrowDiary.Web/App_Data/grow-diary.db`
- `GrowDiary.Web/App_Data/grow-diary.db-wal`, falls vorhanden
- `GrowDiary.Web/App_Data/grow-diary.db-shm`, falls vorhanden
- `GrowDiary.Web/App_Data/knowledge/`
- `GrowDiary.Web/App_Data/ha-config.json`, falls genutzt
- `GrowDiary.Web/App_Data/DataProtectionKeys/`, falls vorhanden
- `GrowDiary.Web/App_Data/snapshots/`, falls vorhanden
- weitere lokale Runtime-Dateien unter `GrowDiary.Web/App_Data`

Zusätzlich prüfen:

- `GrowDiary.Web/wwwroot/uploads/` für Fotos und Uploads. Der aktuelle Code speichert Uploads dort.
- `GrowDiary.Web/App_Data/uploads/`, falls dieser Ordner lokal existiert.

## 2. Wichtig: App stoppen

Für saubere SQLite-Backups ist der einfachste sichere Weg:

1. App oder Dienst stoppen.
2. `GrowDiary.Web/App_Data` kopieren.
3. Optional `GrowDiary.Web/wwwroot/uploads` kopieren, falls Fotos/Uploads genutzt werden.
4. App oder Dienst wieder starten.

Wenn die App während des Backups läuft, können SQLite-WAL/SHM-Dateien relevant sein. Kopiere dann nicht nur die `.db`, sondern auch vorhandene `-wal` und `-shm` Dateien. Besser ist trotzdem ein Backup bei gestoppter App.

## 3. Backup unter Windows

Beispiel aus dem Repo-Root:

```powershell
$date = Get-Date -Format "yyyyMMdd-HHmmss"
$target = "D:\GrowOS-Backups\$date"
New-Item -ItemType Directory -Path $target -Force
Copy-Item -Path "GrowDiary.Web\App_Data" -Destination $target -Recurse
```

Falls Uploads/Fotos genutzt werden:

```powershell
Copy-Item -Path "GrowDiary.Web\wwwroot\uploads" -Destination $target -Recurse
```

Der Upload-Ordner existiert nur, wenn bereits Fotos oder Uploads gespeichert wurden.

## 4. Backup unter Linux oder Raspberry Pi

Beispiel aus dem Repo-Root:

```bash
date=$(date +%Y%m%d-%H%M%S)
target="$HOME/growos-backups/$date"
mkdir -p "$target"
cp -a GrowDiary.Web/App_Data "$target/"
```

Falls Uploads/Fotos genutzt werden:

```bash
cp -a GrowDiary.Web/wwwroot/uploads "$target/"
```

Alternative als Archiv:

```bash
tar -czf "$HOME/growos-backup-$date.tar.gz" GrowDiary.Web/App_Data GrowDiary.Web/wwwroot/uploads
```

Wenn `GrowDiary.Web/wwwroot/uploads` nicht existiert, den Pfad im `tar`-Befehl weglassen.

## 5. Restore

Sicherer Restore-Ablauf:

1. App oder Dienst stoppen.
2. Bestehendes `GrowDiary.Web/App_Data` sichern oder umbenennen.
3. Backup von `App_Data` nach `GrowDiary.Web/App_Data` zurückkopieren.
4. Falls vorhanden, Upload-Backup nach `GrowDiary.Web/wwwroot/uploads` zurückkopieren.
5. Dateirechte prüfen, besonders unter Linux/Raspberry Pi.
6. App starten.
7. App öffnen und prüfen: Dashboard, Settings, Home Assistant Verbindung, Fotos/Snapshots.

Vor einem Restore nie die einzige vorhandene Datenkopie löschen. Erst umbenennen oder separat sichern.

## 6. Updates

Vor diesen Schritten Backup machen:

- `git pull`
- Frontend-Build mit `npm run build`
- größere .NET Updates
- Wechsel auf neue Releases
- manuelle Änderungen an `App_Data/knowledge`

Wenn nach einem Update Probleme auftreten:

1. App stoppen.
2. aktuelles `App_Data` sichern.
3. Backup zurückspielen.
4. App starten und prüfen.

## 7. Was muss nicht ins Backup?

Normalerweise nicht nötig:

- `bin/`
- `obj/`
- `node_modules/`
- `GrowDiary.Web/wwwroot/assets`, wenn aus Source neu buildbar
- `.git/`
- lokale Editor- oder Claude-/Codex-Dateien

Optional:

- Logs, falls sie für Fehlersuche gebraucht werden.

## 8. Privacy

Backups können private Grow-Daten enthalten:

- Messwerte
- Journal- und Task-Daten
- Fotos und Snapshots
- Home Assistant Konfiguration und Tokens
- Knowledge-Anpassungen

Speichere Backups privat. Nutze Verschlüsselung, wenn Backups in Cloud-Speicher, auf externe Datenträger oder auf andere Server kopiert werden. Teile Backups nicht öffentlich und hänge sie nicht ungeprüft an Issues.
