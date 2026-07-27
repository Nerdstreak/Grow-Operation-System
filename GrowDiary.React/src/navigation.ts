/* src/navigation.ts
   Vorher: 7 Gruppen, 23 Einträge. Jetzt: 4 Gruppen, 14 Ziele.
   Zusammengelegt:
     Automatik + Grenzwerte + Benachrichtigungen  -> /regeln (Tabs)
     Sorten + Pheno-Hunt                          -> /sorten (Tabs)
     Ernte + Archiv                               -> /archiv (Tabs)
     Sensoren + Wartung + Kalibrierung            -> /sensoren (eine Tabelle)
   Alte Routen bleiben als Redirect erhalten (siehe legacyRedirects). */

export type NavLeaf = {
  to: string
  label: string
  end: boolean
  /** 'warn' faerbt das Badge (z. B. fällige Aufgabe) */
  badge?: 'count' | 'warn'
  keywords?: string
}

export type NavGroup = {
  id: 'now' | 'grow' | 'plant' | 'library'
  label: string
  items: NavLeaf[]
}

export const navGroups: NavGroup[] = [
  {
    id: 'now',
    label: 'Jetzt',
    items: [
      { to: '/', label: 'Live', end: true, keywords: 'dashboard übersicht start sensoren cockpit' },
      { to: '/messung', label: 'Messen', end: true, keywords: 'werte eintragen ph ec do orp foto snapshot' },
      { to: '/addback', label: 'Addback', end: true, badge: 'warn', keywords: 'nachfüllen dünger dosieren reservoir' },
      { to: '/aufgaben', label: 'Aufgaben', end: true, badge: 'count', keywords: 'todo risiken checkliste wasserwechsel wartung' },
    ],
  },
  {
    id: 'grow',
    label: 'Grow',
    items: [
      { to: '/grows', label: 'Grows', end: false, keywords: 'lauf run pflanzen anbau' },
      { to: '/diagnose', label: 'Diagnose', end: true, keywords: 'problem mangel abweichung risiko krankheit sop' },
      { to: '/journal', label: 'Journal & Fotos', end: true, keywords: 'tagebuch notizen bilder verlauf' },
      { to: '/sorten', label: 'Sorten & Pheno', end: true, keywords: 'strain genetik züchter keeper selektion' },
      { to: '/archiv', label: 'Ernte & Archiv', end: true, keywords: 'harvest ertrag abgeschlossen vergleich' },
    ],
  },
  {
    id: 'plant',
    label: 'Anlage',
    items: [
      { to: '/zelte', label: 'Zelte & Räume', end: false, keywords: 'tent kamera lichtzyklus klima abluft' },
      { to: '/hydro', label: 'Hydro-Systeme', end: false, keywords: 'rdwc dwc reservoir tank pumpe sites layout' },
      { to: '/sensoren', label: 'Sensoren & Wartung', end: true, keywords: 'hardware geräte kalibrierung inventar' },
      { to: '/dosierung', label: 'Dosierung', end: false, keywords: 'pumpe peristaltik ph minus plus säure nährstoff dosieren kalibrieren' },
      { to: '/regeln', label: 'Regeln & Automatik', end: true, keywords: 'grenzwerte schwellen alarm push zeitplan automation ki assistent' },
      { to: '/home-assistant', label: 'Home Assistant', end: true, keywords: 'ha entitäten verbindung integration mapping kamera' },
    ],
  },
  {
    id: 'library',
    label: 'Wissen',
    items: [
      // Ein Eintrag wie im Entwurf: SOPs, Bibliothek und Symptome sind EINE
      // durchsuchbare Sammlung. Laufende SOPs wohnen bei den Aufgaben und im
      // Grow-Detail; die Ersten Schritte öffnet man aus den Einstellungen.
      { to: '/wissen', label: 'SOPs & Bibliothek', end: true, keywords: 'sop anleitung ablauf prozedur checkliste nachschlagen growplan quellen doku symptome bibliothek wissen' },
    ],
  },
]

/** Mobile Bottom-Nav = die vier Ziele der Gruppe "Jetzt". */
export const mobilePrimaryNav = navGroups[0].items

/**
 * Alte Pfade, die weiterhin funktionieren müssen.
 *
 * Mit Tab-Angabe, sonst landet ein Lesezeichen auf /alarme zwar auf der richtigen
 * Seite, aber auf deren erstem Tab — also bei der Automatik statt bei den
 * Grenzwerten. Das ist schlimmer als ein toter Link, weil es unbemerkt bleibt.
 */
export const legacyRedirects: Record<string, string> = {
  '/automatik': '/regeln?tab=automatik',
  '/alarme': '/regeln?tab=grenzwerte',
  '/benachrichtigungen': '/regeln?tab=push',
  '/assistent': '/regeln?tab=ki',
  '/phenohunt': '/sorten',
  '/hardware': '/sensoren',
  '/analyse': '/archiv',
  '/action': '/aufgaben',
}

export const searchablePages = navGroups.flatMap((group) =>
  group.items.map((item) => ({
    label: item.label,
    route: item.to,
    keywords: `${group.label} ${item.keywords ?? ''}`,
  })))

export function isNavLeafActive(item: NavLeaf, pathname: string): boolean {
  return item.end ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`)
}
