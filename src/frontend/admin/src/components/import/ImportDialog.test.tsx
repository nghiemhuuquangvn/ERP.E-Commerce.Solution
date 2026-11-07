import { describe, expect, it, vi } from 'vitest'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'
import { ImportDialog, MAX_ROWS_SHOWN } from './ImportDialog'
import type { ImportCallbacks, ImportEndpoints, ImportResult, ImportRow } from '@/lib/streamingImport'

const endpoints: ImportEndpoints = {
  upload: '/prd/api/v1/products/import',
  events: (id) => `/prd/api/v1/products/import/${id}/events`,
  template: '/prd/api/v1/products/import/template',
}

/** A stand-in for the runner that the test drives by hand: it stays "running" until finish() is called or the dialog aborts it. */
function controllable() {
  let callbacks: ImportCallbacks = {}
  let resolve: (result: ImportResult) => void = () => {}
  const run = vi.fn((options: { callbacks?: ImportCallbacks; signal?: AbortSignal }) => {
    callbacks = options.callbacks ?? {}
    return new Promise<ImportResult>((r) => {
      resolve = r
      options.signal?.addEventListener('abort', () => r({ aborted: true }))
    })
  })
  return {
    run: run as never,
    calls: run,
    emit: (fn: (c: ImportCallbacks) => void) => act(async () => fn(callbacks)),
    finish: (result: ImportResult) => act(async () => resolve(result)),
  }
}

const row = (line: number, status: ImportRow['status'], message: string | null = null): ImportRow => ({ line, key: `K-${line}`, status, message })

function setup(extra: Partial<React.ComponentProps<typeof ImportDialog>> = {}) {
  const driver = controllable()
  const onFinished = vi.fn()
  const onClose = vi.fn()
  const utils = render(
    <ImportDialog
      open onClose={onClose} titleKey="import.products.title" columnsKey="import.products.columns" endpoints={endpoints}
      templateFileName="products-import-template.csv" onFinished={onFinished} run={driver.run} {...extra}
    />,
  )
  return { driver, onFinished, onClose, ...utils }
}

const csv = () => new File(['sku,name\nA,One\n'], 'products.csv', { type: 'text/csv' })

async function chooseFile() {
  await userEvent.upload(screen.getByLabelText('CSV file (UTF-8)'), csv())
}

describe('ImportDialog', () => {
  it('shows the title and the column help, and cannot start without a file', () => {
    setup()
    expect(screen.getByRole('dialog', { name: 'Import products' })).toBeInTheDocument()
    expect(screen.getByText(/Required: sku, name/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Start import/ })).toBeDisabled()
  })

  it('runs the import with the chosen file, shows progress and counters from the server, and ends with a summary', async () => {
    const { driver, onFinished } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))

    expect(driver.calls).toHaveBeenCalledTimes(1)
    const options = driver.calls.mock.calls[0][0] as unknown as { file: File; dryRun: boolean; endpoints: ImportEndpoints }
    expect(options.file.name).toBe('products.csv')
    expect(options.dryRun).toBe(false)
    expect(screen.getByText('Importing…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancel import' })).toBeInTheDocument()

    await driver.emit((c) => c.onProgress?.({ state: 'running', processed: 40, created: 38, skipped: 1, failed: 1, bytesRead: 40, totalBytes: 100, percent: 40, elapsedMs: 900 }))
    expect(screen.getByRole('progressbar', { name: 'Import progress' })).toHaveAttribute('aria-valuenow', '40')
    expect(within(screen.getByText('Processed').parentElement!).getByText('40')).toBeInTheDocument()

    await driver.finish({ summary: { processed: 100, created: 97, skipped: 2, failed: 1, dryRun: false, elapsedMs: 4200 } })
    expect(screen.getByText('Import finished')).toBeInTheDocument()
    expect(screen.getByText('100 rows: 97 created, 2 skipped, 1 failed (4.2 s).')).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Import progress' })).toHaveAttribute('aria-valuenow', '100')
    expect(onFinished).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('button', { name: 'Cancel import' })).toBeNull()
  })

  it('lists the skipped and invalid rows as they arrive, with their line and reason, and offers the download when it is over', async () => {
    const { driver } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))

    await driver.emit((c) => {
      c.onRow?.(row(2, 'created'))
      c.onRow?.(row(3, 'skipped', 'A product with this SKU already exists.'))
      c.onRow?.(row(4, 'error', 'The name must be 3 to 255 characters.'))
    })

    const list = screen.getByRole('region', { name: 'Skipped and invalid rows' })
    expect(within(list).getAllByRole('listitem')).toHaveLength(2)
    expect(within(list).getByText('Line 3')).toBeInTheDocument()
    expect(within(list).getByText('The name must be 3 to 255 characters.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Download problem rows (CSV)' })).toBeNull()   // not while it runs

    await driver.finish({ summary: { processed: 3, created: 1, skipped: 1, failed: 1, dryRun: false, elapsedMs: 100 } })
    expect(screen.getByRole('button', { name: 'Download problem rows (CSV)' })).toBeInTheDocument()
  })

  it('keeps the list at the cap and says so', async () => {
    const { driver } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))

    await driver.emit((c) => {
      for (let i = 0; i < MAX_ROWS_SHOWN + 25; i++) c.onRow?.(row(i + 2, 'error', 'bad'))
    })
    await driver.finish({ summary: { processed: 525, created: 0, skipped: 0, failed: 525, dryRun: false, elapsedMs: 100 } })

    expect(within(screen.getByRole('region', { name: 'Skipped and invalid rows' })).getAllByRole('listitem')).toHaveLength(MAX_ROWS_SHOWN)
    expect(screen.getByText(`Showing the first ${MAX_ROWS_SHOWN} rows; download the full list.`)).toBeInTheDocument()
  })

  it('a check-only run says "would be created" and writes a different summary', async () => {
    const { driver } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('checkbox', { name: 'Check only (nothing is saved)' }))
    await userEvent.click(screen.getByRole('button', { name: 'Start check' }))

    expect((driver.calls.mock.calls[0][0] as unknown as { dryRun: boolean }).dryRun).toBe(true)
    expect(screen.getByText('Would be created')).toBeInTheDocument()
    await driver.finish({ summary: { processed: 10, created: 9, skipped: 0, failed: 1, dryRun: true, elapsedMs: 50 } })
    expect(screen.getByText('Check finished: nothing was saved.')).toBeInTheDocument()
    expect(screen.getByText('10 rows: 9 would be created, 0 skipped, 1 invalid (0.1 s).')).toBeInTheDocument()
  })

  it('cancel aborts the run and says the saved rows stay saved', async () => {
    const { driver } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))

    await userEvent.click(screen.getByRole('button', { name: 'Cancel import' }))

    expect(await screen.findByText('The import was cancelled. Rows already saved stay saved.')).toBeInTheDocument()
    expect(driver.calls).toHaveBeenCalledTimes(1)
  })

  it('shows the reason when the import was refused or stopped', async () => {
    const { driver } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))

    await driver.finish({ error: 'The header lacks the required column(s): sku.' })

    expect(screen.getByRole('alert')).toHaveTextContent('The import stopped: The header lacks the required column(s): sku.')
  })

  it('cannot be closed with Esc while it runs, and Close works afterwards', async () => {
    const { driver, onClose } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))

    await userEvent.keyboard('{Escape}')
    expect(onClose).not.toHaveBeenCalled()

    await driver.finish({ summary: { processed: 1, created: 1, skipped: 0, failed: 0, dryRun: false, elapsedMs: 1 } })
    const closeButtons = screen.getAllByRole('button', { name: 'Close' })   // the modal's own X and the footer button
    expect(closeButtons).toHaveLength(2)
    await userEvent.click(closeButtons[closeButtons.length - 1])
    expect(onClose).toHaveBeenCalled()
  })

  it('has no accessibility violations while running and when finished', async () => {
    const { driver } = setup()
    await chooseFile()
    await userEvent.click(screen.getByRole('button', { name: /Start import/ }))
    await driver.emit((c) => {
      c.onProgress?.({ state: 'running', processed: 3, created: 2, skipped: 1, failed: 0, bytesRead: 3, totalBytes: 10, percent: 30, elapsedMs: 10 })
      c.onRow?.(row(3, 'skipped', 'exists'))
    })
    expect(await axe(document.body)).toHaveNoViolations()

    await driver.finish({ summary: { processed: 3, created: 2, skipped: 1, failed: 0, dryRun: false, elapsedMs: 10 } })
    expect(await axe(document.body)).toHaveNoViolations()
  })
})
