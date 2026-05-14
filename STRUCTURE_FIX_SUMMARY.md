# STRUCTURE-FIX-1

Ziel dieses Pakets:

- frischer Start ohne automatisch gesetzte Standard-Zelte
- Zeltverwaltung primär unter `/zelte`
- Zelte können angelegt, bearbeitet und entfernt werden
- Entfernen löscht nur unbenutzte Zelte hart; Zelte mit Abhängigkeiten werden archiviert
- Settings ist keine Zelt-/Setup-Verwaltung mehr
- Home-Assistant-Token wird in Settings standardmäßig maskiert angezeigt
- lokale React-Fonts bleiben über Fontsource erhalten

Geänderte Kernbereiche:

- `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs`
- `GrowDiary.Web/Infrastructure/GrowRepository.cs`
- `GrowDiary.Web/Models/Tent.cs`
- `GrowDiary.Web/Models/Enums.cs`
- `GrowDiary.Web/Api/Contracts/TentDto.cs`
- `GrowDiary.Web/Api/Contracts/UpdateTentRequest.cs`
- `GrowDiary.Web/Api/Controllers/SettingsApiController.cs`
- `GrowDiary.Web/Api/Mapping/RequestMapping.cs`
- `GrowDiary.Web/Api/Mapping/SettingsMapping.cs`
- `GrowDiary.React/src/pages/TentsPage.tsx`
- `GrowDiary.React/src/pages/SettingsPage.tsx`
- `GrowDiary.React/src/types.ts`
- `GrowDiary.React/src/index.css`

Wichtig:

- Bestehende lokale Datenbankdaten bleiben bestehen. Wenn in deiner `grow-diary.db` bereits Hauptzelt/Anzuchtzelt liegt, werden diese nicht automatisch gelöscht.
- Für einen echten 0-Zelte-Test muss lokal mit leerer DB getestet werden.
- `GrowDiary.Web/wwwroot/index.html` und `wwwroot/assets` sind nicht enthalten, weil sie Build-Artefakte sind.
