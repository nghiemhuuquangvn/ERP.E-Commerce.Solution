import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ReactElement } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The purchase order API files use the gateway client: both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { PurchaseOrderDetailPage } from './PurchaseOrderDetailPage'
import { PurchaseOrderListPage } from './PurchaseOrderListPage'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

function renderAt(ui: ReactElement, path = '/', route = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path={route} element={ui} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const emptyList = () => Object.assign([], { items: [], totalCount: 0, total: 0 })
function answer(rules: Array<[RegExp, unknown]>) {
  get.mockImplementation(((url: string) => {
    const hit = rules.find(([re]) => re.test(url))
    return Promise.resolve({ data: hit ? hit[1] : emptyList() })
  }) as never)
}

const ALL = ['purchase_order:approve', 'purchase_order:finance_approve', 'purchase_order:cancel', 'purchase_order:create', 'purchase_order:receive']

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: emptyList() })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'], permissions: ALL },
    isAuthenticated: true,
  })
})

const po = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'p1', poNumber: 'PO-1', supplierId: 's1', supplier: { id: 's1', name: 'Acme' }, status, orderDate: '2031-01-01T10:00:00Z', totalAmount: 1000, currency: 'USD', items: [], ...extra,
})
const paged = (items: unknown[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20 })

describe('Orders: purchase order list', () => {
  it('Approve says what happens and sends nothing before the confirmation', async () => {
    answer([[/purchase-orders/, paged([po('SUBMITTED')])]])
    renderAt(<PurchaseOrderListPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Approve PO-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/can no longer be edited/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Approve PO-1' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/purchase-orders/p1/approve')))
  })

  it('Finance approve asks too, and a refusal is reported with the server reason', async () => {
    answer([[/purchase-orders/, paged([po('PENDING_FINANCE_APPROVAL')])]])
    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'Over the approval limit.' } } })
    renderAt(<PurchaseOrderListPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Finance Approve PO-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Finance Approve' }))
    expect(await screen.findByText('Over the approval limit.')).toBeInTheDocument()
  })

  it('Reject needs a reason and is sent only then; the dialog is accessible', async () => {
    answer([[/purchase-orders/, paged([po('SUBMITTED')])]])
    renderAt(<PurchaseOrderListPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Reject PO-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject' }))
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
    await userEvent.type(within(dialog).getByRole('textbox'), 'too expensive')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject' }))
    await waitFor(() => expect(post).toHaveBeenCalled())
  })

  it('the status filters are a pressed-state group and an empty filter offers Clear', async () => {
    renderAt(<PurchaseOrderListPage />)
    expect(await screen.findByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(screen.getByRole('button', { name: 'Draft' }))
    expect(screen.getByRole('button', { name: 'Draft' })).toHaveAttribute('aria-pressed', 'true')
    expect(await screen.findByText('No purchase order has this status.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'true')
  })
})

describe('Orders: purchase order detail', () => {
  const line = { id: 'l1', productCode: 'BOX', productName: 'Box', quantity: 10, unit: 'pcs', unitPrice: 100, totalPrice: 1000, status: 'Open', receivedQuantity: 0, remainingQuantity: 10 }
  const detail = () => renderAt(<PurchaseOrderDetailPage />, '/po/p1', '/po/:id')

  it('Receive goods needs a quantity, refuses more than the remaining, and confirms before the stock moves', async () => {
    answer([[/purchase-orders\/p1$/, po('APPROVED', { items: [line] })]])
    detail()
    await userEvent.click(await screen.findByRole('button', { name: 'Receive goods' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    const record = within(dialog).getByRole('button', { name: 'Record receipt' })
    expect(record).toBeDisabled()

    const accepted = within(dialog).getByLabelText('Accepted: Box')
    await userEvent.type(accepted, '12')
    expect(within(dialog).getByText('More than the remaining quantity.')).toBeInTheDocument()
    expect(record).toBeDisabled()
    await userEvent.clear(accepted)
    await userEvent.type(accepted, '8')
    await userEvent.type(within(dialog).getByLabelText('Rejected: Box'), '2')
    expect(record).toBeEnabled()
    await userEvent.click(record)

    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/Add 8 accepted unit\(s\) to stock \(2 rejected\)/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Cancel' }))
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(record)
    const again = (await screen.findAllByRole('dialog')).slice(-1)[0]
    await userEvent.click(within(again).getByRole('button', { name: 'Receive into stock' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/purchase-orders/p1/receive'), expect.anything()))
  })
})

describe('Orders: translations', () => {
  it('every key the purchase order pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./PurchaseOrder{List,Detail}Page.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const src of Object.values(files)) {
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g), ...src.matchAll(/labelKey: '([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
