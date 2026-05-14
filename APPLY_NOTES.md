# GrowOS FULL SOURCE REWORK

Dieses Paket ist ein vollständiger bereinigter Source-Bundle-Auszug des Projekts, nicht nur ein 2-Dateien-Patch.

Enthält:
- GrowDiary.React Source
- GrowDiary.Web Source
- GrowDiary.Web.Tests Source
- Doku/Deployment/CI-Dateien

Nicht enthalten:
- .git
- node_modules
- bin/obj
- App_Data Runtime-Daten/DB/Token/Fotos
- generierte wwwroot/assets und wwwroot/index.html
- artifacts/publish/release/docker-data

Änderungen in diesem Bundle:
- GrowSetupPage: keine Warn-/Erklärbox "Noch nicht gespeichert..." mehr.
- GrowSetupPage: Wizard-Texte deutlich reduziert, kein erklärender Textblock im Prüfen-Schritt.
- Speichern bleibt nur über den finalen Button "Grow starten" / "Grow aktualisieren".
- Font-Setup bleibt über @fontsource/inter und @fontsource/jetbrains-mono in main.tsx lokal gebündelt.

Nach dem Kopieren:
```powershell
cd "D:\Grow Operation System new\GrowDiary.React"
npm install
npm run build
cd "D:\Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
dotnet test GrowDiary.Web.Tests/GrowDiary.Web.Tests.csproj -v:minimal
```
