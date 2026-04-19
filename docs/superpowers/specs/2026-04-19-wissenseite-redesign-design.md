# Design: Wissenseite Redesign

**Datum:** 2026-04-19  
**Status:** Genehmigt

---

## Ziel

Die Wissenseite (`/wissen`) sieht aktuell unfertig aus. Hauptprobleme:
- Sidebar zu schmal (220px), Hersteller-Info fehlt im Nav-Item
- Stage-Karten: `font-size: 20px` für `.wissen-stage-dose` ist zu groß für `minmax(150px)` — Text läuft über
- Kein visueller Akzent für aktives Nav-Item
- Hero-Block hat kein klares visuelles Gewicht

Gewähltes Layout: **Variante C** — Sidebar 260px mit Hersteller-Subtitle + grüner Left-Border-Akzent, kompaktere Stage-Karten.

---

## Änderungen

### CSS — `GrowDiary.Web/wwwroot/css/site.css`

**Wissen-Layout Breite:**
```css
/* vorher */
.wissen-layout { display: grid; grid-template-columns: 220px 1fr; gap: 24px; align-items: start; }

/* nachher */
.wissen-layout { display: grid; grid-template-columns: 260px 1fr; gap: 24px; align-items: start; }
```

**Phase-Nav-Item: Hersteller-Subtitle + aktiver Akzent:**

Die bestehenden `.phase-nav-item`-Regeln werden ersetzt (nicht nur ergänzt). `.phase-week-badge` wird nicht mehr gebraucht — der Selektor kann stehen bleiben (schadet nicht), wird aber im Razor nicht mehr verwendet.

Neue CSS-Regeln für `.phase-nav-item` — aktives Item bekommt grünen Left-Border und hellere Textfarbe:
```css
.phase-nav-item { display: flex; flex-direction: column; gap: 1px; padding: 10px 14px; cursor: pointer; border-left: 2px solid transparent; transition: background 0.1s, border-color 0.1s; border-bottom: 1px solid var(--border); }
.phase-nav-item:last-child { border-bottom: none; }
.phase-nav-item:hover { background: var(--surface2); }
.phase-nav-item.active { border-left-color: var(--green); background: var(--surface2); }
.phase-nav-item-name { font-size: 13px; font-weight: 500; color: var(--muted); }
.phase-nav-item.active .phase-nav-item-name { color: var(--text); }
.phase-nav-item-sub { font-size: 11px; color: var(--faint); }
```

**Stage-Karten kompakter:**
```css
/* vorher */
.wissen-stages { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; margin-bottom: 4px; }
.wissen-stage-dose { font-family: var(--mono); font-size: 20px; font-weight: 500; color: var(--text); line-height: 1; }

/* nachher */
.wissen-stages { display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 8px; margin-bottom: 4px; }
.wissen-stage-dose { font-family: var(--mono); font-size: 14px; font-weight: 300; color: var(--text); line-height: 1.2; }
```

**Hero-Card mit grünem Left-Akzent** (neue Klasse):
```css
.wissen-hero { background: var(--surface); border: 1px solid var(--border); border-left: 3px solid var(--green); border-radius: var(--radius); padding: 20px 22px; margin-bottom: 18px; }
```

---

### Razor — `GrowDiary.Web/Components/Pages/Wissen.razor`

**Sidebar-Items: `phase-nav-item` Markup anpassen**

Aktuelle Nav-Items nutzen ein `<span>` für Name und ein `<span class="phase-week-badge">` für Manufacturer. Das wird ersetzt durch zwei gestackte Spans mit den neuen CSS-Klassen.

Für Nährstoffprogramme:
```razor
<div class="phase-nav-item @(_selectedKey == p.Key ? "active" : "")"
     @onclick="() => Select(p.Key)">
    <span class="phase-nav-item-name">@p.Name</span>
    <span class="phase-nav-item-sub">@p.Manufacturer</span>
</div>
```

Für Playbooks (haben keinen Manufacturer — `Title` als Name, kein Subtitle):
```razor
<div class="phase-nav-item @(_selectedKey == pb.Key ? "active" : "")"
     @onclick="() => SelectPlaybook(pb.Key)">
    <span class="phase-nav-item-name">@pb.Title</span>
</div>
```

**Hero-Block: Klasse wechseln**

```razor
/* vorher */
<div class="grow-hero" style="margin-bottom:20px">

/* nachher */
<div class="wissen-hero">
```

(Gilt für beide: `_selectedProgram` und `_selectedPlaybook` Hero-Blöcke — `style="margin-bottom:20px"` entfernen da nun in `.wissen-hero` integriert)

---

## Betroffene Dateien

| Datei | Änderung |
|-------|----------|
| `GrowDiary.Web/wwwroot/css/site.css` | `.wissen-layout` Breite, `.phase-nav-item` neue Regeln, `.wissen-stage-dose` kleiner, `.wissen-hero` neu |
| `GrowDiary.Web/Components/Pages/Wissen.razor` | Nav-Items Markup, Hero-Klasse |

---

## Nicht im Scope

- Suchfunktion innerhalb der Wissenseite
- Responsive / Mobile-Optimierung der Sidebar
- Neuer Inhalt im Knowledge-Service
