import type { GrowSummary } from '../../types'

// The topbar for a grow-scoped page: page title on the left, a grow switcher on the
// right. Same chrome as the other pages (.topbar), so these pages read as first-class
// destinations, not something buried inside a grow.
export function GrowScopeHeader({
  title,
  grows,
  growId,
  onChange,
}: {
  title: string
  grows: GrowSummary[]
  growId: string | undefined
  onChange: (growId: string) => void
}) {
  return (
    <div className="topbar">
      <div className="topbar-left">
        <span className="topbar-title">{title}</span>
      </div>
      <div className="topbar-right">
        <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: 'var(--muted)' }}>
          Grow
          <select
            className="meas-input"
            style={{ maxWidth: 240 }}
            value={growId ?? ''}
            onChange={(event) => onChange(event.target.value)}
            aria-label="Grow wählen"
            disabled={grows.length === 0}
          >
            {grows.length === 0 && <option value="">Kein aktiver Grow</option>}
            {grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
          </select>
        </label>
      </div>
    </div>
  )
}
