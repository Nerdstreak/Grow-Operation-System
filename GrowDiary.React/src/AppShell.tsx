/* src/AppShell.tsx
   Aus App.tsx herausgezogen: Shell + Navigation + Kontextleiste.
   App.tsx enthaelt danach nur noch <Routes>. */

import { useState, type ReactNode } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { AppSearch } from './components/AppSearch'
import { isNavLeafActive, mobilePrimaryNav, navGroups, searchablePages } from './navigation'
import { useTheme } from './useTheme'
import { useHomeAssistantHealth } from './useHomeAssistantHealth'

type Props = {
  children: ReactNode
  /** Kontextleiste: einmal wählen, gilt für alle grow-/zelt-bezogenen Seiten. */
  scope?: {
    tents: Array<{ id: number; name: string }>
    grows: Array<{ id: number; name: string }>
    tentId: number | null
    growId: number | null
    onTent: (id: number | null) => void
    onGrow: (id: number | null) => void
  }
  /** Zähler fuer die Badges in der Navigation. */
  counts?: { addbackDue?: boolean; openTasks?: number }
}

export function AppShell({ children, scope, counts }: Props) {
  const health = useHomeAssistantHealth()
  const location = useLocation()
  const { theme, toggle } = useTheme()
  const [moreOpen, setMoreOpen] = useState(false)

  return (
    <div className="v1-app-shell rc2-shell" data-audit="mobile-shell">
      <aside className="v1-desktop-nav" aria-label="Navigation">
        <div className="v1-brand">
          <div className="v1-brand-mark">G</div>
          <div>
            <strong>GROW OS</strong>
            <span>RDWC · Home Assistant</span>
          </div>
        </div>

        <AppSearch pages={searchablePages} />

        {navGroups.map((group) => (
          <nav key={group.id} className="v1-nav-group" aria-label={group.label}>
            <div className="v1-nav-group-head">{group.label}</div>
            {group.items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) => (isActive ? 'v1-nav-item active' : 'v1-nav-item')}
              >
                {item.label}
                {item.badge === 'warn' && counts?.addbackDue && <span className="badge warn">fällig</span>}
                {item.badge === 'count' && !!counts?.openTasks && <span className="badge">{counts.openTasks}</span>}
              </NavLink>
            ))}
          </nav>
        ))}

        <div className="v1-nav-foot">
          <NavLink to="/settings" className="v1-nav-item">EINSTELLUNGEN</NavLink>
          <button type="button" className="theme-toggle" onClick={toggle} aria-label="Theme wechseln">
            {theme === 'dark' ? 'DARK' : 'HELL'}
          </button>
        </div>
      </aside>

      <header className="v1-mobile-topbar">
        <div className="v1-brand compact">
          <div className="v1-brand-mark">G</div>
          <div><strong>GROW OS</strong></div>
        </div>
        <button type="button" className="v1-mobile-more-button" data-audit="mobile-more-button" onClick={() => setMoreOpen((open) => !open)} aria-expanded={moreOpen}>
          Mehr
        </button>
      </header>

      {moreOpen && (
        <div className="v1-mobile-more-panel" data-audit="mobile-more-menu">
          <AppSearch pages={searchablePages} />
          {navGroups.map((group) => (
            <section key={group.id}>
              <div className="v1-nav-group-head">{group.label}</div>
              <div className="v1-mobile-more-grid">
                {group.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.end}
                    className={({ isActive }) => (isActive ? 'v1-more-tile active' : 'v1-more-tile')}
                    onClick={() => setMoreOpen(false)}
                  >
                    {item.label}
                  </NavLink>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}

      <main className="v1-route-frame">
        {scope && (
          <div className="gos-contextbar" data-audit="context-bar">
            <div className="gos-scope">
              <label htmlFor="scope-tent">Zelt</label>
              <select id="scope-tent" value={scope.tentId ?? ''} onChange={(event) => scope.onTent(event.target.value ? Number(event.target.value) : null)}>
                <option value="">Alle Zelte</option>
                {scope.tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}
              </select>
            </div>
            <div className="gos-scope">
              <label htmlFor="scope-grow">Grow</label>
              <select id="scope-grow" value={scope.growId ?? ''} onChange={(event) => scope.onGrow(event.target.value ? Number(event.target.value) : null)}>
                <option value="">Kein Grow</option>
                {scope.grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
              </select>
            </div>
            <div className="spacer" />
          </div>
        )}

        {/* Eine Meldung für die ganze App, nicht eine pro Karte. Nur wenn Home
            Assistant eingerichtet ist und gerade nicht antwortet — wer es gar
            nicht nutzt, braucht die Zeile nie zu sehen. */}
        {health?.configured && !health.reachable && (
          <div className="gos-offline" role="status" data-audit="ha-offline">
            <strong>Home Assistant antwortet nicht.</strong>
            <span>
              Die angezeigten Messwerte sind der letzte bekannte Stand. Messen, Addback und alles
              von Hand Eingetragene funktionieren weiter.
            </span>
          </div>
        )}

        {children}
      </main>

      <nav className="v1-bottom-nav" aria-label="Hauptnavigation">
        {mobilePrimaryNav.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            className={isNavLeafActive(item, location.pathname) ? 'v1-bottom-item active' : 'v1-bottom-item'}
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
