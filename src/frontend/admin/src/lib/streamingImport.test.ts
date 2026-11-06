import { describe, expect, it, vi } from 'vitest'
import { readLines, readSse } from '@/lib/streamingFetch'
import { runStreamingImport, type ImportEndpoints, type ImportProgress, type ImportRow } from '@/lib/streamingImport'

/** A ReadableStream that delivers the given chunks one by one (bytes can be split anywhere, as the network may do). */
function streamOf(chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk))
      controller.close()
    },
  })
}

function streamOfBytes(chunks: Uint8Array[]): ReadableStream<Uint8Array> {
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(chunk)
      controller.close()
    },
  })
}

async function collect<T>(iterator: AsyncIterable<T>): Promise<T[]> {
  const out: T[] = []
  for await (const item of iterator) out.push(item)
  return out
}

describe('readLines', () => {
  it('joins a line split across chunks, accepts CRLF and a last line without a newline', async () => {
    const lines = await collect(readLines(streamOf(['{"a":', '1}\r\n{"b"', ':2}\n', 'tail'])))
    expect(lines).toEqual(['{"a":1}', '{"b":2}', 'tail'])
  })

  it('keeps a UTF-8 character that is split between two chunks', async () => {
    const bytes = new TextEncoder().encode('Nguyễn\n')
    const lines = await collect(readLines(streamOfBytes([bytes.slice(0, 5), bytes.slice(5)])))
    expect(lines).toEqual(['Nguyễn'])
  })

  it('yields empty lines (they matter for SSE) and nothing for an empty body', async () => {
    expect(await collect(readLines(streamOf(['a\n\nb\n'])))).toEqual(['a', '', 'b'])
    expect(await collect(readLines(streamOf([])))).toEqual([])
  })
})

describe('readSse', () => {
  it('reads named events, multi-line data and ignores comment heartbeats', async () => {
    const messages = await collect(readSse(streamOf([': connected\nretry: 3000\n\n', 'event: progress\ndata: {"processed"', ':1}\n\n', ': ping\n\n', 'event: completed\ndata: line1\ndata: line2\n\n'])))
    expect(messages).toEqual([
      { event: 'progress', data: '{"processed":1}' },
      { event: 'completed', data: 'line1\nline2' },
    ])
  })

  it('uses "message" when no event name is given and drops a block without data', async () => {
    const messages = await collect(readSse(streamOf(['data: hello\n\n', 'event: lonely\n\n'])))
    expect(messages).toEqual([{ event: 'message', data: 'hello' }])
  })
})

// ── the import runner ───────────────────────────────────────────────────

const endpoints: ImportEndpoints = {
  upload: '/prd/api/v1/products/import',
  events: (id) => `/prd/api/v1/products/import/${id}/events`,
  template: '/prd/api/v1/products/import/template',
}

const progress = (over: Partial<ImportProgress> = {}): ImportProgress => ({
  state: 'running', processed: 1, created: 1, skipped: 0, failed: 0, bytesRead: 10, totalBytes: 100, percent: 10, elapsedMs: 5, ...over,
})

function response(body: ReadableStream<Uint8Array> | string, init: ResponseInit = {}): Response {
  return new Response(typeof body === 'string' ? body : body, { status: 200, ...init })
}

function sse(events: ImportProgress[]): Response {
  return response(streamOf(events.map((e) => `event: ${e.state === 'running' ? 'progress' : e.state}\ndata: ${JSON.stringify(e)}\n\n`)))
}

const file = new Blob(['sku,name\nA,One\n'], { type: 'text/csv' })

describe('runStreamingImport', () => {
  it('opens the progress stream first, posts the file raw with the same import id, and reports rows, progress and the summary', async () => {
    const calls: Array<{ url: string; init?: RequestInit }> = []
    const fetcher = vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({ url, init })
      if (url.includes('/events')) return sse([progress(), progress({ state: 'completed', processed: 2, created: 2, percent: 100 })])
      return response(streamOf([
        '{"importId":"id-1","dryRun":false,"type":"started"}\n',
        '{"line":2,"key":"A","status":"created","message":null,"type":"row"}\n{"line":3,"key":"B","status":"skipped","message":"exists","type":"row"}\n',
        '{"processed":2,"created":1,"skipped":1,"failed":0,"dryRun":false,"elapsedMs":30,"type":"summary"}\n',
      ]))
    })
    const rows: ImportRow[] = []
    const seen: ImportProgress[] = []
    const started = vi.fn()

    const result = await runStreamingImport({
      endpoints, file, dryRun: false, fallbackError: 'failed', newId: () => 'id-1', fetcher,
      callbacks: { onRow: (r) => rows.push(r), onProgress: (p) => seen.push(p), onStarted: started },
    })

    expect(calls[0].url).toBe('/prd/api/v1/products/import/id-1/events')
    expect(calls[1].url).toBe('/prd/api/v1/products/import?importId=id-1&dryRun=false')
    expect(calls[1].init?.method).toBe('POST')
    expect(calls[1].init?.body).toBe(file)
    expect((calls[1].init?.headers as Record<string, string>)['Content-Type']).toBe('text/csv')
    expect(started).toHaveBeenCalledWith('id-1')
    expect(rows.map((r) => r.status)).toEqual(['created', 'skipped'])
    expect(seen.map((p) => p.state)).toEqual(['running', 'completed'])
    expect(result.summary).toMatchObject({ processed: 2, created: 1, skipped: 1, failed: 0 })
    expect(result.error).toBeUndefined()
  })

  it('sends dryRun=true as asked', async () => {
    const fetcher = vi.fn(async (url: string) => (url.includes('/events') ? response('', { status: 404 }) : response(streamOf(['{"processed":0,"created":0,"skipped":0,"failed":0,"dryRun":true,"elapsedMs":1,"type":"summary"}\n']))))

    const result = await runStreamingImport({ endpoints, file, dryRun: true, fallbackError: 'failed', newId: () => 'x', fetcher })

    expect(fetcher.mock.calls[1][0]).toContain('dryRun=true')
    expect(result.summary?.dryRun).toBe(true)
  })

  it('still imports when the progress stream cannot be opened', async () => {
    const fetcher = vi.fn(async (url: string) => {
      if (url.includes('/events')) throw new TypeError('network')
      return response(streamOf(['{"line":2,"key":"A","status":"created","type":"row"}\n', '{"processed":1,"created":1,"skipped":0,"failed":0,"dryRun":false,"elapsedMs":1,"type":"summary"}\n']))
    })
    const rows: ImportRow[] = []

    const result = await runStreamingImport({ endpoints, file, dryRun: false, fallbackError: 'failed', newId: () => 'x', fetcher, callbacks: { onRow: (r) => rows.push(r) } })

    expect(rows).toHaveLength(1)
    expect(result.summary?.created).toBe(1)
  })

  it('turns a refusal before the stream into the server sentence (ProblemDetails detail)', async () => {
    const fetcher = vi.fn(async (url: string) =>
      url.includes('/events')
        ? response('', { status: 404 })
        : response('{"title":"Business rule violation.","detail":"The header lacks the required column(s): sku."}', { status: 400 }))

    const result = await runStreamingImport({ endpoints, file, dryRun: false, fallbackError: 'failed', newId: () => 'x', fetcher })

    expect(result.error).toBe('The header lacks the required column(s): sku.')
    expect(result.summary).toBeUndefined()
  })

  it('reports an error line of the stream as the result error and keeps the rows before it', async () => {
    const fetcher = vi.fn(async (url: string) =>
      url.includes('/events') ? response('', { status: 404 }) : response(streamOf(['{"line":2,"key":"A","status":"created","type":"row"}\n', '{"message":"The import stopped: a batch could not be saved.","type":"error"}\n'])))
    const rows: ImportRow[] = []

    const result = await runStreamingImport({ endpoints, file, dryRun: false, fallbackError: 'failed', newId: () => 'x', fetcher, callbacks: { onRow: (r) => rows.push(r) } })

    expect(rows).toHaveLength(1)
    expect(result.error).toContain('could not be saved')
  })

  it('uses the generic sentence for a network failure and marks an aborted run as aborted', async () => {
    const failing = vi.fn(async (url: string) => {
      if (url.includes('/events')) return response('', { status: 404 })
      throw new TypeError('Failed to fetch')
    })
    expect((await runStreamingImport({ endpoints, file, dryRun: false, fallbackError: 'generic', newId: () => 'x', fetcher: failing })).error).toBe('generic')

    const controller = new AbortController()
    const aborting = vi.fn(async (url: string) => {
      if (url.includes('/events')) return response('', { status: 404 })
      controller.abort()
      throw new DOMException('Aborted', 'AbortError')
    })
    const result = await runStreamingImport({ endpoints, file, dryRun: false, fallbackError: 'generic', newId: () => 'x', fetcher: aborting, signal: controller.signal })
    expect(result.aborted).toBe(true)
    expect(result.error).toBeUndefined()
  })

  it('ignores a malformed line and a malformed progress event', async () => {
    const fetcher = vi.fn(async (url: string) =>
      url.includes('/events')
        ? response(streamOf(['event: progress\ndata: not-json\n\n']))
        : response(streamOf(['garbage\n', '{"processed":0,"created":0,"skipped":0,"failed":0,"dryRun":false,"elapsedMs":1,"type":"summary"}\n'])))

    const result = await runStreamingImport({ endpoints, file, dryRun: false, fallbackError: 'failed', newId: () => 'x', fetcher })

    expect(result.summary).toBeDefined()
  })
})
