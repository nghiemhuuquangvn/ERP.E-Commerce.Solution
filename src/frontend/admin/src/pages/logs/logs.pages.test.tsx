import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// Both clients are the same fake here.
vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { LogsPage } from './LogsPage'

const get = vi.mocked(apiClient.get)

function renderPage(path = '/logs') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/dashboard" element={<div>dashboard</div>} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const entry = {
  id: 'l1', serviceName: 'Gateway', environment: 'prod', level: 'Error', kind: 'Exception', message: 'Boom in the gateway', exceptionType: 'System.Exception',
  stackTrace: 'at A.B()\nat C.D()', correlationId: 'req-1', occurredAt: '2031-01-01T10:00:00Z', properties: { Path: '/api/orders' },
}

const allow = (permissions: string[]) =>
  useAuthStore.setState({ user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: [], permissions }, isAuthenticated: true })

beforeEach(() => {
  get.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: { items: [], totalCount: 0, page: 1, pageSize: 25 } })) as never)
  allow(['logs:view'])
})

describe('Logs', () => {
  it('sends a user without logs:view to the dashboard', async () => {
    allow([])
    renderPage()
    expect(await screen.findByText('dashboard')).toBeInTheDocument()
  })

  it('opens the detail as a dialog from a keyboard-reachable View button and closes it with Esc', async () => {
    get.mockImplementation((() => Promise.resolve({ data: { items: [entry], totalCount: 1, page: 1, pageSize: 25 } })) as never)
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: /View: Boom in the gateway/ }))
    const dialog = await screen.findByRole('dialog')
    expect(document.body.style.overflow).toBe('hidden')
    expect(within(dialog).getByText('System.Exception')).toBeInTheDocument()
    expect(within(dialog).getByText(/at A\.B\(\)/)).toBeInTheDocument()
    expect(within(dialog).getByText('/api/orders')).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('the filters are labelled, live in the URL query, and a search with no result offers Clear', async () => {
    renderPage('/logs?level=Error&keyword=zzz')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    expect(screen.getByLabelText('Level')).toHaveValue('Error')
    expect(screen.getByLabelText('From')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    await waitFor(() => expect(screen.getByLabelText('Level')).toHaveValue(''))
    expect(get).toHaveBeenLastCalledWith('/v1/logs', { params: { page: 1, pageSize: 25 } })
  })
})

describe('Logs: translations', () => {
  it('every key the page uses exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./LogsPage.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
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
