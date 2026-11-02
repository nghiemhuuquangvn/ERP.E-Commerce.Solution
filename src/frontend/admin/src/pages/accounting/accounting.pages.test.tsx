import { describe, it, expect, vi, beforeEach } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The accounting API goes through the gateway client: both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: [] })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { TaxRatesPage } from './TaxRatesPage'
import { FiscalPeriodsPage } from './FiscalPeriodsPage'
import { AccountsPage } from './AccountsPage'
import { BankAccountsPage } from './BankAccountsPage'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)
const del = vi.mocked(apiClient.delete)

const rates = [{ id: 'r1', name: 'VAT 10', code: 'VAT10', rate: 10, isActive: true, description: null, createdAt: '2026-01-01' }]
const periods = [
  { id: 'p1', periodName: '2026-01', startDate: '2026-01-01', endDate: '2026-01-31', status: 'Open', createdAt: '2026-01-01' },
  { id: 'p0', periodName: '2025-12', startDate: '2025-12-01', endDate: '2025-12-31', status: 'Closed', createdAt: '2025-12-01' },
]
const accounts = [
  { id: 'a1', accountNumber: '1111', name: 'Cash on hand', accountType: 'Asset', normalBalance: 'Debit', createdAt: '2026-01-01', isActive: true, parentAccountId: null, level: 1, isControl: false, accountClass: 1, isPostable: true, isCash: true, cashFlowCategory: 'Operating' },
]
const templates = [{ id: 'Vas99', name: 'Vietnam chart', description: 'Circular 99', accountCount: 120 }]
const bankAccounts = [{ id: 'b1', accountName: 'Main account', bankName: 'ACB', accountNumber: '123', currencyCode: 'VND', currentBalance: 1000, isActive: true, createdAt: '2026-01-01' }]
const statements = [{ id: 's1', bankAccountId: 'b1', statementDate: '2026-01-31T00:00:00', openingBalance: 0, closingBalance: 950, status: 'Open', createdAt: '2026-01-31' }]
const line = { id: 'l1', bankStatementId: 's1', lineDate: '2026-01-15T00:00:00', description: 'Monthly fee', reference: 'F1', amount: -50, balance: null, matchStatus: 'Unmatched', matchedJournalLineId: null, journalEntryId: null, matchedEntryNumber: null }
const recon = (over: Record<string, unknown> = {}) => ({
  bankStatementId: 's1', bankAccountId: 'b1', statementDate: '2026-01-31', status: 'Open', openingBalance: 0, closingBalance: 950,
  glAccountId: null, glAccountNumber: null, ledgerBalance: 1000, outstandingLedgerNet: 0, unmatchedStatementNet: -50, expectedClosing: 950,
  difference: 0, unmatchedStatementLines: 1, canReconcile: true, lines: [line], unmatchedLedger: [], notes: [], ...over,
})

let reconData = recon()
function route(url: string) {
  if (url.endsWith('/tax-rates')) return rates
  if (url.endsWith('/closing-policy')) return { closingMode: 'Monthly', fiscalYearEndMonth: 12, autoAccrueLoanInterest: false }
  if (url.endsWith('/fiscal-periods')) return periods
  if (url.endsWith('/chart/templates')) return templates
  if (url.endsWith('/accounts')) return accounts
  if (url.endsWith('/reconciliation')) return reconData
  if (url.endsWith('/statements')) return statements
  if (url.endsWith('/bank-accounts')) return bankAccounts
  return []
}

function renderPage(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const refusal = (detail: string) => Promise.reject({ response: { status: 400, data: { detail } } })

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  del.mockReset()
  reconData = recon()
  get.mockImplementation(((url: string) => Promise.resolve({ data: route(url) })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  del.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: {
      id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'],
      permissions: ['accounting:tax:manage', 'accounting:periods:manage', 'accounting:accounts:manage', 'accounting:bank:view', 'accounting:bank:manage'],
    },
    isAuthenticated: true,
  })
})

describe('Accounting: tax rates', () => {
  it('the dialog validates with linked messages, refuses a rate above 100 and sends a valid one', async () => {
    renderPage(<TaxRatesPage />)
    await screen.findByText('VAT 10')
    await userEvent.click(screen.getByRole('button', { name: 'Add Tax Rate' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    for (const c of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(c.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
    expect(post).not.toHaveBeenCalled()

    await userEvent.type(within(dialog).getByLabelText(/Name/), 'Reduced')
    await userEvent.type(within(dialog).getByLabelText(/Code/), 'VAT5')
    const rate = within(dialog).getByLabelText(/Rate/)
    await userEvent.clear(rate)
    await userEvent.type(rate, '150')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('Enter a rate from 0 to 100.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    await userEvent.clear(rate)
    await userEvent.type(rate, '5')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(post).toHaveBeenCalledTimes(1))
    expect(post.mock.calls[0][1]).toMatchObject({ name: 'Reduced', code: 'VAT5', rate: 5 })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('Deactivate asks first; Esc keeps the rate; a refusal is shown as a toast', async () => {
    renderPage(<TaxRatesPage />)
    await screen.findByText('VAT 10')
    await userEvent.click(screen.getByRole('button', { name: 'Deactivate VAT 10' }))
    const confirm = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()

    post.mockImplementationOnce((() => refusal('The rate is used by an open invoice.')) as never)
    await userEvent.click(screen.getByRole('button', { name: 'Deactivate VAT 10' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Deactivate' }))
    expect(await screen.findByText('The rate is used by an open invoice.')).toBeInTheDocument()
    expect(confirm).toBeTruthy()
  })
})

describe('Accounting: fiscal periods', () => {
  it('the open dialog refuses an end date before the start and sends a valid period', async () => {
    renderPage(<FiscalPeriodsPage />)
    await screen.findByText('2026-01')
    await userEvent.click(screen.getByRole('button', { name: 'Open Period' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.type(within(dialog).getByLabelText(/Period Name/), '2026-02')
    fireEvent.change(within(dialog).getByLabelText(/Start Date/), { target: { value: '2026-02-28' } })
    fireEvent.change(within(dialog).getByLabelText(/End Date/), { target: { value: '2026-02-01' } })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText('The end date must not be before the start date.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    fireEvent.change(within(dialog).getByLabelText(/End Date/), { target: { value: '2026-03-31' } })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(post).toHaveBeenCalledTimes(1))
    expect(post.mock.calls[0][1]).toMatchObject({ periodName: '2026-02', startDate: '2026-02-28', endDate: '2026-03-31' })
  })

  it('Close states the consequence, sends nothing before the confirm and shows the server refusal', async () => {
    renderPage(<FiscalPeriodsPage />)
    await screen.findByText('2026-01')
    post.mockImplementationOnce((() => refusal('An earlier period is still open.')) as never)
    await userEvent.click(screen.getByRole('button', { name: 'Close 2026-01' }))
    const confirm = await screen.findByRole('dialog')
    expect(within(confirm).getByText(/period is locked/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getAllByRole('button', { name: 'Close' }).slice(-1)[0])
    expect(await screen.findByText('An earlier period is still open.')).toBeInTheDocument()
    expect(post).toHaveBeenCalledTimes(1)
  })
})

describe('Accounting: chart of accounts', () => {
  it('Apply template asks before anything is sent and says what it adds', async () => {
    renderPage(<AccountsPage />)
    await screen.findByText('Cash on hand')
    await userEvent.click(screen.getByRole('button', { name: 'Apply chart template' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.selectOptions(within(dialog).getByLabelText('Apply chart template'), 'Vas99')
    expect(within(dialog).getByText(/Have your accountant review/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Apply chart template' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/Add the 120 accounts of "Vietnam chart"/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Cancel' }))
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within((await screen.findAllByRole('dialog')).slice(-1)[0]).getByRole('button', { name: 'Apply chart template' }))
    await userEvent.click(within((await screen.findAllByRole('dialog')).slice(-1)[0]).getByRole('button', { name: 'Apply chart template' }))
    await waitFor(() => expect(post).toHaveBeenCalledTimes(1))
    expect(String(post.mock.calls[0][0])).toContain('apply-template')
  })

  it('Deactivate asks first, an edit locks number and type, and a refusal is a toast', async () => {
    renderPage(<AccountsPage />)
    await screen.findByText('Cash on hand')
    await userEvent.click(screen.getByRole('button', { name: 'Edit 1111' }))
    const edit = await screen.findByRole('dialog')
    expect(within(edit).getByLabelText(/Account Number/)).toBeDisabled()
    expect(within(edit).getByLabelText(/Account Type/)).toBeDisabled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    post.mockImplementationOnce((() => refusal('The account has a balance.')) as never)
    await userEvent.click(screen.getByRole('button', { name: 'Deactivate 1111' }))
    const confirm = await screen.findByRole('dialog')
    expect(within(confirm).getByText(/takes no new postings/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Deactivate' }))
    expect(await screen.findByText('The account has a balance.')).toBeInTheDocument()
  })
})

describe('Accounting: bank accounts and reconciliation', () => {
  async function openReconciliation() {
    renderPage(<BankAccountsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'View Statements: Main account' }))
    await screen.findByRole('dialog')
    await userEvent.click(await screen.findByRole('button', { name: /^Lines of/ }))
    await screen.findByText('Monthly fee')
    return (await screen.findAllByRole('dialog')).slice(-1)[0]
  }

  it('the statement form validates before sending', async () => {
    renderPage(<BankAccountsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'View Statements: Main account' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Add Statement' })[0])
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    expect(post).not.toHaveBeenCalled()
  })

  it('post as fee, delete and reconcile each ask first; the difference is written out; Esc closes only the top dialog', async () => {
    const recDialog = await openReconciliation()
    expect(await axe(recDialog)).toHaveNoViolations()
    expect(within(recDialog).getByText(/^Difference 0 - ready to reconcile/)).toBeInTheDocument()

    // post as fee
    await userEvent.click(within(recDialog).getByRole('button', { name: 'Post as fee Monthly fee' }))
    let confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/as a bank fee\? A journal entry is booked/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.getAllByRole('dialog')).toHaveLength(2))
    expect(post).not.toHaveBeenCalled()

    // delete
    await userEvent.click(within(recDialog).getByRole('button', { name: 'Delete Monthly fee' }))
    confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/Delete "Monthly fee"/)).toBeInTheDocument()
    expect(del).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Cancel' }))
    expect(del).not.toHaveBeenCalled()

    // reconcile
    await userEvent.click(within(recDialog).getByRole('button', { name: 'Reconcile' }))
    confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/the statement is locked/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(confirm).getByRole('button', { name: 'Reconcile' }))
    await waitFor(() => expect(post).toHaveBeenCalledTimes(1))
    expect(String(post.mock.calls[0][0])).toContain('/reconcile')
  })

  it('a non-zero difference is stated in words and blocks Reconcile; a refused post shows the reason', async () => {
    reconData = recon({ difference: 1200, canReconcile: false })
    const recDialog = await openReconciliation()
    expect(within(recDialog).getByText(/Difference .*1.*200.* - cannot reconcile yet/)).toBeInTheDocument()
    expect(within(recDialog).getByRole('button', { name: 'Reconcile' })).toBeDisabled()

    post.mockImplementationOnce((() => refusal('The posting profile has no BankFee account.')) as never)
    await userEvent.click(within(recDialog).getByRole('button', { name: 'Post as fee Monthly fee' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    await userEvent.click(within(confirm).getByRole('button', { name: 'Post as fee' }))
    expect(await within(recDialog).findByText('The posting profile has no BankFee account.')).toBeInTheDocument()
  })
})

describe('Accounting: translations', () => {
  it('every key the pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob(['./TaxRatesPage.tsx', './FiscalPeriodsPage.tsx', './AccountsPage.tsx', './BankAccountsPage.tsx', '../../components/accounting/BankReconciliationModal.tsx'], {
      query: '?raw',
      import: 'default',
      eager: true,
    }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    // keys chosen at run time (fee / interest wording of the post confirm)
    const dynamic = ['accounting.bankRecon.postFeeTitle', 'accounting.bankRecon.postInterestTitle', 'accounting.bankRecon.postFeeConfirm', 'accounting.bankRecon.postInterestConfirm']
    for (const src of Object.values(files)) {
      for (const k of [...[...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1]), ...dynamic]) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
