import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ReactElement } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// Both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { PaymentsPage } from './PaymentsPage'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

function renderPage(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const payment = (extra: Record<string, unknown> = {}) => ({
  id: 'abcdef12-0000-0000-0000-000000000000', orderId: 'o1', orderNumber: 'SO-1', amount: 500000, capturedAmount: 500000, currency: 'VND',
  method: 'Online', provider: 'VNPay', status: 'Captured', createdAt: '2031-01-01T10:00:00Z', ...extra,
})
const paged = (items: unknown[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20 })

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: paged([]) })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'], permissions: ['payments:refund'] },
    isAuthenticated: true,
  })
})

describe('Payments', () => {
  it('shows the empty state; the status filters are a pressed group and an empty filter offers Clear', async () => {
    renderPage(<PaymentsPage />)
    expect(await screen.findByText('No payments yet.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(screen.getByRole('button', { name: 'Failed' }))
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No payments yet.')).toBeInTheDocument()
  })

  it('Refund validates, then asks with the amount and the provider, and sends nothing before the confirmation', async () => {
    get.mockImplementation((() => Promise.resolve({ data: paged([payment()]) })) as never)
    renderPage(<PaymentsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Refund SO-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()

    // no reason: refused in the form, nothing asked
    await userEvent.click(within(dialog).getByRole('button', { name: 'Refund' }))
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(screen.getAllByRole('dialog')).toHaveLength(1)
    expect(post).not.toHaveBeenCalled()

    // an amount above the payment is refused too
    const amount = within(dialog).getByLabelText(/Amount to refund/)
    await userEvent.clear(amount)
    await userEvent.type(amount, '900000')
    await userEvent.type(within(dialog).getByLabelText(/Reason/), 'customer returned the goods')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Refund' }))
    expect(await within(dialog).findByText(/Enter an amount from 1 to/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    await userEvent.clear(amount)
    await userEvent.type(amount, '200000')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Refund' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/through VNPay/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Cancel' }))
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Refund' }))
    const again = (await screen.findAllByRole('dialog')).slice(-1)[0]
    await userEvent.click(within(again).getByRole('button', { name: 'Send refund' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/refund'), { amount: 200000, reason: 'customer returned the goods' }))
  })

  it('a cash-on-delivery refund says nothing is sent to a gateway, and a refusal shows the server reason', async () => {
    get.mockImplementation((() => Promise.resolve({ data: paged([payment({ provider: 'COD', method: 'Cash' })]) })) as never)
    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'Refund exceeds the captured amount.' } } })
    renderPage(<PaymentsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Refund SO-1' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Reason/), 'wrong item')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Refund' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/nothing is sent to a gateway/)).toBeInTheDocument()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Send refund' }))
    expect(await within(dialog).findByText('Refund exceeds the captured amount.')).toBeInTheDocument()
  })
})

describe('Payments: translations', () => {
  it('every key the page uses exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./PaymentsPage.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
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
