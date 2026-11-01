import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ComponentType, ReactElement } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The warehouse API files use the gateway client, the inventory list the plain one: both are the same fake here.
// Every GET answers an empty list that works as both a paged result and a plain array.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { BarcodeResolvePage } from './BarcodeResolvePage'
import { InventoryDetailPage } from './InventoryDetailPage'
import { InventoryListPage } from './InventoryListPage'
import { InventoryValuationSnapshotsPage } from './InventoryValuationSnapshotsPage'
import { LocationsPage } from './LocationsPage'
import { StockAdjustmentsPage } from './StockAdjustmentsPage'
import { StocktakeDetailPage } from './StocktakeDetailPage'
import { StocktakeListPage } from './StocktakeListPage'
import { TransferOrderFormPage } from './TransferOrderFormPage'
import { TransferOrderListPage } from './TransferOrderListPage'
import { WarehousesPage } from './WarehousesPage'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

function renderAt(ui: ReactElement, path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}
const page = (Page: ComponentType) => <Page />

const emptyList = () => Object.assign([], { items: [], totalCount: 0, total: 0 })
/** Answer by url: the first matching rule wins, everything else is an empty list. */
function answer(rules: Array<[RegExp, unknown]>) {
  get.mockImplementation(((url: string) => {
    const hit = rules.find(([re]) => re.test(url))
    return Promise.resolve({ data: hit ? hit[1] : emptyList() })
  }) as never)
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: emptyList() })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.c', tenantId: 't1', roles: ['Admin'], permissions: [] },
    isAuthenticated: true,
  })
})

const submitOf = (dialog: HTMLElement) => within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!
const addButton = () => screen.findByRole('button', { name: /^\+/ })

describe.each([
  ['Warehouses', WarehousesPage],
  ['Locations', LocationsPage],
] as Array<[string, ComponentType]>)('Inventory: %s', (_n, Page) => {
  it('shows the empty state, opens the add dialog, shows linked required errors, is accessible, and closes with Esc', async () => {
    renderAt(page(Page))
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
    await userEvent.click(await addButton())
    const dialog = await screen.findByRole('dialog')
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })
})

describe('Inventory: stock list', () => {
  it('shows the empty state and offers Clear when a search hides everything', async () => {
    renderAt(page(InventoryListPage))
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
    await userEvent.type(screen.getByRole('textbox'), 'zzz')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
  })
})

describe('Inventory: adjustments', () => {
  const adj = { id: 'a1', productId: 'p-1', quantityDelta: -3, reason: 'Damaged', requestedBy: 'x', requestedAt: '2031-01-01T10:00:00Z', status: 'Pending', approvedBy: null, approvedAt: null, rejectionReason: null }

  it('Reject needs a written reason and is sent only then', async () => {
    answer([[/stock-adjustments/, [adj]]])
    renderAt(page(StockAdjustmentsPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
    await userEvent.type(within(dialog).getByRole('textbox'), 'counted wrong')
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/stock-adjustments/a1/reject'), { reason: 'counted wrong' }))
  })

  it('the status buttons are a pressed-state group and the empty filter offers Clear', async () => {
    renderAt(page(StockAdjustmentsPage))
    expect(await screen.findByRole('button', { name: 'Pending' })).toHaveAttribute('aria-pressed', 'true')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'true')
  })
})

describe('Inventory: product stock detail', () => {
  const inventory = {
    productId: 'p-1', productName: 'Widget', totalQuantity: 10, availableQuantity: 8, reservedQuantity: 2, lockedQuantity: 0, reorderPoint: 3, reorderQuantity: 5,
    minStock: 2, maxStock: 20, safetyStock: 1, isLowStock: false, locations: [{ location: 'A-1', quantity: 10, reserved: 2 }], lastUpdated: '2031-01-01T00:00:00Z',
  }
  const detail = () => (
    <Routes>
      <Route path="/inventory/:productId" element={<InventoryDetailPage />} />
    </Routes>
  )

  it('shows the figures and the tabs as a tablist, and the adjustment request is accessible and needs a non-zero quantity', async () => {
    answer([[/inventory\/p-1$/, inventory]])
    renderAt(detail(), '/inventory/p-1')
    expect(await screen.findByText('Widget')).toBeInTheDocument()
    expect(screen.getAllByRole('tab').length).toBe(4)
    await userEvent.click(await screen.findByRole('button', { name: 'Request Adjustment' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
  })
})

describe('Inventory: stocktake', () => {
  const stocktake = (status: string) => ({
    stocktakeId: 's1', stocktakeNumber: 'ST-1', locationId: null, status, notes: null, startedBy: 'x', startedAt: '2031-01-01T00:00:00Z', completedAt: null,
    lines: [{ productId: 'p-1', systemQuantity: 10, countedQuantity: 7, variance: -3, varianceReason: null }],
  })
  const detail = () => (
    <Routes>
      <Route path="/inventory/stocktakes/:id" element={<StocktakeDetailPage />} />
    </Routes>
  )

  it.each([
    ['InProgress', 'Complete Count', '/stocktakes/s1/complete'],
    ['PendingApproval', 'Approve Variance', '/stocktakes/s1/approve'],
  ])('%s: "%s" asks first and calls the API only after confirming', async (status, label, url) => {
    answer([[/stocktakes\/s1$/, stocktake(status)]])
    renderAt(detail(), '/inventory/stocktakes/s1')
    await userEvent.click(await screen.findByRole('button', { name: label }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: label }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining(url)))
  })

  it('Cancel asks first, a shortage is shown with its sign, and a completed count has no actions', async () => {
    answer([[/stocktakes\/s1$/, stocktake('InProgress')]])
    const { unmount } = renderAt(detail(), '/inventory/stocktakes/s1')
    expect(await screen.findByText('-3')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Cancel Stocktake' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()
    unmount()

    answer([[/stocktakes\/s1$/, stocktake('Completed')]])
    renderAt(detail(), '/inventory/stocktakes/s1')
    await screen.findByText('ST-1')
    expect(screen.queryByRole('button', { name: 'Cancel Stocktake' })).toBeNull()
  })

  it('the list shows the start hint', async () => {
    renderAt(page(StocktakeListPage))
    expect(await screen.findByText('Counts the whole warehouse.')).toBeInTheDocument()
  })
})

describe('Inventory: transfers', () => {
  const order = (status: string) => ({
    transferOrderId: 't1', transferNumber: 'TO-1', fromLocationId: 'l1', toLocationId: 'l2', status, notes: null, createdAt: '2031-01-01T00:00:00Z', shippedAt: null, completedAt: null,
    lines: [{ productId: 'p-1', quantity: 2 }],
  })

  it.each([
    ['Draft', 'Ship', 'Ship the transfer', '/transfer-orders/t1/ship'],
    ['Draft', 'Cancel', 'Cancel the transfer', '/transfer-orders/t1/cancel'],
    ['InTransit', 'Complete', 'Receive the transfer', '/transfer-orders/t1/complete'],
  ])('%s: "%s" asks first and calls the API only after confirming', async (status, row, confirm, url) => {
    answer([[/transfer-orders/, [order(status)]]])
    renderAt(page(TransferOrderListPage))
    await userEvent.click(await screen.findByRole('button', { name: row }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: confirm }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining(url)))
  })

  it('the new-transfer form refuses an empty form with linked errors and the same source and destination', async () => {
    answer([[/locations/, [{ locationId: 'l1', locationCode: 'A-1', isActive: true }, { locationId: 'l2', locationCode: 'B-1', isActive: true }]]])
    const { container } = renderAt(page(TransferOrderFormPage))
    await userEvent.click(await screen.findByRole('button', { name: 'Save' }))
    await waitFor(() => expect(container.querySelectorAll('[aria-invalid="true"]').length).toBe(2))
    expect(await screen.findByText('Please add at least one line item.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    const [from, to] = screen.getAllByRole('combobox')
    await userEvent.selectOptions(from, 'l1')
    await userEvent.selectOptions(to, 'l1')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Source and destination locations must differ.')).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Inventory: valuation and barcode', () => {
  it('Close period explains what it does and calls the API only after confirming', async () => {
    renderAt(page(InventoryValuationSnapshotsPage))
    await userEvent.type((await screen.findAllByLabelText(/Period Code/i))[0], '2031-01')
    await userEvent.click(screen.getByRole('button', { name: 'Close Period' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/locked/i)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Close Period' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('valuation'), expect.objectContaining({ periodCode: '2031-01' })))
  })

  it('the barcode page is accessible', async () => {
    const { container } = renderAt(page(BarcodeResolvePage))
    expect(await screen.findByRole('heading', { name: 'Barcode' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Inventory: translations', () => {
  it('every key the Inventory pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const [file, src] of Object.entries(files)) {
      if (file.endsWith('.test.tsx')) continue
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g), ...src.matchAll(/\bt\(`([a-zA-Z0-9_.]+)\$\{/g)].map((m) => m[1])
      for (const k of keys) {
        if (k.endsWith('.')) continue // a dynamic suffix: checked below
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    // the dynamic keys of the transfer confirm dialog
    for (const k of ['ship', 'complete', 'cancel']) {
      for (const part of ['Title', 'Confirm']) {
        const key = `inventory.transfers.${k}${part}`
        if (typeof lookup(en, key) !== 'string') missing.push(`en ${key}`)
        if (typeof lookup(vi, key) !== 'string') missing.push(`vi ${key}`)
      }
    }
    expect(missing).toEqual([])
  })
})
