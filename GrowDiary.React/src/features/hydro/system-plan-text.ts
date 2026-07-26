/* Texte zum Systemplan.
   Eigene Datei, weil eine Komponentendatei nur Komponenten exportieren soll —
   sonst verliert Fast Refresh den Zustand bei jeder Aenderung. */
import type { SystemPlan } from './system-plan-model'

export function fitMessage(plan: SystemPlan): string {
  if (plan.sites.length <= 1) return 'Einzeleimer'
  return plan.fits
    ? `Passt: ${Math.round(plan.aisleCm)} cm Gang seitlich, ${Math.round(plan.rowGapCm)} cm zwischen den Reihen.`
    : `Zu eng: nur ${Math.round(Math.min(plan.aisleCm, plan.rowGapCm))} cm Luft. Weniger Sites, kleinere Eimer oder größeres Zelt.`
}
