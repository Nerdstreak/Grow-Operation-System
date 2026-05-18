# Grow OS FRONTEND V1 Rebuild

Dies ist bewusst **kein Patch-Paket**. Es enthält fertige Source-Dateien unter `source/` und ein Copy-Skript.

## Ziel

- Mobile-first App-Shell
- Core-Navigation: Live, Addback, Zelte, Hydro
- Zelte und Hydro sauber getrennt
- Live-Dashboard als echte Grow-Zentrale
- Addback als Workflow statt nur Rechner
- Grow starten mit Zelt + Hydro + Nährstoffprogramm
- Home Assistant Mapping als fokussierter Flow
- Settings ohne operative Müllhalde

## Dateien

Diese Dateien werden ersetzt bzw. neu angelegt:

```text
GrowDiary.React/src/App.tsx
GrowDiary.React/src/index.css
GrowDiary.React/src/components/v1.tsx
GrowDiary.React/src/pages/LiveDashboardPage.tsx
GrowDiary.React/src/pages/TentsPage.tsx
GrowDiary.React/src/pages/HydroPage.tsx
GrowDiary.React/src/pages/GrowSetupPage.tsx
GrowDiary.React/src/pages/HomeAssistantPage.tsx
GrowDiary.React/src/pages/AddbackHubPage.tsx
GrowDiary.React/src/pages/AddbackPage.tsx
GrowDiary.React/src/pages/MobileActionPage.tsx
GrowDiary.React/src/pages/SettingsPage.tsx
```

## Anwenden

Im entpackten Ordner:

```powershell
powershell -ExecutionPolicy Bypass -File .\Apply-FRONTEND-V1-REBUILD.ps1
```

Oder mit RepoRoot:

```powershell
powershell -ExecutionPolicy Bypass -File .\Apply-FRONTEND-V1-REBUILD.ps1 -RepoRoot "D:\Grow Operation System new"
```

## Prüfen

```powershell
cd "D:\Grow Operation System new\GrowDiary.React"
npm run build
npm run audit:visual

cd "D:\Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```

Falls `npm run build` `GrowDiary.Web/wwwroot` ändert:

```powershell
cd "D:\Grow Operation System new"
git checkout -- GrowDiary.Web/wwwroot
```

## Commit

```powershell
git add GrowDiary.React/src/App.tsx `
        GrowDiary.React/src/index.css `
        GrowDiary.React/src/components/v1.tsx `
        GrowDiary.React/src/pages/LiveDashboardPage.tsx `
        GrowDiary.React/src/pages/TentsPage.tsx `
        GrowDiary.React/src/pages/HydroPage.tsx `
        GrowDiary.React/src/pages/GrowSetupPage.tsx `
        GrowDiary.React/src/pages/HomeAssistantPage.tsx `
        GrowDiary.React/src/pages/AddbackHubPage.tsx `
        GrowDiary.React/src/pages/AddbackPage.tsx `
        GrowDiary.React/src/pages/MobileActionPage.tsx `
        GrowDiary.React/src/pages/SettingsPage.tsx

git commit -m "Rebuild frontend for V1 mobile workflow"
git push origin main
```
