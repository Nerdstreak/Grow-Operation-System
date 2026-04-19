# Grow Diary Web

Lokale ASP.NET Core Web-App für dein Grow-Tagebuch.

## Start in VS Code

```bash
cd GrowDiary.Web

dotnet restore
dotnet run
```

Danach im Browser öffnen:

- `http://localhost:5076`
- oder die URL, die `dotnet run` ausgibt

## Neu in dieser Version

- Dashboard **Heute** mit Zelt-Karten
- echte **Zelt-Ebene** mit Live-Werten, Charts und Grow-Karten
- überarbeitetes **Grow-Detail** mit klaren Bereichen
- **Dark/Light Toggle**
- **Home-Assistant-Einstellungen** in der App
- Standard-Zelte: **Hauptzelt** und **Anzuchtzelt**
- bestehende Datenbank bereits übernommen

## Datenbank

Die App nutzt standardmäßig:

- `App_Data/grow-diary.db`

Als Backup liegt zusätzlich die vorherige Datei hier:

- `App_Data/legacy-grow-diary.db`

## Konfiguration

Die schnellste Methode: eine Datei `App_Data/ha-config.json` anlegen.
Sie wird beim Start automatisch eingelesen und in die Datenbank geschrieben.

1. Vorlage kopieren: `App_Data/ha-config.example.json` → `App_Data/ha-config.json`
2. `url` auf deine Home-Assistant-Instanz setzen (z. B. `http://192.168.178.68:8123/api/`)
3. `token` mit einem Long-Lived Access Token aus HA befüllen
4. Entity-IDs pro Zelt eintragen (nur die, die du brauchst)
5. App neu starten — fertig

Die Datei überschreibt beim Start immer die DB-Werte.
`ha-config.json` ist in `.gitignore` eingetragen und landet nie im Repository.

Alternativ: Verbindung und Entity-IDs direkt in der App unter **Einstellungen → Home Assistant & Zelt-Mapping** eintragen.

## Home Assistant

Eine ausführliche Anleitung findest du in:

- `HOME_ASSISTANT_SETUP.md`
