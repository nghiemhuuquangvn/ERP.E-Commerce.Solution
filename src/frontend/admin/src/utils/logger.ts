import { logsApi } from '@/api/logs/logs.api'
import type { ClientLogEntry, LogLevel } from '@/api/logs/logs.api'

const FLUSH_INTERVAL_MS = 5_000
const MAX_QUEUE_SIZE = 20

let queue: ClientLogEntry[] = []
let flushTimer: ReturnType<typeof setInterval> | null = null

function ensureTimer() {
  if (flushTimer) return
  flushTimer = setInterval(flush, FLUSH_INTERVAL_MS)
}

function enqueue(entry: ClientLogEntry) {
  queue.push(entry)
  ensureTimer()
  if (queue.length >= MAX_QUEUE_SIZE) {
    void flush()
  }
}

/** Sends the queued entries. Never throws — a logger must not break the app it logs for. */
function flush() {
  if (queue.length === 0) return
  const batch = queue
  queue = []
  logsApi.reportClient(batch).catch(() => {
    // Diagnostic logging is best-effort; a failed flush is silently dropped.
  })
}

function beaconFlush() {
  if (queue.length === 0) return
  const batch = queue
  queue = []
  try {
    const base = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
    const gatewayRoot = base.replace(/\/api\/?$/, '')
    navigator.sendBeacon(
      `${gatewayRoot}/logs/api/v1/logs/client`,
      new Blob([JSON.stringify(batch)], { type: 'application/json' }),
    )
  } catch {
    // Best-effort only.
  }
}

if (typeof window !== 'undefined') {
  window.addEventListener('beforeunload', beaconFlush)
}

export const logger = {
  trace(level: LogLevel, message: string, properties?: Record<string, string>) {
    enqueue({ serviceName: 'Admin', level, kind: 'Trace', message, properties })
  },

  exception(error: unknown, properties?: Record<string, string>) {
    const err = error instanceof Error ? error : new Error(String(error))
    enqueue({
      serviceName: 'Admin',
      level: 'Error',
      kind: 'Exception',
      message: err.message,
      exceptionType: err.name,
      stackTrace: err.stack,
      properties,
    })
  },
}
