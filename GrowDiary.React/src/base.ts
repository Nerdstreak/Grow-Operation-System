// Runtime base-path resolution so the app works both at the site root and behind
// the Home Assistant ingress proxy, where it is served under a dynamic path like
// /api/hassio_ingress/<token>/. The backend injects a matching <base href> into
// index.html, so document.baseURI is the single source of truth here — the base
// cannot be baked in at build time because the ingress token changes per request.

/** Absolute base URL of the app, e.g. "https://host/" or "https://host/api/hassio_ingress/abc/".
 *
 * Ausserhalb des Browsers gibt es kein `document` — etwa in den Unit-Tests, die
 * ohne DOM laufen. Ein Modul, das beim blossen Importieren zerbricht, zwingt
 * sonst jeden Test, der irgendwo daran haengt, in eine DOM-Umgebung. Der
 * Rueckfall ist bedeutungslos: dort wird keine URL aufgeloest. */
const BASE_HREF = typeof document !== 'undefined' ? document.baseURI : 'http://localhost/'

/** Base path only, always ending in a slash: "/" or "/api/hassio_ingress/abc/". */
export const APP_BASE_PATH = new URL(BASE_HREF).pathname

/** React Router basename (no trailing slash; "/" at the site root). */
export const ROUTER_BASENAME = APP_BASE_PATH.replace(/\/$/, '') || '/'

/** Resolves an app-absolute path (e.g. "/api/foo") against the runtime base. */
export function resolveUrl(path: string): string {
  return new URL(path.replace(/^\/+/, ''), BASE_HREF).toString()
}
