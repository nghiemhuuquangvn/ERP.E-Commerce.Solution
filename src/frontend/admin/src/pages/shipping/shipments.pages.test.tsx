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

import { ShipmentsPage } from './ShipmentsPage'

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

const shipment = (extra: Record<string, unknown> = {}) => ({
  id: 's1', orderId: 'o1', carrierId: 'c1', carrierName: 'GHN', trackingNumber: 'GHN123', status: 'Pending', recipientName: 'An', recipientPhone: '090',
  addressStreet: '1 Main', addressDistrict: 'D1', addressProvince: 'HCM', totalWeight: 1, codAmount: 100000, declaredValue: 100000, carrierFee: 20000,
  createdAt: '2031-01-01T10:00:00Z', trackingEvents: [], parcelNo: 1, parcelCount: 1,
  items: [{ productId: 'p1', productName: 'Shirt', quantity: 2, weight: 0.5 }, { productId: 'p2', productName: 'Hat', quantity: 1, weight: 0.2 }],
  ...extra,
})
const list = (items: unknown[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20 })

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: list([]) })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'], permissions: ['shipping:update'] },
    isAuthenticated: true,
  })
})

const open = async (extra: Record<string, unknown> = {}) => {
  get.mockImplementation((() => Promise.resolve({ data: list([shipment(extra)]) })) as never)
  renderPage(<ShipmentsPage />)
  await userEvent.click(await screen.findByRole('button', { name: /^View (GHN123|GHN)$/ }))
  return await screen.findByRole('dialog')
}

describe('Shipments', () => {
  it('shows the empty state; a filter with no result offers Clear', async () => {
    renderPage(<ShipmentsPage />)
    expect(await screen.findByText('No shipments yet.')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'Delivered')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No shipments yet.')).toBeInTheDocument()
  })

  it('the detail dialog is accessible, shows the review note and closes with Esc', async () => {
    const dialog = await open({ reviewNote: 'Cash does not match', bookingError: 'Carrier timeout' })
    expect(within(dialog).getByText('Cash does not match')).toBeInTheDocument()
    expect(within(dialog).getByText('Carrier timeout')).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('Cancel at the carrier asks first, names the carrier and sends nothing until confirmed', async () => {
    const dialog = await open()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel at the carrier' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/Cancel this parcel at GHN/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.getAllByRole('dialog')).toHaveLength(1))
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel at the carrier' }))
    const again = (await screen.findAllByRole('dialog')).slice(-1)[0]
    await userEvent.click(within(again).getByRole('button', { name: 'Cancel at the carrier' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/shipments/s1/cancel-at-carrier'), expect.anything()))
  })

  it('Re-book asks first and a refusal is shown as a toast', async () => {
    const dialog = await open({ trackingNumber: null })
    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'No carrier accepts this parcel.' } } })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Book with a carrier again' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Book again' }))
    expect(await screen.findByText('No carrier accepts this parcel.')).toBeInTheDocument()
  })

  it('the split grid names every unit that is not assigned, blocks the button, and confirms before it books', async () => {
    const dialog = await open()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Split into parcels' }))
    const split = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(await axe(split)).toHaveNoViolations()
    const go = within(split).getByRole('button', { name: 'Split and book' })
    // the first parcel starts with everything, the second is empty: every parcel needs a unit
    expect(within(split).getByText('Every parcel needs at least one unit.')).toBeInTheDocument()
    expect(go).toBeDisabled()

    const shirt1 = within(split).getByLabelText('Shirt: parcel 1')
    await userEvent.clear(shirt1)
    await userEvent.type(shirt1, '1')
    expect(within(split).getByText('1 unit(s) are not assigned to a parcel yet.')).toBeInTheDocument()
    await userEvent.clear(within(split).getByLabelText('Shirt: parcel 2'))
    await userEvent.type(within(split).getByLabelText('Shirt: parcel 2'), '1')
    expect(within(split).getByText('Every item is assigned.')).toBeInTheDocument()
    expect(go).toBeEnabled()

    await userEvent.click(go)
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/Split into 2 parcels/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Cancel' }))
    expect(post).not.toHaveBeenCalled()
  })
})

describe('Shipments: translations', () => {
  it('every key the page uses exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./ShipmentsPage.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const src of Object.values(files)) {
      for (const k of [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
      // the status filter labels are built from the status name
      for (const s of ['Pending', 'ReadyForPickup', 'PickedUp', 'InTransit', 'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Returned', 'Cancelled']) {
        if (typeof lookup(en, `shipments.status.${s}`) !== 'string') missing.push(`en status ${s}`)
        if (typeof lookup(vi, `shipments.status.${s}`) !== 'string') missing.push(`vi status ${s}`)
      }
    }
    expect(missing).toEqual([])
  })
})
