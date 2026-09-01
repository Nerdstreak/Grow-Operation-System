import { useEffect, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import { V1Alert, V1Card, V1Empty, V1Page, V1Skeleton } from '../components/v1'
import { ChangeoutsPanel } from '../features/changeouts/ChangeoutsPanel'
import { WasserwechselStand } from '../features/changeouts/WasserwechselStand'
import { GrowScopePicker } from '../features/grow-scope/GrowScopePicker'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import type { WasserwechselStandDto } from '../types'
import '../features/changeouts/changeouts.css'

/**
 * Der Wasserwechsel — eine eigene Seite, weil er eine eigene Handlung ist.
 *
 * <b>Der Anlass (31.08.2026).</b> Gemeldet: „der User findet den Wasserwechsel
 * nicht wirklich, das ist sehr umständlich von uns gelöst, weil er hat jetzt
 * einen gemacht und will den eintragen und zurückdatieren."
 *
 * Der Weg dorthin war: Menü „Addback" → scrollen → dritter Abschnitt →
 * „Wechsel erfassen". Das Wort „Wasserwechsel" stand auf dem ganzen Weg
 * nirgends, im Hauptmenü überhaupt nicht — und wer es in die Suche tippte,
 * landete auf der Aufgabenseite, weil das Wort dort als Schlagwort stand.
 *
 * <b>Warum eine eigene Seite und kein zweites Formular.</b> Die Regel dieses
 * Projekts sagt: führt eine neue Seite dieselbe Hauptaktion wie eine andere,
 * ist das ein Befund und kein Feature. Deshalb ist das Formular hierher
 * <b>umgezogen</b>; auf /addback steht jetzt nur noch der Stand mit einem Weg
 * hierher. Nachfüllen und Wechseln sind zwei Handlungen — Wasser dazugeben ist
 * nicht Wasser austauschen.
 */
export default function WasserwechselPage() {
  const { grows, growId, setGrowId, loading, error } = useSelectedGrow()
  const grow = grows.find((item) => String(item.id) === String(growId)) ?? null

  const [stand, setStand] = useState<WasserwechselStandDto | null>(null)
  const [standFehler, setStandFehler] = useState<string | null>(null)
  const [neuGeladen, setNeuGeladen] = useState(0)

  const growId2 = grow?.id ?? null
  useEffect(() => {
    if (growId2 == null) return
    const controller = new AbortController()
    async function laden(id: number) {
      try {
        const daten = await apiFetch<WasserwechselStandDto>(
          `/api/grows/${id}/changeouts/stand`, { signal: controller.signal })
        if (controller.signal.aborted) return
        setStand(daten)
        setStandFehler(null)
      } catch (caught) {
        if (controller.signal.aborted) return
        setStandFehler(caught instanceof ApiRequestError ? caught.message : 'Stand konnte nicht geladen werden.')
      }
    }
    void laden(growId2)
    return () => controller.abort()
  }, [growId2, neuGeladen])

  return (
    <V1Page
      eyebrow="Jetzt"
      title="Wasserwechsel"
      subtitle="Wann zuletzt gewechselt wurde — und der Eintrag für den, den du gerade gemacht hast."
      action={<GrowScopePicker grows={grows} growId={growId} onChange={setGrowId} />}
    >
      {error && <V1Alert message={error} tone="critical" />}
      {standFehler && <V1Alert message={standFehler} tone="warn" />}

      {loading ? (
        <V1Skeleton rows={4} label="Lade Wasserwechsel" />
      ) : grows.length === 0 ? (
        <V1Empty
          title="Kein aktiver Grow"
          text="Der Wasserwechsel gehört zu einem laufenden Grow. Leg zuerst einen an."
        />
      ) : !grow ? null : (
        <>
          {stand && (
            <V1Card className="ww-stand-card">
              <WasserwechselStand stand={stand} />
            </V1Card>
          )}

          {/* Ein nachgetragener Wechsel verschiebt den Stand — sonst stuenden
              oben 9 Tage, waehrend unten der Eintrag von gestern steht. */}
          <ChangeoutsPanel
            growId={grow.id}
            growName={grow.name}
            offenBeiStart={stand?.zustand === 'faellig' || stand?.zustand === 'ueberfaellig'}
            onGespeichert={() => setNeuGeladen((wert) => wert + 1)}
            leerHinweis={leerHinweis(stand)}
          />
        </>
      )}
    </V1Page>
  )
}

/**
 * Was in der leeren Liste steht — ohne der Zahl darüber zu widersprechen.
 *
 * Ein Wechsel kann auf zwei Wegen belegt sein: als Häkchen an einer Messung
 * oder als Eintrag hier. Steht oben „vor 0 Tagen" und unten „noch kein
 * Wasserwechsel", ist beides wahr und der Nutzer trotzdem verwirrt — genau so
 * sah die Seite am 31.08.2026 bei der ersten Sicht aus.
 */
function leerHinweis(stand: WasserwechselStandDto | null): string | undefined {
  if (stand?.zuletztUtc == null) return undefined
  const wann = new Date(stand.zuletztUtc).toLocaleDateString('de-DE')
  return `Der letzte belegte Wechsel (${wann}) kommt aus einer Messung — dort war „Lösungswechsel" angehakt. `
    + 'Über dieses Formular ist noch keiner eingetragen; beide zählen gleich.'
}
