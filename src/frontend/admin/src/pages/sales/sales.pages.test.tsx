import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The commission API goes through soClient (the gateway client): both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: [] })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { CommissionPlansPage } from './CommissionPlansPage'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>
          <CommissionPlansPage />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: [] })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'], permissions: ['sales_order:approve'] },
    isAuthenticated: true,
  })
})

describe('Sales: commission plans', () => {
  it('shows the empty state with the add action; the dialog validates, is accessible and closes with Esc', async () => {
    renderPage()
    expect(await screen.findByText('No commission plan yet.')).toBeInTheDocument()
    await userEvent.click(screen.getAllByRole('button', { name: '+ Add Plan' })[0])
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.clear(within(dialog).getByLabelText(/Name/))
    await userEvent.clear(within(dialog).getByLabelText(/Rate/))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const c of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(c.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('a rate above 100 is refused; a valid plan is sent as a fraction', async () => {
    renderPage()
    await userEvent.click((await screen.findAllByRole('button', { name: '+ Add Plan' }))[0])
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Name/), 'Gold')
    const rate = within(dialog).getByLabelText(/Rate/)
    await userEvent.clear(rate)
    await userEvent.type(rate, '150')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('Enter a rate above 0 and up to 100.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.clear(rate)
    await userEvent.type(rate, '7.5')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/commission-plans'), { name: 'Gold', rateValue: 0.075 }))
  })

  it('Deactivate asks first, and a refusal is reported with the server reason', async () => {
    get.mockImplementation((() => Promise.resolve({ data: [{ id: 'p1', name: 'Gold', rateValue: 0.05, isActive: true }] })) as never)
    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'The plan is in use.' } } })
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Deactivate Gold' }))
    const confirm = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Deactivate' }))
    expect(await screen.findByText('The plan is in use.')).toBeInTheDocument()
  })
})

describe('Sales: translations', () => {
  it('every key the page uses exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./CommissionPlansPage.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const src of Object.values(files)) {
      for (const k of [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
