import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ComponentType } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { gatewayClient as apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// Every HRM API goes through the one axios client: answer every GET with an empty list that works as
// both a paged result and a plain array.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  // the HRM API files use the gateway client
  return { apiClient: client, gatewayClient: client }
})

import { AttendancePage } from './AttendancePage'
import { LeaveRequestsPage } from './LeaveRequestsPage'
import { PayrollPage } from './PayrollPage'
import { PayrollSettingsPage } from './PayrollSettingsPage'
import { SalaryProfilesPage } from './SalaryProfilesPage'
import { WorkShiftsPage } from './WorkShiftsPage'

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

beforeEach(() => {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.c', tenantId: 't1', roles: ['Admin'], permissions: [] },
    isAuthenticated: true,
  })
})

/** The first button outside tables, tab strips and the page header's filters: the page's add button. */
async function clickAdd() {
  const add = await waitFor(() => {
    const b = screen.getAllByRole('button').find((x) => x.closest('table') === null && !x.closest('[role=tablist]'))
    if (!b) throw new Error('no add button yet')
    return b
  })
  await userEvent.click(add)
  return screen.findByRole('dialog')
}

const DIALOG_PAGES: Array<[string, ComponentType]> = [
  ['Attendance', AttendancePage],
  ['Leave requests', LeaveRequestsPage],
  ['Payroll', PayrollPage],
  ['Payroll settings', PayrollSettingsPage],
  ['Work shifts', WorkShiftsPage],
]

describe.each(DIALOG_PAGES)('HRM: %s', (_name, Page) => {
  it('shows the empty state, opens the first dialog, shows linked required errors, is accessible, and closes with Esc', async () => {
    renderPage(Page)
    expect(await screen.findByText('No records found.')).toBeInTheDocument()

    const dialog = await clickAdd()
    expect(within(dialog).getByRole('heading')).toBeInTheDocument()
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()

    const submit = within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')
    expect(submit).toBeTruthy()
    await userEvent.click(submit!)
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }

    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(document.body.style.overflow).not.toBe('hidden')
  })
})

describe('HRM: Salary profiles', () => {
  it('shows the empty state and is accessible', async () => {
    const { container } = renderPage(SalaryProfilesPage)
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('HRM: Payroll list', () => {
  it('offers Clear when a filter hides everything', async () => {
    renderPage(PayrollPage)
    await userEvent.type(screen.getByLabelText('Year'), '2031')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
  })

  it('opens a pay slip, shows the breakdown, asks for a reason before cancelling, and never calls the API on open', async () => {
    const slip = {
      id: 's1', paySlipNumber: 'PS-0001', employeeId: 'e1', year: 2031, month: 1, payPeriod: '2031-01', grossSalary: 1000, deductions: 0,
      overtimeHours: 0, overtimePay: 0, netSalary: 900, paymentDate: '', paymentStatus: 'Pending', createdAt: '', baseSalary: 1000, allowances: 0,
      workedDays: 22, standardDays: 22, employeeSocial: 80, employeeHealth: 15, employeeUnemployment: 10, employerSocial: 170, employerHealth: 30,
      employerUnemployment: 10, taxableIncome: 0, personalIncomeTax: 0, dependentCount: 0,
    }
    vi.mocked(apiClient.get).mockImplementation((url: string) =>
      Promise.resolve({ data: /payslips/.test(url) ? { items: [slip], totalCount: 1, page: 1, pageSize: 20 } : Object.assign([], { items: [], totalCount: 0 }) }) as never,
    )
    vi.mocked(apiClient.post).mockClear()
    vi.mocked(apiClient.put).mockClear()

    renderPage(PayrollPage)
    await userEvent.click(await screen.findByRole('button', { name: 'Details' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Net Salary')).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    expect(apiClient.post).not.toHaveBeenCalled()
    expect(apiClient.put).not.toHaveBeenCalled()

    // cancelling needs a written reason: the confirm button stays disabled until one is typed
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel pay slip' }))
    const confirm = within(dialog).getByRole('button', { name: 'Cancel the pay slip' })
    expect(confirm).toBeDisabled()
    await userEvent.type(within(dialog).getByRole('textbox'), 'wrong month')
    expect(confirm).toBeEnabled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(apiClient.post).not.toHaveBeenCalled()
  })
})

describe('HRM: translations', () => {
  it('every key the HRM pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const get = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const [file, src] of Object.entries(files)) {
      if (file.endsWith('.test.tsx')) continue
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g), ...src.matchAll(/labelKey: '([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof get(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof get(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
