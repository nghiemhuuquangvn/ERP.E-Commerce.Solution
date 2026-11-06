import { authorizedFetch, readLines, readSse } from '@/lib/streamingFetch'

// The client side of a streaming import (docs/architecture/streaming-import-pattern.md): the progress is followed on a Server-Sent Events stream
// opened first, the CSV is POSTed as a raw body, and the answer - one JSON line per row - is read while it arrives.

export type ImportRowStatus = 'created' | 'would-create' | 'skipped' | 'error'

export interface ImportRow {
  line: number
  key?: string | null
  status: ImportRowStatus
  message?: string | null
}

export interface ImportSummary {
  processed: number
  created: number
  skipped: number
  failed: number
  dryRun: boolean
  elapsedMs: number
}

export interface ImportProgress {
  state: 'running' | 'completed' | 'failed'
  processed: number
  created: number
  skipped: number
  failed: number
  bytesRead: number
  totalBytes?: number | null
  percent?: number | null
  elapsedMs: number
  message?: string | null
}

export interface ImportEndpoints {
  /** POST target; `importId` and `dryRun` are added as query parameters. */
  upload: string
  /** GET target of the SSE progress stream. */
  events: (importId: string) => string
  template: string
}

export interface ImportCallbacks {
  onStarted?: (importId: string) => void
  onProgress?: (progress: ImportProgress) => void
  onRow?: (row: ImportRow) => void
}

export interface ImportResult {
  summary?: ImportSummary
  /** The import was refused or stopped: the server's sentence. Rows reported before it were processed as stated. */
  error?: string
  aborted?: boolean
}

type Fetcher = typeof authorizedFetch

function problemText(text: string, fallback: string): string {
  try {
    const body = JSON.parse(text) as { detail?: string; message?: string; title?: string }
    return body.detail ?? body.message ?? body.title ?? fallback
  } catch {
    return fallback
  }
}

/**
 * Runs one import. Resolves with the summary (or the reason it stopped); never rejects for an HTTP refusal or a network problem - those are
 * the result's `error`. Aborting the signal cancels both streams (the server stops after the batch it is saving).
 */
export async function runStreamingImport(options: {
  endpoints: ImportEndpoints
  file: Blob
  dryRun: boolean
  signal?: AbortSignal
  callbacks?: ImportCallbacks
  fallbackError: string
  newId?: () => string
  fetcher?: Fetcher
}): Promise<ImportResult> {
  const { endpoints, file, dryRun, signal, callbacks = {}, fallbackError } = options
  const fetcher = options.fetcher ?? authorizedFetch
  const importId = (options.newId ?? (() => crypto.randomUUID()))()
  const sseAbort = new AbortController()
  const onAbort = () => sseAbort.abort()
  signal?.addEventListener('abort', onAbort)

  // 1) Progress channel first: the subscription exists before the first byte of the file is sent. Its failure never stops the import.
  let sseDone: Promise<void> = Promise.resolve()
  try {
    const sse = await fetcher(endpoints.events(importId), { headers: { Accept: 'text/event-stream' }, signal: sseAbort.signal })
    if (sse.ok && sse.body) {
      sseDone = (async () => {
        try {
          for await (const message of readSse(sse.body!)) {
            try {
              callbacks.onProgress?.(JSON.parse(message.data) as ImportProgress)
            } catch {
              /* a malformed event is ignored */
            }
          }
        } catch {
          /* the stream ended or was aborted */
        }
      })()
    }
  } catch {
    /* no progress stream: the rows below still report everything */
  }

  const result: ImportResult = {}
  try {
    // 2) The streaming upload; the answer is read line by line while it arrives.
    const response = await fetcher(`${endpoints.upload}?importId=${importId}&dryRun=${dryRun}`, {
      method: 'POST',
      headers: { 'Content-Type': 'text/csv' },
      body: file,
      signal,
    })
    if (!response.ok || !response.body) {
      result.error = problemText(await response.text().catch(() => ''), fallbackError)
      return result
    }

    for await (const line of readLines(response.body)) {
      if (!line.trim()) continue
      let item: { type?: string } & Record<string, unknown>
      try {
        item = JSON.parse(line)
      } catch {
        continue
      }
      if (item.type === 'started') callbacks.onStarted?.(importId)
      else if (item.type === 'row') callbacks.onRow?.(item as unknown as ImportRow)
      else if (item.type === 'summary') result.summary = item as unknown as ImportSummary
      else if (item.type === 'error') result.error = String(item.message ?? fallbackError)
    }
  } catch {
    if (signal?.aborted) result.aborted = true
    else result.error = fallbackError   // a network problem: the generic sentence
  } finally {
    // Let the last progress event ("completed") arrive, then close the progress stream.
    await Promise.race([sseDone, new Promise<void>((resolve) => setTimeout(resolve, 800))])
    sseAbort.abort()
    signal?.removeEventListener('abort', onAbort)
  }
  return result
}
