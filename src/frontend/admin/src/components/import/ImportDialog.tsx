import { useCallback, useEffect, useRef, useState } from 'react'
import { Download, FileUp } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, Modal, Progress } from '@/components/ui'
import { authorizedFetch } from '@/lib/streamingFetch'
import {
  runStreamingImport,
  type ImportEndpoints,
  type ImportProgress,
  type ImportResult,
  type ImportRow,
} from '@/lib/streamingImport'

/** The list on screen stops here (a 20 000-row file with many problems must not freeze the page); the download holds all of them. */
export const MAX_ROWS_SHOWN = 500

export interface ImportDialogProps {
  open: boolean
  onClose: () => void
  /** i18n keys of the dialog title and of the column help (e.g. `import.products.title`). */
  titleKey: string
  columnsKey: string
  endpoints: ImportEndpoints
  templateFileName: string
  /** Called once an import ended (also a dry run, a cancelled or a stopped one): refresh the list behind the dialog. */
  onFinished?: (result: ImportResult) => void
  /** Test seam. */
  run?: typeof runStreamingImport
}

type Phase = 'idle' | 'running' | 'done'

const isProblem = (row: ImportRow) => row.status === 'skipped' || row.status === 'error'

function csvCell(value: string | number | null | undefined): string {
  const text = String(value ?? '')
  const safe = /^[=+\-@]/.test(text) ? `'${text}` : text   // a cell that starts like a formula is neutralised for Excel
  return /[",\r\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe
}

function download(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}

/**
 * One dialog for every streaming CSV import (products, employees): pick a file, optionally check it without saving, watch the progress
 * (a progress bar fed by Server-Sent Events) and the skipped / invalid rows as they are reported, cancel, and get a summary.
 */
export function ImportDialog({ open, onClose, titleKey, columnsKey, endpoints, templateFileName, onFinished, run = runStreamingImport }: ImportDialogProps) {
  const { t } = useTranslation()
  const [file, setFile] = useState<File | null>(null)
  const [dryRun, setDryRun] = useState(false)
  const [phase, setPhase] = useState<Phase>('idle')
  const [progress, setProgress] = useState<ImportProgress | null>(null)
  const [tally, setTally] = useState({ created: 0, skipped: 0, failed: 0, processed: 0 })
  const [shown, setShown] = useState<ImportRow[]>([])
  const [result, setResult] = useState<ImportResult | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const problems = useRef<ImportRow[]>([])
  const abort = useRef<AbortController | null>(null)

  useEffect(() => {
    if (!open) return
    setFile(null)
    setDryRun(false)
    setPhase('idle')
    setProgress(null)
    setTally({ created: 0, skipped: 0, failed: 0, processed: 0 })
    setShown([])
    setResult(null)
    setMessage(null)
    problems.current = []
  }, [open])

  const start = useCallback(async () => {
    if (!file) {
      setMessage(t('import.noFile'))
      return
    }
    setMessage(null)
    setPhase('running')
    problems.current = []
    setShown([])
    setProgress(null)
    setTally({ created: 0, skipped: 0, failed: 0, processed: 0 })
    const controller = new AbortController()
    abort.current = controller

    const outcome = await run({
      endpoints,
      file,
      dryRun,
      signal: controller.signal,
      fallbackError: t('import.failedGeneric'),
      callbacks: {
        onProgress: setProgress,
        onRow: (row) => {
          setTally((current) => ({
            processed: current.processed + 1,
            created: current.created + (row.status === 'created' || row.status === 'would-create' ? 1 : 0),
            skipped: current.skipped + (row.status === 'skipped' ? 1 : 0),
            failed: current.failed + (row.status === 'error' ? 1 : 0),
          }))
          if (!isProblem(row)) return
          problems.current.push(row)
          if (problems.current.length <= MAX_ROWS_SHOWN) setShown((current) => [...current, row])
        },
      },
    })

    abort.current = null
    setResult(outcome)
    setPhase('done')
    onFinished?.(outcome)
  }, [file, dryRun, endpoints, run, t, onFinished])

  const cancel = () => abort.current?.abort()

  const downloadTemplate = async () => {
    setMessage(null)
    try {
      const response = await authorizedFetch(endpoints.template)
      if (!response.ok) throw new Error(String(response.status))
      download(await response.blob(), templateFileName)
    } catch {
      setMessage(t('import.templateFailed'))
    }
  }

  const downloadProblems = () => {
    const lines = ['line,key,status,message', ...problems.current.map((r) => [r.line, r.key, r.status, r.message].map(csvCell).join(','))]
    download(new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' }), 'import-problems.csv')
  }

  const running = phase === 'running'
  // The server's progress snapshots are the source of the counters; the row lines fill in until the first one arrives.
  const counts = progress ? { processed: progress.processed, created: progress.created, skipped: progress.skipped, failed: progress.failed } : tally
  const summary = result?.summary

  return (
    <Modal
      open={open}
      title={t(titleKey)}
      onClose={onClose}
      size="lg"
      closeOnEsc={!running}
      footer={
        <>
          {running ? (
            <Button variant="secondary" onClick={cancel}>
              {t('import.cancel')}
            </Button>
          ) : (
            <Button variant="secondary" onClick={onClose}>
              {t('import.close')}
            </Button>
          )}
          {phase !== 'running' && (
            <Button onClick={start} disabled={!file}>
              <FileUp size={16} strokeWidth={1.75} aria-hidden="true" />
              {dryRun ? t('import.startDryRun') : t('import.start')}
            </Button>
          )}
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-secondary">{t(columnsKey)}</p>

        <div className="flex flex-wrap items-center gap-3">
          <label className="text-sm font-medium text-ink" htmlFor="import-file">
            {t('import.chooseFile')}
          </label>
          <input
            id="import-file"
            type="file"
            accept=".csv,text/csv"
            disabled={running}
            onChange={(event) => {
              setFile(event.target.files?.[0] ?? null)
              setMessage(null)
            }}
            className="text-sm text-ink file:mr-3 file:rounded-md file:border file:border-line-strong file:bg-surface file:px-3 file:py-1.5 file:text-sm file:text-ink"
          />
          <Button variant="ghost" size="sm" onClick={downloadTemplate} disabled={running}>
            <Download size={16} strokeWidth={1.75} aria-hidden="true" />
            {t('import.downloadTemplate')}
          </Button>
        </div>

        <Checkbox label={t('import.dryRun')} checked={dryRun} disabled={running} onChange={(event) => setDryRun(event.target.checked)} />

        {message && <Alert variant="danger">{message}</Alert>}

        {phase !== 'idle' && (
          <div className="space-y-3">
            <Progress label={t('import.progress')} value={phase === 'done' ? (result?.aborted ? progress?.percent : 100) : progress?.percent} />
            <dl className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
              <Counter label={t('import.processed')} value={counts.processed} />
              <Counter label={dryRun ? t('import.wouldCreate') : t('import.created')} value={counts.created} />
              <Counter label={t('import.skipped')} value={counts.skipped} />
              <Counter label={t('import.failed')} value={counts.failed} />
            </dl>
            {running && <p className="text-sm text-ink-muted">{t('import.running')}</p>}
          </div>
        )}

        <div aria-live="polite">
          {phase === 'done' && result?.aborted && <Alert variant="warning">{t('import.cancelled')}</Alert>}
          {phase === 'done' && result?.error && !result.aborted && <Alert variant="danger">{t('import.stopped', { message: result.error })}</Alert>}
          {phase === 'done' && summary && !result?.error && (
            <Alert variant={summary.failed > 0 || summary.skipped > 0 ? 'warning' : 'success'} title={summary.dryRun ? t('import.doneDryRun') : t('import.doneTitle')}>
              {t(summary.dryRun ? 'import.summaryDryRun' : 'import.summary', {
                processed: summary.processed,
                created: summary.created,
                skipped: summary.skipped,
                failed: summary.failed,
                seconds: (summary.elapsedMs / 1000).toFixed(1),
              })}
            </Alert>
          )}
        </div>

        {shown.length > 0 && (
          <section aria-label={t('import.rowsTitle')} className="space-y-2">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-sm font-medium text-ink">{t('import.rowsTitle')}</h3>
              {!running && (
                <Button variant="ghost" size="sm" onClick={downloadProblems}>
                  <Download size={16} strokeWidth={1.75} aria-hidden="true" />
                  {t('import.downloadProblems')}
                </Button>
              )}
            </div>
            <ul className="max-h-60 divide-y divide-line overflow-y-auto rounded-md border border-line text-sm">
              {shown.map((row) => (
                <li key={`${row.line}-${row.key ?? ''}`} className="flex flex-wrap gap-x-3 px-3 py-1.5">
                  <span className="text-ink-muted">{t('import.rowLine', { line: row.line })}</span>
                  <span className="font-medium text-ink">{row.key}</span>
                  <span className={row.status === 'error' ? 'text-danger-soft-text' : 'text-warning-soft-text'}>{row.message}</span>
                </li>
              ))}
            </ul>
            {problems.current.length > MAX_ROWS_SHOWN && <p className="text-xs text-ink-muted">{t('import.more', { count: MAX_ROWS_SHOWN })}</p>}
          </section>
        )}
      </div>
    </Modal>
  )
}

function Counter({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-md bg-surface-muted px-3 py-2">
      <dt className="text-xs text-ink-secondary">{label}</dt>
      <dd className="text-base font-semibold text-ink">{value}</dd>
    </div>
  )
}
