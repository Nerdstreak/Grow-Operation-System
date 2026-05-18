# GitHub- und Codex-Workflow

## Grundregeln

- Das lokale Repository ist die Wahrheit.
- Nicht direkt auf `main` arbeiten.
- Vor Aenderungen Branch anlegen oder die vom Ticket vorgegebene Branch nutzen.
- Nur den explizit genannten Ticket-Scope bearbeiten.
- Keine Architekturentscheidungen ohne Rueckfrage.
- Keine breiten Refactors ohne eigene Branch und klare Freigabe.
- Keine UI-Aenderungen ohne ausdrueckliche UI-Aufgabe.
- Keine Tests deaktivieren.
- Keine destruktiven Aenderungen an `App_Data`.

## Prompt-Regeln

Ein guter Codex-Prompt nennt:

- Zielbranch
- erlaubte Dateien oder Bereiche
- verbotene Bereiche
- erwartete Build-/Testbefehle
- Abschlussbericht
- ob Commit/Push gewuenscht ist

Bei Backend-Tickets sollten Controller, Services, DTOs, Models, Repositories und Tests explizit ein- oder ausgeschlossen werden.

Bei Dokumentationstickets sollte klar sein, ob nur Markdown geaendert werden darf.

## Standard-Kommandos

Backend Build:

```powershell
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Backend Tests:

```powershell
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

Frontend Build:

```powershell
cd GrowDiary.React
npm run build
```

Falls PowerShell `npm.ps1` blockiert:

```powershell
& 'C:\Program Files\nodejs\npm.cmd' run build
```

## Dokumentationspflicht

Nach jedem Ticket berichten:

- geaenderte Dateien
- konkrete Aenderungen
- Backend-Build-Status
- Test-Status
- Frontend-Build-Status, falls Frontend betroffen ist
- offene Punkte oder Abweichungen

## Umgang mit Unsicherheit

- Wenn Code und Dokumentation widersprechen, gewinnt der Code.
- Unsichere alte Inhalte nicht loeschen; lieber nach `docs/archive/` verschieben.
- Bei mehr als 20 erwarteten betroffenen Dateien Rueckfrage stellen, sofern das Ticket nicht genau diesen Umfang freigibt.
- Wenn ein Scope versehentlich Code-Dateien beruehrt, diese Aenderungen rueckgaengig machen.

## Codex bei Refactors

Der Repository-Refactor wurde bewusst schrittweise umgesetzt. Fuer aehnliche Arbeiten gilt:

- Facade-Verhalten erhalten, wenn Controller/Services unveraendert bleiben sollen.
- Keine neuen Interfaces einfuehren, wenn das Ticket es nicht verlangt.
- Keine Transactions ueber Repository-Grenzen aufbrechen.
- Build gruen halten ist wichtiger als maximale Kuerzung.

## OpenAI- und API-Dokumentation

Wenn aktuelle OpenAI-, Codex- oder API-Dokumentation benoetigt wird, sollen offizielle OpenAI Developer Docs verwendet werden.
