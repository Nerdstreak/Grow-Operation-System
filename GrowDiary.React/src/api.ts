import { resolveUrl } from './base'
import type { ApiError } from './types'

export class ApiRequestError extends Error {
  status: number
  payload: ApiError | null

  constructor(status: number, payload: ApiError | null, fallbackMessage: string) {
    super(payload?.message ?? fallbackMessage)
    this.status = status
    this.payload = payload
  }
}

/* ── Remote admin key (for accessing protected APIs from another device) ── */
const ADMIN_KEY_STORAGE = 'growos.adminKey'
export const ADMIN_KEY_HEADER = 'X-GrowOS-Admin-Key'
export const ADMIN_EVENTS = { required: 'growos:admin-required', open: 'growos:admin-open' } as const

export function getAdminKey(): string {
  try {
    return localStorage.getItem(ADMIN_KEY_STORAGE) ?? ''
  } catch {
    return ''
  }
}

export function setAdminKey(key: string): void {
  try {
    const trimmed = key.trim()
    if (trimmed) localStorage.setItem(ADMIN_KEY_STORAGE, trimmed)
    else localStorage.removeItem(ADMIN_KEY_STORAGE)
  } catch {
    /* localStorage unavailable — ignore */
  }
}

export function openAdminKeyDialog(): void {
  try {
    window.dispatchEvent(new CustomEvent(ADMIN_EVENTS.open))
  } catch {
    /* no window — ignore */
  }
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T
  }

  const text = await response.text()
  if (!text) {
    return undefined as T
  }

  return JSON.parse(text) as T
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  if (!(init?.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const adminKey = getAdminKey()
  if (adminKey && !headers.has(ADMIN_KEY_HEADER)) {
    headers.set(ADMIN_KEY_HEADER, adminKey)
  }

  const response = await fetch(resolveUrl(path), {
    ...init,
    headers,
  })

  if (!response.ok) {
    let payload: ApiError | null

    try {
      payload = await parseResponse<ApiError>(response)
    } catch {
      payload = null
    }

    if (response.status === 403 && payload?.code === 'admin_access_required') {
      try {
        window.dispatchEvent(new CustomEvent(ADMIN_EVENTS.required))
      } catch {
        /* no window — ignore */
      }
    }

    throw new ApiRequestError(response.status, payload, `API request failed with status ${response.status}`)
  }

  return parseResponse<T>(response)
}
