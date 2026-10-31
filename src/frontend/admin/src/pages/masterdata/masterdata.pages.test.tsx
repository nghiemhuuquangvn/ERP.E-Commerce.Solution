import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ComponentType } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { axe } from '@/test/axe'
import en from '@/i18n/locales/en.json'

// Every Master data API goes through the one axios client: answer every GET with an empty list that works as
// both a paged result ({ items, totalCount }) and a plain array.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  return { apiClient: { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) } }
})

import { BranchesPage } from './BranchesPage'
import { BusinessUnitsPage } from './BusinessUnitsPage'
import { CompaniesPage } from './CompaniesPage'
import { CostCentersPage } from './CostCentersPage'
import { CurrenciesPage } from './CurrenciesPage'
import { DepartmentsPage } from './DepartmentsPage'
import { DivisionsPage } from './DivisionsPage'
import { EmployeeTeamAssignmentsPage } from './EmployeeTeamAssignmentsPage'
import { EmployeesPage } from './EmployeesPage'
import { GeographyPage } from './GeographyPage'
import { LanguagesPage } from './LanguagesPage'
import { OrganizationCalendarsPage } from './OrganizationCalendarsPage'
import { PaymentTermsPage } from './PaymentTermsPage'
import { PositionsPage } from './PositionsPage'
import { ProfitCentersPage } from './ProfitCentersPage'
import { TeamsPage } from './TeamsPage'
import { UnitsOfMeasurePage } from './UnitsOfMeasurePage'

/** The text of the empty list; a page that asks for a filter first says so instead. */
const EMPTY: Record<string, string> = { 'Employee team assignments': en.masterdata.employeeTeamAssignments.pickEmployeeFirst }

const PAGES: Array<[string, ComponentType]> = [
  ['Branches', BranchesPage], ['Business units', BusinessUnitsPage], ['Companies', CompaniesPage], ['Cost centers', CostCentersPage],
  ['Currencies', CurrenciesPage], ['Departments', DepartmentsPage], ['Divisions', DivisionsPage],
  ['Employee team assignments', EmployeeTeamAssignmentsPage], ['Employees', EmployeesPage], ['Geography', GeographyPage],
  ['Languages', LanguagesPage], ['Organization calendars', OrganizationCalendarsPage], ['Payment terms', PaymentTermsPage],
  ['Positions', PositionsPage], ['Profit centers', ProfitCentersPage], ['Teams', TeamsPage], ['Units of measure', UnitsOfMeasurePage],
]

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

describe.each(PAGES)('Master data: %s', (name, Page) => {
  it('shows the empty state, opens the add dialog, refuses an empty form with linked errors, and closes with Esc', async () => {
    renderPage(Page)
    // empty list: the shared empty state, not a blank table
    expect(await screen.findByText(EMPTY[name] ?? 'No records found.')).toBeInTheDocument()

    // the header's add button opens a labelled dialog
    const add = await waitFor(() => {
      const b = screen.getAllByRole('button').find((x) => x.closest('table') === null && !x.closest('[role=tablist]'))
      if (!b) throw new Error('no add button yet')
      return b
    })
    await userEvent.click(add)
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('heading')).toBeInTheDocument()
    expect(document.body.style.overflow).toBe('hidden')

    // the dialog has no accessibility violations
    expect(await axe(dialog)).toHaveNoViolations()

    // submit the empty form: every required field shows an error linked to its control
    const submit = within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')
    expect(submit).toBeTruthy()
    await userEvent.click(submit!)
    const alerts = await within(dialog).findAllByRole('alert')
    expect(alerts.length).toBeGreaterThan(0)
    const invalid = dialog.querySelectorAll('[aria-invalid="true"]')
    expect(invalid.length).toBe(alerts.length)
    for (const control of invalid) {
      const described = control.getAttribute('aria-describedby')
      expect(described && document.getElementById(described)).toBeTruthy()
    }

    // Esc closes the dialog and unlocks the page
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(document.body.style.overflow).not.toBe('hidden')
  })
})
