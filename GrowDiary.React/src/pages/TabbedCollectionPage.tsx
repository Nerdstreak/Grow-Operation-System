import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { V1Tabs } from '../components/v1'

export type CollectionTab = {
  /** Wert in der Adresszeile, z. B. ?tab=grenzwerte */
  key: string
  label: string
  render: () => React.ReactNode
}

/**
 * Eine Seite, die mehrere bisher getrennte Seiten unter Tabs zusammenfasst.
 *
 * Die Navigation ist von 23 Zielen auf 14 geschrumpft, indem Verwandtes
 * zusammengezogen wurde — Automatik, Grenzwerte und Benachrichtigungen sind
 * dasselbe Thema und standen als drei Einträge nebeneinander.
 *
 * Der aktive Tab steht in der Adresszeile, damit ein Lesezeichen oder ein Link
 * aus einem Home-Assistant-Dashboard wieder dort landet, wo er hinzeigt. Die
 * alten Pfade leiten auf den jeweiligen Tab um, statt ins Leere zu laufen.
 */
export function TabbedCollectionPage({ tabs, paramName = 'tab' }: { tabs: CollectionTab[]; paramName?: string }) {
  const [params, setParams] = useSearchParams()
  const requested = params.get(paramName)
  const active = useMemo(
    () => tabs.find((tab) => tab.key === requested) ?? tabs[0],
    [tabs, requested],
  )

  return (
    <>
      <V1Tabs
        items={tabs.map((tab) => ({ value: tab.key, label: tab.label, audit: `collection-tab-${tab.key}` }))}
        active={active.key}
        onChange={(key) => {
          const next = new URLSearchParams(params)
          next.set(paramName, String(key))
          // replace: Tabwechsel soll den Zurück-Knopf nicht vollmüllen.
          setParams(next, { replace: true })
        }}
        label="Bereich"
      />
      {active.render()}
    </>
  )
}
