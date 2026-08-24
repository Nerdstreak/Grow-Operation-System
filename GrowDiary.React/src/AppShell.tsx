/* src/AppShell.tsx
   Aus App.tsx herausgezogen: Shell + Navigation + Kontextleiste.
   App.tsx enthaelt danach nur noch <Routes>. */

import { useEffect, useRef, useState, type ReactNode } from 'react'
import { NavLink, useLocation, useNavigationType } from 'react-router-dom'
import { AppSearch } from './components/AppSearch'
import { isNavLeafActive, mobilePrimaryNav, navGroups, searchablePages } from './navigation'
import { useTheme } from './useTheme'
import { useHomeAssistantHealth } from './useHomeAssistantHealth'
import { useDemoMode } from './useDemoMode'

type Props = {
  children: ReactNode
  /**
   * Zähler für die Badges in der Navigation — über alle laufenden Grows.
   *
   * Hier stand einmal eine globale Zelt/Grow-Auswahl. Sie steuerte allerdings
   * nur diese beiden Zahlen und keine einzige Seite: man stellte oben etwas ein
   * und unten passierte nichts. Jetzt wählt jede Seite selbst, und die Zähler
   * zählen das, was ihre Ziele auch zeigen.
   */
  counts?: { addbackDue?: boolean; openTasks?: number }
}

export function AppShell({ children, counts }: Props) {
  const health = useHomeAssistantHealth()
  const demoMode = useDemoMode()
  const location = useLocation()
  const { theme, toggle } = useTheme()
  const [moreOpen, setMoreOpen] = useState(false)

  // Beim Seitenwechsel nach oben.
  //
  // Ohne das oeffnet die neue Seite dort, wo die alte aufgehoert hat: wer eine
  // lange Seite bis ans Ende gelesen hat und dann das Menue benutzt, landet
  // mitten im neuen Inhalt, dessen Ueberschrift weit ueber dem Sichtbaren
  // liegt. Gemessen: von /wissen (Stand 2531) auf die Startseite -> Stand
  // 2004, die Ueberschrift 1769 px oberhalb. Das ist die halbe Antwort auf
  // „ich muss suchen, wo welches Feature ist".
  //
  // NUR bei PUSH und REPLACE. Beim Zurueckgehen (POP) soll der Browser seinen
  // gemerkten Stand behalten — sonst verliert man beim Zurueck genau die
  // Stelle, an der man war.
  const wechselArt = useNavigationType()
  useEffect(() => {
    if (wechselArt === 'POP') return
    window.scrollTo({ top: 0, left: 0 })
  }, [location.pathname, wechselArt])

  /**
   * Der aktive Menuepunkt muss sichtbar sein.
   *
   * Die Seitenleiste ist 1126 px hoch. Auf einem Notebook mit 1366x768 sind
   * damit die letzten neun Punkte unter der Fensterkante — steht man auf einem
   * davon, sieht man nirgends, wo man ist. Die Leiste kann scrollen
   * (`overflow-y: auto`), sie tut es nur von allein nicht.
   *
   * `block: 'nearest'` scrollt nur, wenn noetig, und ruckt den Punkt nicht in
   * die Mitte: liegt er ohnehin im Blick, passiert gar nichts.
   */
  const navRef = useRef<HTMLElement>(null)
  useEffect(() => {
    const aktiv = navRef.current?.querySelector('.v1-nav-item.active')
    aktiv?.scrollIntoView({ block: 'nearest' })
  }, [location.pathname])

  return (
    <div className="v1-app-shell rc2-shell" data-audit="mobile-shell">
      {/* Der Sprung ueber die Navigation. Wer mit der Tastatur arbeitet, musste
          auf JEDER Seite erst durch Suchfeld und 26 Menuepunkte tabben, bevor
          der Inhalt drankam. Der Link ist unsichtbar, bis er den Fokus hat. */}
      <a href="#inhalt" className="gos-skip-link">Zum Inhalt springen</a>

      <aside className="v1-desktop-nav" aria-label="Navigation" ref={navRef}>
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

      {/*
        Die Hauptnavigation sitzt oben, nicht unten.

        Unten funktioniert sie im Home-Assistant-Ingress nicht: dort steckt
        Grow OS in einem iframe, das hoeher ist als das, was auf dem Schirm
        Platz hat. `position: fixed; bottom: 0` klebt an der Unterkante DIESES
        iframes — und die liegt unterhalb des sichtbaren Bereichs. Auf dem
        Telefon ragte nur noch die Oberkante der aktiven Kachel ins Bild.

        Von innen laesst sich nicht messen, wie viel abgeschnitten ist: das
        iframe kennt seine eigene Hoehe, nicht die des Fensters darum. Oben gibt
        es das Problem nicht — die Oberkante liegt immer im Bild.
      */}
      <nav className="v1-mobile-nav" aria-label="Hauptnavigation">
        {mobilePrimaryNav.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            className={isNavLeafActive(item, location.pathname) ? 'v1-mobile-nav-item active' : 'v1-mobile-nav-item'}
          >
            {item.label}
          </NavLink>
        ))}
      </nav>

      {moreOpen && (
        <div className="v1-mobile-more-panel" data-audit="mobile-more-menu">
          <AppSearch pages={searchablePages} onNavigate={() => setMoreOpen(false)} />
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

      <main className="v1-route-frame" id="inhalt" tabIndex={-1}>

        {/* Testdaten: muss über allem stehen und darf nicht wegzuklicken sein.
            Erfundene Messwerte, die jemand für echte hält, wären nicht bloß
            falsch — an ihnen hängen Alarme und die Dosierung. */}
        {demoMode && (
          <div className="gos-demo" role="status" data-audit="demo-mode">
            <strong>Testdaten</strong>
            <span>
              {/* „Es wird nichts geschaltet" stimmte nicht mehr, seit der
                  Testbetrieb Schaltbefehle festhaelt (Demoschaltbrett). Wer
                  eine Stufe stellt, sieht sie hinterher stehen — nur eben im
                  Testbestand und nicht an einem Geraet. Der Unterschied
                  gehoert hierhin, sonst glaubt jemand, er habe wirklich
                  geschaltet. */}
              Alle Messwerte auf diesem Server sind erfunden. Es geht nichts an ein echtes
              Gerät — was du hier stellst, merkt sich nur der Testbestand.
            </span>
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

    </div>
  )
}
