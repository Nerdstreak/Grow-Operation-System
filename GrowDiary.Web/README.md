# Grow Diary Web

ASP.NET Core backend plus React SPA fuer das Grow-Tagebuch.

## Projektstruktur

- `GrowDiary.Web` - Backend, JSON-API, SQLite, Auslieferung der gebauten SPA
- `GrowDiary.React` - React/Vite-Quellcode
- `GrowDiary.Web.Tests` - Backend-Tests

## Backend starten

```bash
dotnet restore
dotnet run --project GrowDiary.Web/GrowDiary.Web.csproj
```

Danach die von `dotnet run` ausgegebene URL im Browser oeffnen.

## Frontend bauen

```bash
cd GrowDiary.React
npm install
npm run build
```

Der Vite-Build schreibt direkt nach `GrowDiary.Web/wwwroot`.

## Datenbank

Standardpfad:

- `GrowDiary.Web/App_Data/grow-diary.db`

Optional kann ein eigener Pfad ueber `GROWDIARY_DB_PATH` gesetzt werden.

## Home Assistant

Die schnellste Einrichtung erfolgt ueber `GrowDiary.Web/App_Data/ha-config.json`.
Die Datei wird beim Start eingelesen und in die Datenbank uebernommen.

1. `GrowDiary.Web/App_Data/ha-config.example.json` nach `GrowDiary.Web/App_Data/ha-config.json` kopieren
2. `url` auf deine Home-Assistant-Instanz setzen
3. `token` mit einem Long-Lived Access Token befuellen
4. Sensoren den Zelten zuordnen
5. App neu starten

Alternativ kann die Konfiguration in der React-App unter `Einstellungen` gepflegt werden.

## Tests

```bash
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj
```

## Hinweise

- Die aktive UI ist React; das Backend liefert JSON unter `/api/*`.
- Build-Artefakte der SPA liegen in `GrowDiary.Web/wwwroot`.
- Es gibt aktuell keine Repo-Skripte unter `scripts/`; alte Verweise darauf sind entfernt.
