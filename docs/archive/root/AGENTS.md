# Grow OS – Codex Agent Instructions

## Projektpfad

Arbeite ausschließlich in dieser lokalen Arbeitskopie:

`D:\Grow Operation System new`

Das lokale Projekt ist die Wahrheit. Nicht vom GitHub-Remote ausgehen, wenn lokale Dateien abweichen.

## Pflichtlektüre vor jeder Aufgabe

Lies vor jeder Aufgabe zuerst:

1. `GrowDiary.Web/CLAUDE.md`
2. `GrowDiary.Web/README.md`

Wenn Code und `CLAUDE.md` widersprechen, gewinnt der Code. Dokumentiere den Widerspruch im Abschlussbericht.

## Arbeitsregeln

- Arbeite nur den explizit genannten Ticket-Scope ab.
- Keine Refactorings außerhalb des Scopes.
- Keine Architekturentscheidungen ohne Rückfrage.
- Keine Dateien ändern, die nicht direkt zum Ticket gehören.
- Bei großen Änderungen mit mehr als 20 betroffenen Dateien stoppen und Rückfrage stellen.
- Keine Backend-/API-Änderungen bei reinen UI-Tickets.
- Keine Datenbank-/Schema-Änderungen ohne ausdrückliche Ticket-Anweisung.
- Keine destruktiven Änderungen an `App_Data`.
- Keine Tests deaktivieren.
- Keine Build-Artefakte unnötig anfassen.
- Wenn OpenAI-, Codex- oder API-Dokumentation benötigt wird, nutze den OpenAI Developer Docs MCP.

## Standard-Kommandos

Backend build:

`dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal`

Backend tests:

`dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal`

Frontend build:

`cd GrowDiary.React && npm run build`

Falls PowerShell `npm.ps1` blockiert, verwende:

`& 'C:\Program Files\nodejs\npm.cmd' run build`

## Abschlussbericht

Nach jedem Ticket immer berichten:

- Geänderte Dateien
- Was wurde konkret geändert?
- Frontend-Build-Status, falls Frontend betroffen
- Backend-Build-Status
- Test-Status
- Auffälligkeiten / offene Punkte