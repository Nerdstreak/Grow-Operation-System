import type { GrowSummary } from '../../types'

// The grow switcher for the top-level grow-scoped pages. Rendered in the V1 page
// header's action slot, so it looks consistent and — unlike the old .topbar version —
// shows on mobile too.
export function GrowScopePicker({
  grows,
  growId,
  onChange,
}: {
  grows: GrowSummary[]
  growId: string | undefined
  onChange: (growId: string) => void
}) {
  return (
    <label className="v1-scope-picker">
      <span>Grow</span>
      <select
        value={growId ?? ''}
        onChange={(event) => onChange(event.target.value)}
        aria-label="Grow wählen"
        disabled={grows.length === 0}
      >
        {grows.length === 0 && <option value="">Kein aktiver Grow</option>}
        {grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
      </select>
    </label>
  )
}
