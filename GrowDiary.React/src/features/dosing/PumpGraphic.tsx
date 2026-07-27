import { classNames } from '../../utils'

/**
 * Die Peristaltikpumpe, nach der Zeichnung des Besitzers: Gehäuse mit zwei
 * Füßen, Rotor mit zwei Rollen, der Schlauch außen herum.
 *
 * Sie dreht sich NUR, wenn wirklich dosiert wird. Eine Dauer-Animation wäre
 * Dekoration — und bei einer Pumpe, die Säure fördert, eine gefährliche: man
 * würde aufhören hinzusehen.
 */
export function PumpGraphic({ dosing, tone = 'danger' }: { dosing: boolean; tone?: 'danger' | 'accent' | 'info' }) {
  return (
    <svg
      className={classNames('pump-svg', dosing && 'is-dosing', `is-${tone}`)}
      viewBox="0 0 200 200"
      role="img"
      aria-label={dosing ? 'Pumpe dosiert gerade' : 'Pumpe steht'}
    >
      <rect className="pump-housing" x="12" y="16" width="176" height="156" rx="5" />
      <rect className="pump-foot" x="42" y="172" width="26" height="20" />
      <rect className="pump-foot" x="132" y="172" width="26" height="20" />
      <path className="pump-tube" d="M 60 143 A 62 62 0 1 1 140 143" />
      <path className="pump-liquid" d="M 60 143 A 62 62 0 1 1 140 143" />
      <g className="pump-rotor">
        <circle cx="100" cy="96" r="47" />
        <circle className="pump-roller" cx="100" cy="49" r="13" />
        <circle className="pump-roller" cx="100" cy="143" r="13" />
      </g>
    </svg>
  )
}
