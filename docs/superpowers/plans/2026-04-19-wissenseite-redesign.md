# Wissenseite Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die Wissenseite optisch aufwerten — breitere Sidebar mit Hersteller-Subtitle, grüner Left-Border-Akzent für aktives Item, kompaktere Stage-Karten, Hero mit grünem Akzent.

**Architecture:** Reine CSS + Razor-Änderungen, kein neuer Service, keine neuen Dateien. Task 1 macht alle CSS-Anpassungen, Task 2 passt das Razor-Markup an.

**Tech Stack:** Blazor Server (.NET 8), CSS custom properties (`--green`, `--surface`, `--border`, etc.). Kein Test-Framework konfiguriert.

---

### Task 1: CSS — Layout, Sidebar-Items, Stage-Karten, Hero

**Files:**
- Modify: `GrowDiary.Web/wwwroot/css/site.css:404` (wissen-layout Breite)
- Modify: `GrowDiary.Web/wwwroot/css/site.css:409-412` (phase-nav-item Regeln)
- Modify: `GrowDiary.Web/wwwroot/css/site.css:419` (wissen-stages min-width)
- Modify: `GrowDiary.Web/wwwroot/css/site.css:422` (wissen-stage-dose Font)
- Modify: `GrowDiary.Web/wwwroot/css/site.css` (wissen-hero neue Klasse nach wissen-summary)

- [ ] **Step 1: `.wissen-layout` Breite von 220px auf 260px**

Aktuelle Zeile 404:
```css
.wissen-layout  { display: grid; grid-template-columns: 220px 1fr; gap: 24px; align-items: start; }
```

Ersetzen durch:
```css
.wissen-layout  { display: grid; grid-template-columns: 260px 1fr; gap: 24px; align-items: start; }
```

- [ ] **Step 2: `.phase-nav-item` Regeln ersetzen (Zeilen 409–412)**

Aktuelle Zeilen 409–412:
```css
.phase-nav-item { padding: 12px 14px; font-size: 13px; font-weight: 500; color: var(--muted); cursor: pointer; border-bottom: 1px solid var(--border); transition: background 0.1s, color 0.1s; display: flex; justify-content: space-between; align-items: center; gap: 8px; }
.phase-nav-item:last-child { border-bottom: none; }
.phase-nav-item:hover  { background: var(--surface2); color: var(--text); }
.phase-nav-item.active { background: var(--surface3); color: var(--text); border-left: 2px solid var(--green); }
```

Ersetzen durch:
```css
.phase-nav-item { display: flex; flex-direction: column; gap: 1px; padding: 10px 14px; cursor: pointer; border-left: 2px solid transparent; border-bottom: 1px solid var(--border); transition: background 0.1s, border-color 0.1s; }
.phase-nav-item:last-child { border-bottom: none; }
.phase-nav-item:hover { background: var(--surface2); }
.phase-nav-item.active { border-left-color: var(--green); background: var(--surface2); }
.phase-nav-item-name { font-size: 13px; font-weight: 500; color: var(--muted); }
.phase-nav-item.active .phase-nav-item-name { color: var(--text); }
.phase-nav-item-sub { font-size: 11px; color: var(--faint); }
```

- [ ] **Step 3: `.wissen-stages` min-width von 150px auf 120px, gap von 10px auf 8px**

Aktuelle Zeile 419:
```css
.wissen-stages { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; margin-bottom: 4px; }
```

Ersetzen durch:
```css
.wissen-stages { display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 8px; margin-bottom: 4px; }
```

- [ ] **Step 4: `.wissen-stage-dose` Font-Size und Weight reduzieren**

Aktuelle Zeile 422:
```css
.wissen-stage-dose   { font-family: var(--mono); font-size: 20px; font-weight: 500; color: var(--text); line-height: 1; }
```

Ersetzen durch:
```css
.wissen-stage-dose   { font-family: var(--mono); font-size: 14px; font-weight: 300; color: var(--text); line-height: 1.2; }
```

- [ ] **Step 5: `.wissen-hero` neue Klasse nach `.wissen-summary` einfügen**

Aktuelle Zeile 416:
```css
.wissen-summary { font-size: 14px; color: var(--muted); line-height: 1.65; margin-top: 14px; max-width: 72ch; }
```

Nach dieser Zeile einfügen:
```css
.wissen-hero { background: var(--surface); border: 1px solid var(--border); border-left: 3px solid var(--green); border-radius: var(--radius); padding: 20px 22px; margin-bottom: 18px; }
```

- [ ] **Step 6: Build prüfen**

```bash
dotnet build "GrowDiary.slnx" -m:1 -v:minimal
```
(Ausführen aus `D:\Grow Operation System new`)

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 7: Commit**

```bash
git add GrowDiary.Web/wwwroot/css/site.css
git commit -m "style: Wissenseite CSS — Sidebar 260px, Phase-Nav-Item Subtitle, Stage-Karten kompakter, wissen-hero"
```

---

### Task 2: Razor — Nav-Items Markup + Hero-Klasse

**Files:**
- Modify: `GrowDiary.Web/Components/Pages/Wissen.razor`

Hintergrund: Die Nav-Items nutzen aktuell `<span>` für Name und `<span class="phase-week-badge">` für Manufacturer (inline nebeneinander). Das wird auf zwei gestackte Spans mit den neuen CSS-Klassen aus Task 1 umgestellt. Die Hero-Blöcke nutzen `class="grow-hero" style="margin-bottom:20px"` — das wird auf `class="wissen-hero"` umgestellt (margin ist jetzt in der CSS-Klasse).

- [ ] **Step 1: Nav-Item für Nährstoffprogramme anpassen**

Aktuelles Markup (in `Wissen.razor` im `@if (_programs.Any())` Block):
```razor
<div class="phase-nav-item @(_selectedKey == p.Key ? "active" : "")"
     @onclick="() => Select(p.Key)">
    <span>@p.Name</span>
    <span class="phase-week-badge">@p.Manufacturer</span>
</div>
```

Ersetzen durch:
```razor
<div class="phase-nav-item @(_selectedKey == p.Key ? "active" : "")"
     @onclick="() => Select(p.Key)">
    <span class="phase-nav-item-name">@p.Name</span>
    <span class="phase-nav-item-sub">@p.Manufacturer</span>
</div>
```

- [ ] **Step 2: Nav-Item für Playbooks anpassen**

Aktuelles Markup (im `@if (_playbooks.Any())` Block):
```razor
<div class="phase-nav-item @(_selectedKey == pb.Key ? "active" : "")"
     @onclick="() => SelectPlaybook(pb.Key)">
    @pb.Title
</div>
```

Ersetzen durch:
```razor
<div class="phase-nav-item @(_selectedKey == pb.Key ? "active" : "")"
     @onclick="() => SelectPlaybook(pb.Key)">
    <span class="phase-nav-item-name">@pb.Title</span>
</div>
```

- [ ] **Step 3: Hero-Block für Nährstoffprogramm auf `wissen-hero` umstellen**

Aktuelles Markup (im `@if (_selectedProgram is not null)` Block):
```razor
<div class="grow-hero" style="margin-bottom:20px">
    <div class="grow-hero-title">@_selectedProgram.Name</div>
    <div class="grow-hero-sub">@_selectedProgram.Manufacturer · @_selectedProgram.Category</div>
```

Ersetzen durch:
```razor
<div class="wissen-hero">
    <div class="grow-hero-title">@_selectedProgram.Name</div>
    <div class="grow-hero-sub">@_selectedProgram.Manufacturer · @_selectedProgram.Category</div>
```

- [ ] **Step 4: Hero-Block für Playbook auf `wissen-hero` umstellen**

Aktuelles Markup (im `else if (_selectedPlaybook is not null)` Block):
```razor
<div class="grow-hero" style="margin-bottom:20px">
    <div class="grow-hero-title">@_selectedPlaybook.Title</div>
```

Ersetzen durch:
```razor
<div class="wissen-hero">
    <div class="grow-hero-title">@_selectedPlaybook.Title</div>
```

- [ ] **Step 5: Build prüfen**

```bash
dotnet build "GrowDiary.slnx" -m:1 -v:minimal
```
(Ausführen aus `D:\Grow Operation System new`)

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 6: Commit**

```bash
git add GrowDiary.Web/Components/Pages/Wissen.razor
git commit -m "style: Wissenseite Razor — Phase-Nav-Item Subtitle-Spans, wissen-hero Klasse"
```
