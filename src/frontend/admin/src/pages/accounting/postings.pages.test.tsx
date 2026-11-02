import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
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

import { JournalEntriesPage } from './JournalEntriesPage'
import { BillsPage } from './BillsPage'
import { InvoicesPage } from './InvoicesPage'
import { FxRevaluationPanel } from '@/components/accounting/FxRevaluationPanel'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

const accounts = [
  { id: 'a1', accountNumber: '1111', name: 'Cash' },
  { id: 'a2', accountNumber: '5111', name: 'Revenue' },
]
const periods = [{ id: 'p1', periodName: '2026-11', status: 'Open' }]
const entry = { id: 'j1', entryNumber: 'JE-1', fiscalPeriodId: 'p1', description: 'Rent', totalDebit: 100, totalCredit: 100, createdAt: '2026-11-01T00:00:00', status: 'Posted', referenceType: null, lines: [] }
const bill = { id: 'b1', billNumber: 'B-1', supplierId: 's1', poId: 'po1', netAmount: 100, taxAmount: 10, taxRatePercent: 10, amount: 110, dueDate: '2026-12-01', status: 'Pending', postedToLedger: true }
const fxBill = { ...bill, id: 'b2', billNumber: 'B-2', status: 'Approved', foreignCurrencyCode: 'USD', foreignAmount: 5, exchangeRate: 25000 }
const invoice = { id: 'i1', invoiceNumber: 'INV-1', customerId: 'c1', orderId: 'o1', netAmount: 100, shippingNetAmount: 0, taxAmount: 10, taxRatePercent: 10, amount: 110, dueDate: '2026-12-01', status: 'Unpaid', postedToLedger: true }
const revaluation = {
  fiscalPeriodId: 'p1', periodName: '2026-11', asOfDate: '2026-11-30', baseCurrency: 'VND', rates: { USD: 25500 }, missingRates: [],
  lines: [{ documentType: 'Invoice', documentId: 'i9', number: 'INV-9', currency: 'USD', foreignOutstanding: 10, carryingAmount: 250000, closingRate: 25500, revaluedAmount: 255000, difference: 5000 }],
  receivablesDifference: 5000, payablesDifference: 0, netDifference: 5000, posted: false, entryNumber: null, reversalEntryNumber: null, notes: [],
}

let gaps: unknown[] = []
function route(url: string) {
  if (url.endsWith('/journal-entries')) return [entry]
  if (url.endsWith('/accounts')) return accounts
  if (url.endsWith('/fiscal-periods')) return periods
  if (url.endsWith('/bills')) return [bill, fxBill]
  if (url.endsWith('/invoices')) return [invoice]
  if (url.endsWith('/posting-gaps')) return gaps
  if (url.endsWith('/e-invoices')) return []
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
  gaps = []
  get.mockImplementation(((url: string) => Promise.resolve({ data: route(url) })) as never)
  post.mockImplementation(((url: string) => Promise.resolve({ data: url.endsWith('/preview') ? revaluation : {} })) as never)
  useAuthStore.setState({
    user: {
      id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'],
      permissions: ['accounting:journals:view', 'accounting:journals:manage', 'accounting:bills:view', 'accounting:bills:manage', 'accounting:invoices:view', 'accounting:invoices:manage'],
    },
    isAuthenticated: true,
  })
})

const posted = (suffix: string) => post.mock.calls.filter((c) => String(c[0]).endsWith(suffix))

describe('Accounting: journal entries', () => {
  it('the create dialog validates, says why Post is blocked and sends the exact lines when balanced', async () => {
    renderPage(<JournalEntriesPage />)
    await screen.findByText('JE-1')
    await userEvent.click(screen.getByRole('button', { name: 'New Journal Entry' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    expect(within(dialog).getByText(/posted to the ledger as soon as you press Post/)).toBeInTheDocument()

    await userEvent.type(within(dialog).getByLabelText(/Entry Number/), 'JE-2')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Fiscal Period/), 'p1')
    await userEvent.type(within(dialog).getByLabelText(/^Description/), 'Sale')
    await userEvent.selectOptions(within(dialog).getByLabelText('Line 1 Account'), 'a1')
    await userEvent.selectOptions(within(dialog).getByLabelText('Line 2 Account'), 'a2')
    await userEvent.type(within(dialog).getByLabelText('Line 1 Debit'), '100')
    expect(await within(dialog).findByText(/difference 100.00/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Post' }))
    expect(await within(dialog).findAllByText(/Not balanced/)).not.toHaveLength(0)
    expect(post).not.toHaveBeenCalled()

    await userEvent.type(within(dialog).getByLabelText('Line 2 Credit'), '100')
    expect(within(dialog).getByText('Balanced')).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Post' }))
    await waitFor(() => expect(posted('/journal-entries')).toHaveLength(1))
    expect(posted('/journal-entries')[0][1]).toMatchObject({
      entryNumber: 'JE-2', fiscalPeriodId: 'p1', description: 'Sale',
      lines: [{ accountId: 'a1', debit: 100, credit: 0 }, { accountId: 'a2', debit: 0, credit: 100 }],
    })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('an empty form shows linked errors and sends nothing', async () => {
    renderPage(<JournalEntriesPage />)
    await screen.findByText('JE-1')
    await userEvent.click(screen.getByRole('button', { name: 'New Journal Entry' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Post' }))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    expect(post).not.toHaveBeenCalled()
  })

  it('Reverse and Void ask for a reason first; a refusal stays in the dialog', async () => {
    renderPage(<JournalEntriesPage />)
    await screen.findByText('JE-1')
    await userEvent.click(screen.getByRole('button', { name: 'Reverse JE-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()

    post.mockImplementationOnce((() => refusal('The period is closed.')) as never)
    await userEvent.click(screen.getByRole('button', { name: 'Void JE-1' }))
    const voidDialog = await screen.findByRole('dialog')
    await userEvent.type(within(voidDialog).getByLabelText(/Reason/), 'typo')
    await userEvent.click(within(voidDialog).getByRole('button', { name: 'Void' }))
    expect(await within(voidDialog).findByText('The period is closed.')).toBeInTheDocument()
    expect(dialog).toBeTruthy()
  })

  it('the page has no accessibility violations', async () => {
    const { container } = renderPage(<JournalEntriesPage />)
    await screen.findByText('JE-1')
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Accounting: bills', () => {
  it('Approve opens a dialog first; an unchanged rate sends none, a changed one is sent', async () => {
    renderPage(<BillsPage />)
    await screen.findByText('B-1')
    await userEvent.click(screen.getByRole('button', { name: 'Approve B-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/books the bill/i)).toBeInTheDocument()
    expect(await axe(dialog)).toHaveNoViolations()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(posted('/bills/b1/approve')).toHaveLength(1))
    expect(posted('/bills/b1/approve')[0][1]).toEqual({ taxRatePercent: undefined })

    await userEvent.click(await screen.findByRole('button', { name: 'Approve B-1' }))
    const second = await screen.findByRole('dialog')
    const rate = within(second).getByLabelText(/VAT rate/)
    await userEvent.clear(rate)
    await userEvent.type(rate, '8')
    await userEvent.click(within(second).getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(posted('/bills/b1/approve')).toHaveLength(2))
    expect(posted('/bills/b1/approve')[1][1]).toEqual({ taxRatePercent: 8 })
  })

  it('Mark paid asks first; a foreign bill needs a rate above 0 and sends it', async () => {
    renderPage(<BillsPage />)
    await screen.findByText('B-2')
    await userEvent.click(screen.getByRole('button', { name: 'Mark Paid B-2' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    const rate = within(dialog).getByLabelText(/Rate the USD was paid at/)
    await userEvent.clear(rate)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Mark paid' }))
    expect(await within(dialog).findByText('Enter a rate greater than 0.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.type(rate, '25100')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Mark paid' }))
    await waitFor(() => expect(posted('/bills/b2/mark-paid')).toHaveLength(1))
    expect(posted('/bills/b2/mark-paid')[0][1]).toEqual({ paymentRate: 25100 })
  })

  it('Mark paid of a base-currency bill needs the confirm too, and a refusal is a toast', async () => {
    renderPage(<BillsPage />)
    await screen.findByText('B-1')
    await userEvent.click(screen.getByRole('button', { name: 'Mark Paid B-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    post.mockImplementationOnce((() => refusal('No open fiscal period.')) as never)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Mark paid' }))
    expect(await screen.findByText('No open fiscal period.')).toBeInTheDocument()
  })

  it('Post missing entries confirms first', async () => {
    gaps = [{ documentType: 'Bill', documentId: 'b1', number: 'B-1', date: '2026-11-01', amount: 110, posted: null, entryNumber: null }]
    renderPage(<BillsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Post missing entries' }))
    const dialog = await screen.findByRole('dialog')
    expect(posted('/posting-gaps/repost')).toHaveLength(0)
    await userEvent.click(within(dialog).getAllByRole('button', { name: 'Post missing entries' }).slice(-1)[0])
    await waitFor(() => expect(posted('/posting-gaps/repost')).toHaveLength(1))
  })

  it('the create dialog validates with linked errors and the page has no violations', async () => {
    const { container } = renderPage(<BillsPage />)
    await screen.findByText('B-1')
    expect(await axe(container)).toHaveNoViolations()
    await userEvent.click(screen.getByRole('button', { name: 'New Bill' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThan(0))
    expect(posted('/bills')).toHaveLength(0)
  })
})

describe('Accounting: invoices', () => {
  it('Mark paid asks first', async () => {
    renderPage(<InvoicesPage />)
    await screen.findByText('INV-1')
    await userEvent.click(screen.getByRole('button', { name: 'Mark Paid INV-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Mark paid' }))
    await waitFor(() => expect(posted('/invoices/i1/mark-paid')).toHaveLength(1))
  })

  it('Adjust VAT stays blocked until the lines add up to the invoice amount', async () => {
    renderPage(<InvoicesPage />)
    await screen.findByText('INV-1')
    await userEvent.click(screen.getByRole('button', { name: 'Adjust VAT INV-1' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByLabelText('Several VAT lines (rates / goods not subject to VAT)'))
    const amount = within(dialog).getByLabelText('Line 1 amount')
    await userEvent.clear(amount)
    await userEvent.type(amount, '100')
    expect(within(dialog).getByRole('button', { name: 'Save' })).toBeDisabled()
    await userEvent.clear(amount)
    await userEvent.type(amount, '110')
    expect(within(dialog).getByRole('button', { name: 'Save' })).toBeEnabled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(posted('/invoices/i1/adjust-vat')).toHaveLength(1))
  })

  it('Issue e-invoice names the consequence and sends nothing before the confirm', async () => {
    renderPage(<InvoicesPage />)
    await screen.findByText('INV-1')
    await userEvent.click(screen.getByRole('button', { name: 'E-invoice INV-1' }))
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(await within(dialog).findByRole('button', { name: 'Issue e-invoice' }))
    const confirm = (await screen.findAllByRole('dialog')).slice(-1)[0]
    expect(within(confirm).getByText(/VAT of the invoice is locked|VAT is locked/i)).toBeInTheDocument()
    expect(posted('/e-invoice')).toHaveLength(0)
    await userEvent.keyboard('{Escape}')
    expect(posted('/e-invoice')).toHaveLength(0)
  })

  it('the page has no accessibility violations', async () => {
    const { container } = renderPage(<InvoicesPage />)
    await screen.findByText('INV-1')
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Accounting: foreign currency revaluation', () => {
  it('posting asks first and states the period, the figure and the reversal', async () => {
    renderPage(<FxRevaluationPanel canManage />)
    await screen.findByRole('option', { name: '2026-11' })
    await userEvent.selectOptions(screen.getByLabelText('Period Name'), 'p1')
    await userEvent.click(await screen.findByRole('button', { name: 'Post the revaluation' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/2026-11/)).toBeInTheDocument()
    expect(within(dialog).getByText(/reversed the next day/)).toBeInTheDocument()
    expect(posted('/fx/revaluation')).toHaveLength(0)
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(posted('/fx/revaluation')).toHaveLength(0)

    await userEvent.click(screen.getByRole('button', { name: 'Post the revaluation' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Post the revaluation' }))
    await waitFor(() => expect(posted('/fx/revaluation')).toHaveLength(1))
  })
})

describe('Accounting postings: translations', () => {
  it('every key the files use exists in English and Vietnamese', async () => {
    const files = import.meta.glob(
      ['./JournalEntriesPage.tsx', './BillsPage.tsx', './InvoicesPage.tsx', '../../components/accounting/{FxRevaluationPanel,PostingGapsBar,PayDialog,ForeignCurrencyFields,VatLinesEditor}.tsx'],
      { query: '?raw', import: 'default', eager: true },
    ) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    // keys chosen at run time: status names and the buyer fields
    const dynamic = ['bills.statusPending', 'bills.statusApproved', 'bills.statusPaid', 'invoices.statusUnpaid', 'invoices.statusPaid', 'invoices.statusOverdue'].map((k) => `accounting.${k}`).concat(['buyerName', 'buyerTaxCode', 'buyerAddress'].map((k) => `accounting.vat.${k}`))
    for (const src of Object.values(files)) {
      for (const k of [...[...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1]), ...dynamic]) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
