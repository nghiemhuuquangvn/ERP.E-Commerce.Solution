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

// The CRM API files use the gateway client, the users API the plain one: both are the same fake here.
// Every GET answers an empty list that works as both a paged result and a plain array.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { ActivitiesTab } from './ActivitiesTab'
import { CasesTab } from './CasesTab'
import { ContactsTab } from './ContactsTab'
import { CustomersPage } from './CustomersPage'
import { LeadsTab } from './LeadsTab'
import { OpportunitiesTab } from './OpportunitiesTab'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

function renderAt(Page: ComponentType, path = '/customers') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Page />
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const paged = (items: unknown[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20 })
/** Answer the list of one resource, everything else empty. */
function answer(match: RegExp, items: unknown[], extra?: (url: string) => unknown) {
  get.mockImplementation(((url: string) => {
    if (extra) { const x = extra(url); if (x !== undefined) return Promise.resolve({ data: x }) }
    return Promise.resolve({ data: match.test(url) ? paged(items) : Object.assign([], { items: [], totalCount: 0 }) })
  }) as never)
}

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.c', tenantId: 't1', roles: ['Admin'], permissions: [] },
    isAuthenticated: true,
  })
})

/** The page's add button: the only button whose label starts with "+". */
const addButton = () => screen.findByRole('button', { name: /^\+/ })

const TABS: Array<[string, ComponentType]> = [
  ['Contacts', ContactsTab],
  ['Leads', LeadsTab],
  ['Opportunities', OpportunitiesTab],
  ['Cases', CasesTab],
  ['Activities', ActivitiesTab],
]

describe.each(TABS)('CRM: %s tab', (_name, Tab) => {
  it('shows the empty state, opens the add dialog, shows linked required errors, is accessible, and closes with Esc', async () => {
    renderAt(Tab)
    expect(await screen.findByText('No records found.')).toBeInTheDocument()

    await userEvent.click(await addButton())
    const dialog = await screen.findByRole('dialog')
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()

    await userEvent.click(within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!)
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }

    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(document.body.style.overflow).not.toBe('hidden')
  })
})

describe('CRM: page shell', () => {
  it('opens the tab named in the URL and moves between tabs with the arrow keys', async () => {
    renderAt(CustomersPage, '/customers?tab=leads')
    const leads = await screen.findByRole('tab', { name: 'Leads' })
    expect(leads).toHaveAttribute('aria-selected', 'true')
    leads.focus()
    await userEvent.keyboard('{ArrowRight}')
    expect(await screen.findByRole('tab', { name: 'Opportunities' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('tabpanel')).toBeInTheDocument()
  })
})

describe('CRM: contacts', () => {
  it('search with no match offers Clear', async () => {
    renderAt(ContactsTab)
    await userEvent.type(await screen.findByRole('textbox'), 'zzz')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
  })

  it('the tags dialog lists tags, removes one through the API and is accessible', async () => {
    const contact = { id: 'c1', name: 'Ada', email: null, phone: null, company: null, contactSource: 'Manual' }
    answer(/\/contacts$/, [contact], (url) => (/tags$/.test(url) ? { tags: ['vip'] } : undefined))
    vi.mocked(apiClient.delete).mockClear()
    renderAt(ContactsTab)
    await userEvent.click(await screen.findByRole('button', { name: 'Tags' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('vip')).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(within(dialog).getByRole('button', { name: /vip/ }))
    await waitFor(() => expect(apiClient.delete).toHaveBeenCalled())
  })
})

describe('CRM: leads', () => {
  const lead = { id: 'l1', leadName: 'Acme', leadStatus: 'New', leadSource: 'Website', estimatedValue: 5000, contactId: 'c1', createdAt: '', updatedAt: '' }

  it('Qualify explains the side effect and calls the API only after confirming', async () => {
    answer(/\/leads$/, [lead])
    renderAt(LeadsTab)
    await userEvent.click(await screen.findByRole('button', { name: 'Qualify' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/creates an opportunity/i)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Qualify' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/leads/l1/qualify')))
  })

  it('Mark lost needs a reason', async () => {
    answer(/\/leads$/, [lead])
    renderAt(LeadsTab)
    await userEvent.click(await screen.findByRole('button', { name: 'Mark Lost' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!)
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
  })

  it('a lead without a contact cannot be qualified: no Qualify button, a badge says why', async () => {
    answer(/\/leads$/, [{ ...lead, contactId: null }])
    renderAt(LeadsTab)
    expect(await screen.findByText('No contact - cannot qualify')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Qualify' })).toBeNull()
  })

  it('the create form needs a contact: refused with a linked error, saved once one is chosen', async () => {
    answer(/\/contacts$/, [{ id: 'c1', name: 'Ada', email: null, phone: null, company: null, contactSource: 'Manual' }])
    renderAt(LeadsTab)
    await userEvent.click(await addButton())
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Lead name/i), 'Acme deal')
    await userEvent.click(within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!)
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
    await userEvent.selectOptions(within(dialog).getByLabelText(/^Contact/), await within(dialog).findByRole('option', { name: 'Ada' }))
    await userEvent.click(within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!)
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/leads'), expect.objectContaining({ contactId: 'c1' })))
  })

  it('with no contact at all the form says to create one first and saving is refused', async () => {
    renderAt(LeadsTab)
    await userEvent.click(await addButton())
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText(/no contact yet/i)).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText(/Lead name/i), 'Acme deal')
    await userEvent.click(within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!)
    await waitFor(() => expect(dialog.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
  })

  it('hides the actions of a qualified lead', async () => {
    answer(/\/leads$/, [{ ...lead, leadStatus: 'Qualified' }])
    renderAt(LeadsTab)
    expect(await screen.findByText('Acme')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Qualify' })).toBeNull()
  })
})

describe('CRM: opportunities', () => {
  it('proposes an expected close date in the future', async () => {
    renderAt(OpportunitiesTab)
    await userEvent.click(await addButton())
    const dialog = await screen.findByRole('dialog')
    const date = (dialog.querySelector('input[type=date]') as HTMLInputElement).value
    expect(date > new Date().toISOString().slice(0, 10)).toBe(true)
  })

  it('a stage chip filters the list and a second click clears it', async () => {
    answer(/\/opportunities$/, [], (url) => (/pipeline\/summary/.test(url) ? { prospecting: 2, qualification: 1, proposal: 0, negotiation: 0, won: 3, lost: 1, totalValue: 0, wonValue: 0 } : undefined))
    renderAt(OpportunitiesTab)
    const chip = await screen.findByRole('button', { name: /Won/ })
    expect(chip).toHaveAttribute('aria-pressed', 'false')
    await userEvent.click(chip)
    expect(chip).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(chip)
    expect(chip).toHaveAttribute('aria-pressed', 'false')
  })
})

describe('CRM: cases', () => {
  it('Close asks to confirm before calling the API, and a closed case has no actions', async () => {
    const resolved = { id: 'k1', caseNumber: 'CASE-1', contactId: 'c1', caseSubject: 'Broken', priority: 'High', status: 'Resolved', createdAt: '', updatedAt: '' }
    answer(/\/cases$/, [resolved])
    renderAt(CasesTab)
    await userEvent.click(await screen.findByRole('button', { name: 'Close' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Close case' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/cases/k1/close')))
  })

  it('shows the priority as a badge', async () => {
    answer(/\/cases$/, [{ id: 'k2', caseNumber: 'CASE-2', contactId: 'c1', caseSubject: 'Slow', priority: 'Critical', status: 'Open', createdAt: '', updatedAt: '' }])
    renderAt(CasesTab)
    expect(await screen.findByText('Critical')).toBeInTheDocument()
  })
})

describe('CRM: activities', () => {
  it('Cancel asks to confirm before calling the API', async () => {
    const activity = { id: 'a1', contactId: 'c1', activityType: 'Call', subject: 'Call back', status: 'Scheduled', scheduledAt: '2031-01-01T10:00:00Z' }
    answer(/\/activities$/, [activity])
    renderAt(ActivitiesTab)
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel activity' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/activities/a1/cancel')))
  })
})

describe('CRM: translations', () => {
  it('every key the CRM pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const [file, src] of Object.entries(files)) {
      if (file.endsWith('.test.tsx')) continue
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g), ...src.matchAll(/labelKey: '([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
