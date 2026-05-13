# Contributing

Beiträge sind willkommen. Grow Operation System ist ein aktives selfhosted Community-Projekt mit Fokus auf lokalen Betrieb, Home Assistant Integration und datenschutzfreundliche Nutzung.

## 1. Willkommen

Gute Beiträge sind zum Beispiel:

- reproduzierbare Bugreports
- kleine, klar abgegrenzte Fixes
- Dokumentationsverbesserungen
- Tests für bestehendes Verhalten
- UI-Verbesserungen, die mobile Nutzung und Lesbarkeit verbessern
- fachlich begründete Knowledge- oder Grow-Workflow-Vorschläge

## 2. Entwicklungssetup

Die vollständige Startanleitung steht in [INSTALL.md](INSTALL.md).

Kurzbefehle:

```bash
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal

cd GrowDiary.React
npm install
npm run dev
npm run build
```

## 3. Projektstruktur kurz

- `GrowDiary.Web`: ASP.NET Core Backend, JSON-API, SQLite/ADO.NET, Services, Infrastructure und Models.
- `GrowDiary.React`: React/Vite/TypeScript Frontend.
- `GrowDiary.Web.Tests`: Backend-Tests.
- `GrowDiary.Web/App_Data`: lokale Runtime-Daten, nicht committen.
- `GrowDiary.Web/wwwroot/knowledge-defaults`: ausgelieferte Default-Knowledge-Daten.

## 4. Arbeitsregeln

- Kleine Pull Requests bevorzugen.
- Keine großen Refactorings ohne vorheriges Issue oder Abstimmung.
- Keine Secrets oder `App_Data` committen.
- Keine generierten `wwwroot`-Artefakte committen, außer sie sind bewusst Teil der Änderung.
- Keine Knowledge-JSONs ohne fachliche Begründung ändern.
- Tests vor Pull Request ausführen.
- Build vor Pull Request ausführen.
- Kein ungeprüftes Löschen von Daten, Migrationen oder lokalen Runtime-Dateien.

## 5. Coding Guidelines

Backend:

- bestehende ADO.NET/SQLite-Repository-Struktur respektieren.
- Schemaänderungen nur additiv und kontrolliert.
- keine destruktiven Datenbankänderungen ohne explizite Planung.
- Validierungen testen.
- API-Verträge nicht nebenbei ändern.

Frontend:

- bestehende Pages, Types und API-Helfer nutzen.
- responsive Layouts prüfen.
- keine unnötigen Textwüsten einbauen.
- Buttons und Touch-Ziele ausreichend groß halten.
- bestehende Design- und CSS-Struktur respektieren.

Doku:

- Deutsch ist aktuell die Hauptsprache.
- Security-Hinweise klar und vorsichtig formulieren.
- Keine Features versprechen, die nicht implementiert sind.

## 6. Tests

Vor einem Pull Request mindestens:

```bash
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

Wenn Frontend betroffen ist:

```bash
cd GrowDiary.React
npm run build
```

Keine `App_Data`-Testdaten, lokalen Datenbanken, Uploads, Snapshots oder Secrets committen.

GitHub Actions führt bei Push und Pull Request Backend-Build, Backend-Tests, Frontend-Install und Frontend-Build aus.

## 7. Issues und Feature Requests

Bugreport:

- kurze Beschreibung
- Schritte zum Reproduzieren
- erwartetes Verhalten
- tatsächliches Verhalten
- relevante Logs ohne Secrets
- Browser/OS, falls UI betroffen ist

Feature Request:

- welches Problem soll gelöst werden?
- welches Ziel hat die Änderung?
- mögliche Lösung oder Workflow-Skizze
- betroffene Seiten oder APIs, falls bekannt

Security:

- keine Tokens, Datenbanken, privaten Grow-Fotos oder vollständigen Konfigurationen öffentlich posten.
- Falls ein Secret betroffen ist, nicht öffentlich mit sensiblen Details melden.
- Weitere Hinweise stehen in [SECURITY.md](SECURITY.md).

## 8. Security

- Keine Home Assistant Tokens posten.
- Keine privaten Grow-Fotos oder Snapshots posten, wenn das nicht bewusst gewollt ist.
- Keine lokalen `ha-config.json` Dateien teilen.
- Keine SQLite-Datenbanken oder WAL/SHM-Dateien anhängen.
- Remote-Zugriff und Admin-Freigaben vorsichtig behandeln.

## 9. Pull Requests

Ein guter Pull Request enthält:

- klare Beschreibung der Änderung.
- betroffene Bereiche.
- ausgeführte Tests und Builds.
- Screenshots bei UI-Änderungen.
- Hinweis, ob Datenbank/Schema betroffen ist.
- Hinweis, ob `App_Data`, Knowledge-Daten oder Build-Artefakte betroffen sind.

Wenn du unsicher bist, erst ein Issue mit Problem und Ziel öffnen.
