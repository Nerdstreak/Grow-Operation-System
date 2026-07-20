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

    throw new ApiRequestError(response.status, payload, `API request failed with status ${response.status}`)
  }

  return parseResponse<T>(response)
}
