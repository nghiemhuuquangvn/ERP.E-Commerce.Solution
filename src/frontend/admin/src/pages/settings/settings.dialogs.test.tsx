import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ReactElement } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The shipping and notification API files use the gateway client: both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { CarrierRulesCard } from './CarrierRulesCard'
import { NotificationHistoryPage } from './NotificationHistoryPage'
import { ShippingFeeRulesCard } from './ShippingFeeRulesCard'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)
const put = vi.mocked(apiClient.put)
const del = vi.mocked(apiClient.delete)

function renderAt(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
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

beforeEach(() => {
  for (const m of [get, post, put, del]) m.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: emptyList() })) as never)
  for (const m of [post, put, del]) m.mockImplementation((() => Promise.resolve({ data: {} })) as never)
})

const submitOf = (dialog: HTMLElement) => within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!

const carrierRule = { id: 'c1', name: 'North only', priority: 10, isActive: true, matchProvinces: 'Hà Nội', carrierCode: 'GHN' }
const feeRule = { id: 'f1', name: 'Local', priority: 10, isActive: true, matchProvinces: 'Hà Nội', fee: 20000 }

describe.each([
  ['carrier rules', () => <CarrierRulesCard canManage />, /carriers\/rules/, carrierRule, 'Add rule'],
  ['fee rules', () => <ShippingFeeRulesCard canManage />, /fee-rules/, feeRule, 'Add fee row'],
] as Array<[string, () => ReactElement, RegExp, unknown, string]>)('Settings: %s', (_n, ui, url, row, addName) => {
  it('shows the empty state with the add action, validates the form with linked errors, and closes with Esc', async () => {
    renderAt(ui())
    expect((await screen.findAllByRole('button', { name: addName })).length).toBeGreaterThan(0)
    await userEvent.click(screen.getAllByRole('button', { name: addName })[0])
    const dialog = await screen.findByRole('dialog')
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('a weight range with the minimum above the maximum is refused before anything is sent', async () => {
    renderAt(ui())
    await userEvent.click((await screen.findAllByRole('button', { name: addName }))[0])
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByRole('textbox', { name: /^Name/ }), 'Heavy')
    await userEvent.type(within(dialog).getByLabelText(/Min weight|From weight/), '900')
    await userEvent.type(within(dialog).getByLabelText(/Max weight|Up to weight/), '100')
    await userEvent.click(submitOf(dialog))
    expect(await within(dialog).findByText('The minimum must not be above the maximum.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('Delete asks first, says new orders stop using it, and sends nothing until confirmed', async () => {
    answer([[url, [row]]])
    renderAt(ui())
    await userEvent.click(await screen.findByRole('button', { name: /^Delete / }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/New orders/)).toBeInTheDocument()
    expect(del).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(del).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: /^Delete / }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(del).toHaveBeenCalledTimes(1))
  })

  it('a refused Delete is reported with the server reason', async () => {
    answer([[url, [row]]])
    del.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'The rule is in use.' } } })
    renderAt(ui())
    await userEvent.click(await screen.findByRole('button', { name: /^Delete / }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }))
    expect(await screen.findByText('The rule is in use.')).toBeInTheDocument()
  })
})

describe('Settings: notification history', () => {
  const sent = {
    id: 'n1', recipientId: 'x', recipientEmail: 'buyer@shop.test', channel: 'Email', subject: 'Order shipped', body: 'Your parcel is on its way.',
    status: 'Failed', priority: 'Normal', retryCount: 2, errorMessage: 'Mailbox full', sentAt: null, createdAt: '2031-01-01T10:00:00Z',
    logs: [{ eventType: 'Attempt', details: 'SMTP 552', eventTimestamp: '2031-01-01T10:00:05Z' }],
  }

  it('opens the detail dialog (message, error with retries, log) and closes with Esc', async () => {
    answer([[/notification-history/, { total: 1, items: [sent] }]])
    renderAt(<NotificationHistoryPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'View: Order shipped' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Your parcel is on its way.')).toBeInTheDocument()
    expect(within(dialog).getByText(/Mailbox full/)).toBeInTheDocument()
    expect(within(dialog).getByText(/SMTP 552/)).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('shows the empty state, and a search with no match offers Clear', async () => {
    renderAt(<NotificationHistoryPage />)
    expect(await screen.findByText('No notifications were sent yet.')).toBeInTheDocument()
    await userEvent.type(screen.getByRole('searchbox'), 'zzz')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No notifications were sent yet.')).toBeInTheDocument()
  })
})

describe('Settings: translations', () => {
  it('every key the migrated Settings files use exists in English and Vietnamese', async () => {
    const all = import.meta.glob('./*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const mine = ['./CarrierRulesCard.tsx', './ShippingFeeRulesCard.tsx', './NotificationHistoryPage.tsx']
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const file of mine) {
      const keys = [...all[file].matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
