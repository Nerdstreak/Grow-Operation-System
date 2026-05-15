import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { classNames, formatDate, formatNumber } from '../utils'

export type Tone = 'neutral' | 'ok' | 'warn' | 'critical' | 'accent'

export function V1Page({ eyebrow, title, subtitle, action, children, className }: { eyebrow?: string; title: string; subtitle?: string; action?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <main className={classNames('v1-page', className)}>
      <section className="v1-hero">
        <div>
          {eyebrow && <div className="v1-eyebrow">{eyebrow}</div>}
          <h1>{title}</h1>
          {subtitle && <p>{subtitle}</p>}
        </div>
        {action && <div className="v1-hero-action">{action}</div>}
      </section>
      {children}
    </main>
  )
}

export function V1Section({ title, action, children, className }: { title: string; action?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={classNames('v1-section', className)}>
      <header className="v1-section-head">
        <h2>{title}</h2>
        {action}
      </header>
      <div className="v1-section-body">{children}</div>
    </section>
  )
}

export function V1Card({ children, className, tone = 'neutral' }: { children: ReactNode; className?: string; tone?: Tone }) {
  return <article className={classNames('v1-card', `tone-${tone}`, className)}>{children}</article>
}

export function V1Button({ children, onClick, type = 'button', disabled, variant = 'secondary', className }: { children: ReactNode; onClick?: () => void; type?: 'button' | 'submit'; disabled?: boolean; variant?: 'primary' | 'secondary' | 'ghost' | 'danger'; className?: string }) {
  return <button type={type} className={classNames('v1-button', `is-${variant}`, className)} disabled={disabled} onClick={onClick}>{children}</button>
}

export function V1LinkButton({ to, children, variant = 'secondary', className }: { to: string; children: ReactNode; variant?: 'primary' | 'secondary' | 'ghost' | 'danger'; className?: string }) {
  return <Link to={to} className={classNames('v1-button', `is-${variant}`, className)}>{children}</Link>
}

export function V1Badge({ children, tone = 'neutral' }: { children: ReactNode; tone?: Tone }) {
  return <span className={classNames('v1-badge', `tone-${tone}`)}>{children}</span>
}

export function V1Stat({ label, value, unit, hint, tone = 'neutral' }: { label: string; value: ReactNode; unit?: string | null; hint?: string | null; tone?: Tone }) {
  return (
    <div className={classNames('v1-stat', `tone-${tone}`)}>
      <span>{label}</span>
      <strong>{value}{unit && value !== '–' && <em>{unit}</em>}</strong>
      {hint && <small>{hint}</small>}
    </div>
  )
}

export function V1Empty({ title, text, action }: { title: string; text?: string; action?: ReactNode }) {
  return (
    <div className="v1-empty">
      <strong>{title}</strong>
      {text && <span>{text}</span>}
      {action}
    </div>
  )
}

export function V1Alert({ title, message, tone = 'warn' }: { title?: string; message: string; tone?: Tone }) {
  return (
    <div className={classNames('v1-alert', `tone-${tone}`)}>
      {title && <strong>{title}</strong>}
      <span>{message}</span>
    </div>
  )
}

export function V1Tabs<T extends string | number>({ items, active, onChange, label }: { items: Array<{ value: T; label: string; meta?: string | null }>; active: T; onChange: (value: T) => void; label?: string }) {
  return (
    <div className="v1-tabs" role="tablist" aria-label={label}>
      {items.map((item) => (
        <button key={String(item.value)} type="button" className={classNames('v1-tab', item.value === active && 'active')} onClick={() => onChange(item.value)}>
          <strong>{item.label}</strong>
          {item.meta && <span>{item.meta}</span>}
        </button>
      ))}
    </div>
  )
}

export function V1Field({ label, children, hint, wide }: { label: string; children: ReactNode; hint?: string | null; wide?: boolean }) {
  return (
    <label className={classNames('v1-field', wide && 'is-wide')}>
      <span>{label}</span>
      {children}
      {hint && <small>{hint}</small>}
    </label>
  )
}

export function V1Switch({ label, checked, onChange, hint }: { label: string; checked: boolean; onChange: (checked: boolean) => void; hint?: string }) {
  return (
    <label className="v1-switch">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>
        <strong>{label}</strong>
        {hint && <small>{hint}</small>}
      </span>
    </label>
  )
}

export function V1Wizard({ steps, currentStep, onStep }: { steps: string[]; currentStep: number; onStep?: (step: number) => void }) {
  return (
    <div className="v1-wizard-steps">
      {steps.map((step, index) => {
        const number = index + 1
        return (
          <button key={step} type="button" className={classNames('v1-wizard-step', currentStep === number && 'active', currentStep > number && 'done')} onClick={() => onStep?.(number)} disabled={!onStep}>
            <span>{number}</span>
            <strong>{step}</strong>
          </button>
        )
      })}
    </div>
  )
}

export function V1Skeleton({ label = 'Lädt...' }: { label?: string }) {
  return <div className="v1-skeleton">{label}</div>
}

export function numberInput(value: string, onChange: (value: string) => void, props?: Record<string, unknown>) {
  return <input inputMode="decimal" value={value} onChange={(event) => onChange(event.target.value)} {...props} />
}

export function textOrDash(value: string | number | null | undefined) {
  if (value === null || value === undefined || value === '') return '–'
  return String(value)
}

export function formatLiters(value: number | null | undefined) {
  return value == null ? '–' : `${formatNumber(value, 1)} L`
}

export function formatDateShort(value: string | null | undefined) {
  return formatDate(value)
}

export function toNullableString(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim()
  return trimmed.length === 0 ? null : trimmed
}

export function toNullableInt(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number.parseInt(trimmed, 10)
  return Number.isFinite(parsed) ? parsed : null
}

export function toNullableFloat(value: string): number | null {
  const trimmed = value.trim().replace(',', '.')
  if (!trimmed) return null
  const parsed = Number.parseFloat(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

export function draftNumber(value: number | null | undefined) {
  return value == null || Number.isNaN(value) ? '' : String(value)
}
