import { refreshAccessToken } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'

// axios (XHR) cannot read a response while it arrives and EventSource cannot send an Authorization header, so the streaming imports use `fetch`
// with the signed-in user's token (the same token, language header and 401 refresh as the axios clients).

const ACCEPT_LANGUAGE: Record<string, string> = { en: 'en-US', vi: 'vi-VN' }

function withAuth(init: RequestInit, token: string | null): RequestInit {
  const headers = new Headers(init.headers)
  if (token) headers.set('Authorization', `Bearer ${token}`)
  headers.set('Accept-Language', ACCEPT_LANGUAGE[useUiStore.getState().locale] ?? 'en-US')
  return { ...init, headers }
}

/**
 * fetch with the user's bearer token. On a 401 the token is refreshed once and the request sent again, so the body must be re-sendable
 * (a File, a Blob or a string is; a one-shot stream is not).
 */
export async function authorizedFetch(url: string, init: RequestInit = {}): Promise<Response> {
  let response = await fetch(url, withAuth(init, useAuthStore.getState().accessToken))
  if (response.status !== 401) return response
  let token: string
  try {
    token = await refreshAccessToken()
  } catch {
    return response
  }
  response = await fetch(url, withAuth(init, token))
  return response
}

/** The lines of a streamed text body, as they arrive: a chunk may end in the middle of a line (or of a UTF-8 character), CRLF is accepted. */
export async function* readLines(body: ReadableStream<Uint8Array>): AsyncGenerator<string> {
  const reader = body.getReader()
  const decoder = new TextDecoder('utf-8')
  let buffer = ''
  try {
    for (;;) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })
      let newline: number
      while ((newline = buffer.indexOf('\n')) >= 0) {
        yield buffer.slice(0, newline).replace(/\r$/, '')
        buffer = buffer.slice(newline + 1)
      }
    }
    buffer += decoder.decode()
    if (buffer.length > 0) yield buffer.replace(/\r$/, '')
  } finally {
    reader.releaseLock()
  }
}

export interface SseMessage {
  event: string
  data: string
}

/** Server-Sent Events from a streamed body: `event:` names, multi-line `data:`, `:` comments (heartbeats) ignored. */
export async function* readSse(body: ReadableStream<Uint8Array>): AsyncGenerator<SseMessage> {
  let event = 'message'
  let data: string[] = []
  for await (const line of readLines(body)) {
    if (line === '') {
      if (data.length > 0) yield { event, data: data.join('\n') }
      event = 'message'
      data = []
      continue
    }
    if (line.startsWith(':')) continue
    const colon = line.indexOf(':')
    const field = colon < 0 ? line : line.slice(0, colon)
    let value = colon < 0 ? '' : line.slice(colon + 1)
    if (value.startsWith(' ')) value = value.slice(1)
    if (field === 'event') event = value
    else if (field === 'data') data.push(value)
  }
}
