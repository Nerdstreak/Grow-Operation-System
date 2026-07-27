import { useEffect, useState } from 'react'
import { apiFetch } from './api'

type GrowOption = { id: number; status?: string }

/**
 * Die zwei Zahlen in der Seitenleiste: offene Aufgaben und „Addback fällig".
 *
 * Sie zählen über ALLE laufenden Grows, nicht über einen ausgewählten. Das ist
 * kein Kompromiss, sondern das, was die Ziele zeigen: die Aufgaben-Seite listet
 * ohnehin die Aufgaben aller aktiven Grows, und der Addback-Hub führt jeden
 * Grow mit Reservoir. Ein Zähler, der etwas anderes meint als die Seite, auf die
 * er zeigt, ist schlimmer als keiner.
 *
 * Vorher hing das an einer globalen Zelt/Grow-Auswahl in der Kopfleiste. Die
 * Auswahl steuerte allerdings nur diese beiden Zahlen und sonst keine einzige
 * Seite — man stellte oben etwas ein und unten passierte nichts. Deshalb ist die
 * Leiste weg und jede Seite wählt selbst.
 */
export const TASKS_CHANGED_EVENT = 'growos-tasks-changed'

export function useNavCounts(): { addbackDue: boolean; openTasks: number } {
  const [openTasks, setOpenTasks] = useState(0)
  const [addbackDue, setAddbackDue] = useState(false)
  const [version, setVersion] = useState(0)

  // Abhaken und Anlegen melden sich per Event — sonst zeigte die Leiste die
  // alte Zahl, bis man die App neu lud.
  useEffect(() => {
    const onChange = () => setVersion((current) => current + 1)
    window.addEventListener(TASKS_CHANGED_EVENT, onChange)
    return () => window.removeEventListener(TASKS_CHANGED_EVENT, onChange)
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const grows = await apiFetch<GrowOption[]>('/api/grows?archived=false', { signal: controller.signal }).catch(() => [])
        const laufende = grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
        if (controller.signal.aborted || laufende.length === 0) return

        const [taskLists, deviationLists] = await Promise.all([
          Promise.all(laufende.map((grow) =>
            apiFetch<Array<{ status: string }>>(`/api/grows/${grow.id}/tasks`, { signal: controller.signal }).catch(() => []),
          )),
          Promise.all(laufende.map((grow) =>
            apiFetch<Array<{ stableKey: string }>>(`/api/grows/${grow.id}/deviations`, { signal: controller.signal }).catch(() => []),
          )),
        ])
        if (controller.signal.aborted) return

        setOpenTasks(taskLists.flat().filter((task) => task.status === 'Open').length)
        setAddbackDue(deviationLists.flat().some((deviation) => deviation.stableKey === 'hydro.ec'))
      } catch {
        /* Ein fehlendes Badge ist besser als ein falsches. */
      }
    }

    void load()
    return () => controller.abort()
  }, [version])

  return { addbackDue, openTasks }
}
