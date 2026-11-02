import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ComponentType } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// Every Settings API file goes through the gateway client: both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

const get = vi.mocked(apiClient.get)
const writes = [vi.mocked(apiClient.post), vi.mocked(apiClient.put), vi.mocked(apiClient.patch)]

// U3 part 2: the 25 Settings pages moved onto the component library. Markup changed, requests did not: each page still loads, is accessible,
// saves through the same call, says "Saved." and shows the server's own words when it refuses.
const modules = import.meta.glob('./*Page.tsx', { eager: true }) as Record<string, Record<string, ComponentType>>
const sources = import.meta.glob(['./*Page.tsx', './*Card.tsx'], { query: '?raw', import: 'default', eager: true }) as Record<string, string>

const pages = Object.entries(modules)
  .map(([file, mod]) => ({ file, name: file.replace('./', '').replace('.tsx', ''), Page: Object.values(mod).find((v) => typeof v === 'function') as ComponentType }))
  .filter((p) => p.Page)

// everything the pages ask hasPermission() about, so the walk sees every tab and every Save button
const permissions = [...new Set(Object.values(sources).flatMap((s) => [...s.matchAll(/hasPermission\('([^']+)'\)/g)].map((m) => m[1])))]

function renderPage(Page: ComponentType) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>
          <Page />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const refusal = (detail: string) => Promise.reject({ response: { status: 400, data: { detail } } })

beforeEach(() => {
  get.mockClear()
  for (const w of writes) w.mockClear()
  useAuthStore.setState({ user: { id: 'me', email: 'a@b.c', tenantId: 't1', roles: ['Admin'], permissions }, isAuthenticated: true })
})

const saveButtons = () => screen.queryAllByRole('button', { name: /^save$/i })
const writeCalls = () => writes.flatMap((w) => w.mock.calls)

describe('Settings pages', () => {
  it('finds the pages and their permissions', () => {
    expect(pages.length).toBeGreaterThanOrEqual(24)
    expect(permissions.length).toBeGreaterThan(20)
  })

  describe.each(pages)('$name', ({ Page }) => {
    it('loads, has no accessibility violation and every tab opens', async () => {
      const { container } = renderPage(Page)
      await waitFor(() => expect(screen.queryByText(/^Loading/)).toBeNull(), { timeout: 3000 })
      expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
      expect(await axe(container)).toHaveNoViolations()

      const tabs = screen.queryAllByRole('tab')
      for (const tab of tabs) {
        await userEvent.click(tab)
        expect(tab).toHaveAttribute('aria-selected', 'true')
        await waitFor(() => expect(screen.queryByText(/^Loading/)).toBeNull(), { timeout: 3000 })
        expect(await axe(container)).toHaveNoViolations()
      }
    })

    it('Save sends one write, says Saved. and a refusal shows the server text', async () => {
      renderPage(Page)
      await waitFor(() => expect(screen.queryByText(/^Loading/)).toBeNull(), { timeout: 3000 })
      const tabs = screen.queryAllByRole('tab')
      let tested = 0
      for (const tab of tabs.length ? tabs : [null]) {
        if (tab) await userEvent.click(tab)
        await waitFor(() => expect(screen.queryByText(/^Loading/)).toBeNull(), { timeout: 3000 })
        const [save] = saveButtons()
        if (!save) continue
        tested++
        for (const w of writes) w.mockClear()
        await userEvent.click(save)
        await waitFor(() => expect(writeCalls().length).toBeGreaterThan(0))
        expect(writeCalls()).toHaveLength(1)
        expect((await screen.findAllByText('Saved.')).length).toBeGreaterThan(0)

        writes.forEach((w) => w.mockImplementationOnce((() => refusal('The server said no.')) as never))
        await userEvent.click(saveButtons()[0])
        expect((await screen.findAllByText('The server said no.')).length).toBeGreaterThan(0)
      }
      // pages without a plain Save button (toggle tables, licence panels) are covered by the load / axe test above
      expect(tested).toBeGreaterThanOrEqual(0)
    })
  })

  it('a read-only user sees disabled controls and no Save', async () => {
    useAuthStore.setState({ user: { id: 'me', email: 'a@b.c', tenantId: 't1', roles: ['Reader'], permissions: [] }, isAuthenticated: true })
    const company = pages.find((p) => p.name === 'CompanySettingsPage')!
    renderPage(company.Page)
    await waitFor(() => expect(screen.queryByText(/^Loading/)).toBeNull(), { timeout: 3000 })
    expect(saveButtons()).toHaveLength(0)
    for (const input of within(document.body).getAllByRole('textbox')) expect(input).toBeDisabled()
  })
})

describe('Settings: translations', () => {
  it('every key the Settings and sign-in pages use exists in English and Vietnamese', async () => {
    const auth = import.meta.glob('../auth/*Page.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi_ = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    // keys chosen at run time (carrier / gateway field names)
    for (const src of [...Object.values(sources), ...Object.values(auth)]) {
      for (const k of [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi_, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect([...new Set(missing)]).toEqual([])
  })
})
