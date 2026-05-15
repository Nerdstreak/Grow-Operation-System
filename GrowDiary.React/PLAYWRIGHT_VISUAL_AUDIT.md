# Grow OS Playwright Visual Audit

Dieses Paket richtet ein reproduzierbares Screenshot-Audit für die wichtigsten Grow-OS-Seiten ein.
Es ist bewusst kein UI-Approval-Test, sondern ein Werkzeug, um nach Umbauten die komplette App sichtbar zu prüfen.

## Einmalig Browser installieren

```powershell
cd "D:\Grow Operation System new\GrowDiary.React"
npm run audit:install
```

## Audit mit automatisch gestarteten Servern

```powershell
cd "D:\Grow Operation System new\GrowDiary.React"
npm run audit:visual
```

Das startet über Playwright:

- Backend auf `http://127.0.0.1:5076`
- Vite auf `http://127.0.0.1:5173`

Die Screenshots landen unter:

```text
D:\Grow Operation System new\artifacts\visual-audit-current\
```

Wichtige Dateien:

```text
artifacts/visual-audit-current/visual-audit-report.md
artifacts/visual-audit-current/visual-audit-report.json
```

## Audit mit bereits laufenden Servern

Falls Backend/Vite schon laufen:

```powershell
cd "D:\Grow Operation System new\GrowDiary.React"
$env:GROW_OS_AUDIT_START_SERVERS="0"
npm run audit:visual
Remove-Item Env:\GROW_OS_AUDIT_START_SERVERS
```

## Eigener Audit-Ordner

```powershell
$env:GROW_OS_AUDIT_NAME="visual-audit-before-addback"
npm run audit:visual
Remove-Item Env:\GROW_OS_AUDIT_NAME
```

## Geprüfte Seiten

Desktop `1440x1000` und Mobile `390x844`:

- `/`
- `/addback`
- `/action`
- `/zelte`
- `/home-assistant`
- `/grows/new`
- `/settings`
- `/wissen`
- `/hardware`

## Was protokolliert wird

- Screenshot pro Seite und Viewport
- HTTP-Status
- erste Überschrift
- horizontaler Overflow
- Console-Warnungen/Errors
- fehlgeschlagene Requests

`artifacts/` ist im Root-`.gitignore` ignoriert und soll nicht committed werden.
