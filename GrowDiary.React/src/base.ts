// Runtime base-path resolution so the app works both at the site root and behind
// the Home Assistant ingress proxy, where it is served under a dynamic path like
// /api/hassio_ingress/<token>/. The backend injects a matching <base href> into
// index.html, so document.baseURI is the single source of truth here — the base
// cannot be baked in at build time because the ingress token changes per request.

/** Absolute base URL of the app, e.g. "https://host/" or "https://host/api/hassio_ingress/abc/". */
const BASE_HREF = document.baseURI

/** Base path only, always ending in a slash: "/" or "/api/hassio_ingress/abc/". */
export const APP_BASE_PATH = new URL(BASE_HREF).pathname

/** React Router basename (no trailing slash; "/" at the site root). */
export const ROUTER_BASENAME = APP_BASE_PATH.replace(/\/$/, '') || '/'

/** True when served behind the Home Assistant ingress (i.e. not at the site root). */
export const IS_INGRESS = APP_BASE_PATH !== '/'

/** Resolves an app-absolute path (e.g. "/api/foo") against the runtime base. */
export function resolveUrl(path: string): string {
  return new URL(path.replace(/^\/+/, ''), BASE_HREF).toString()
}
