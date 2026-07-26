import { classNames } from '../../utils'

export type FlowStep = {
  /** „MESSEN", „ZIEL", „DOSIEREN", „KONTROLLE" */
  title: string
  /** Die eine Zahl, um die es in diesem Schritt geht. */
  value: string
  note?: string
  done: boolean
  current: boolean
}

/**
 * Der Addback-Ablauf als Streifen über der Seite.
 *
 * Addback war ein Assistent mit sechs Schritten, durch die man sich klickte. Wer
 * am Reservoir steht, will Istwert, Ziel und Dosiermenge aber gleichzeitig sehen
 * — der Weg dahin ist keine Entscheidungsfolge, sondern immer derselbe. Also
 * eine Seite, und darüber vier Schritte, die sagen, wo man gerade ist.
 *
 * Bewusst nicht anklickbar: alles steht ohnehin untereinander auf derselben
 * Seite, und ein Sprung nach vorn hätte nur mit halben Daten gerechnet.
 */
export function AddbackFlow({ steps }: { steps: FlowStep[] }) {
  return (
    <ol className="ab-flow" data-audit="addback-flow">
      {steps.map((step, index) => (
        <li
          key={step.title}
          className={classNames('ab-flow-step', step.done && 'is-done', step.current && 'is-current')}
          aria-current={step.current ? 'step' : undefined}
        >
          <div className="ab-flow-head">
            <span className="ab-flow-no">{index + 1}</span>
            <span className="ab-flow-title">{step.title}</span>
          </div>
          <div className="ab-flow-value">{step.value}</div>
          {step.note && <div className="ab-flow-note">{step.note}</div>}
        </li>
      ))}
    </ol>
  )
}
