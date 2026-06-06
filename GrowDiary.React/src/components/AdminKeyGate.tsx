import { useEffect, useState } from 'react'
import { ADMIN_EVENTS, getAdminKey, setAdminKey } from '../api'
import './admin-gate.css'

/**
 * Overlay shown when a protected API call is rejected for a remote device
 * (HTTP 403 `admin_access_required`). Lets the user enter the Admin-Key once;
 * it is stored in localStorage and sent on every request thereafter.
 * Can also be opened manually via openAdminKeyDialog() to change/clear the key.
 */
export default function AdminKeyGate() {
  const [open, setOpen] = useState(false)
  const [rejected, setRejected] = useState(false)
  const [keyInput, setKeyInput] = useState('')

  useEffect(() => {
    const onRequired = () => {
      setKeyInput(getAdminKey())
      setRejected(getAdminKey().length > 0)
      setOpen(true)
    }
    const onOpen = () => {
      setKeyInput(getAdminKey())
      setRejected(false)
      setOpen(true)
    }
    window.addEventListener(ADMIN_EVENTS.required, onRequired)
    window.addEventListener(ADMIN_EVENTS.open, onOpen)
    return () => {
      window.removeEventListener(ADMIN_EVENTS.required, onRequired)
      window.removeEventListener(ADMIN_EVENTS.open, onOpen)
    }
  }, [])

  if (!open) return null

  const save = () => {
    setAdminKey(keyInput)
    window.location.reload()
  }

  return (
    <div className="admin-gate" role="dialog" aria-modal="true" aria-label="Remote-Zugriff">
      <div className="admin-gate-card">
        <span className="admin-gate-kicker">Remote-Zugriff</span>
        <h2>{rejected ? 'Admin-Key abgelehnt' : 'Admin-Key eingeben'}</h2>
        <p>
          Dieses Gerät greift aus der Ferne zu. Gib den Admin-Key ein, um Grows, Messungen und
          Einstellungen freizuschalten.
          {rejected ? ' Der gespeicherte Key wurde abgelehnt — bitte prüfe ihn.' : ''}
        </p>
        <input
          type="text"
          inputMode="text"
          autoComplete="off"
          autoCapitalize="none"
          autoCorrect="off"
          spellCheck={false}
          value={keyInput}
          onChange={(event) => setKeyInput(event.target.value)}
          onKeyDown={(event) => { if (event.key === 'Enter' && keyInput.trim()) save() }}
          placeholder="Admin-Key"
          aria-label="Admin-Key"
        />
        <div className="admin-gate-actions">
          <button type="button" className="admin-gate-secondary" onClick={() => setOpen(false)}>Später</button>
          <button type="button" className="admin-gate-primary" onClick={save} disabled={!keyInput.trim()}>
            Speichern &amp; neu laden
          </button>
        </div>
      </div>
    </div>
  )
}
