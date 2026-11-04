import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ReactElement } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import i18n from 'i18next'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { axe } from '@/test/axe'

// The purchasing API files use the gateway client: both clients are the same fake here.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { RfqDetailPage, RfqListPage } from './RfqPages'
import { PurchasingSettingsPage, SupplierInvoiceDetailPage, SupplierInvoiceListPage } from './SupplierInvoicePages'
import { VendorReturnListPage } from './VendorReturnPages'

const get = vi.mocked(apiClient.get)
const post = vi.mocked(apiClient.post)

function renderAt(ui: ReactElement, path = '/', route = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path={route} element={ui} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const emptyList = () => Object.assign([], { items: [], totalCount: 0, total: 0 })
/** Answer by url: the first matching rule wins, everything else is an empty list. */
function answer(rules: Array<[RegExp, unknown]>) {
  get.mockImplementation(((url: string) => {
    const hit = rules.find(([re]) => re.test(url))
    return Promise.resolve({ data: hit ? hit[1] : emptyList() })
  }) as never)
}

const ALL = ['rfq:manage', 'supplier_invoice:manage', 'supplier_invoice:approve', 'supplier_invoice:approve_variance', 'vendor_return:manage', 'purchasing_settings:manage']

beforeEach(() => {
  get.mockReset()
  post.mockReset()
  get.mockImplementation((() => Promise.resolve({ data: emptyList() })) as never)
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({
    user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: ['Admin'], permissions: ALL },
    isAuthenticated: true,
  })
})

const submitOf = (dialog: HTMLElement) => within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!
const linked = (dialog: HTMLElement) => {
  const bad = dialog.querySelectorAll('[aria-invalid="true"]')
  expect(bad.length).toBeGreaterThan(0)
  for (const control of bad) expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
}

const rfq = {
  id: 'r1', rfqNumber: 'RFQ-1', title: 'Boxes', neededBy: null, notes: null, status: 'Sent', sentAt: null, awardedSupplierId: null, awardedPurchaseOrderId: null, createdAt: '2031-01-01T00:00:00Z',
  lines: [{ id: 'l1', productCode: 'BOX', productName: 'Box', quantity: 10, unit: 'pcs' }],
  invitations: [{ supplierId: 's1', supplierName: 'Acme', state: 'Quoted' }, { supplierId: 's2', supplierName: 'Bolt', state: 'Invited' }],
  quotations: [{ id: 'q1', supplierId: 's1', supplierName: 'Acme', quoteNumber: 'Q-1', validUntil: '2031-02-01T00:00:00Z', currencyCode: 'VND', leadTimeDays: 5, notes: null, isWinner: false, lines: [{ rfqLineId: 'l1', unitPrice: 100 }] }],
}
const comparison = {
  rfqId: 'r1', rfqNumber: 'RFQ-1', status: 'Sent',
  lines: [{ lineId: 'l1', productCode: 'BOX', productName: 'Box', quantity: 10, unit: 'pcs', lowestPrice: 100, prices: [{ supplierId: 's1', unitPrice: 100, expired: false, isLowest: true }] }],
  suppliers: [{ supplierId: 's1', supplierName: 'Acme', quoteNumber: 'Q-1', currencyCode: 'VND', leadTimeDays: 5, validUntil: '2031-02-01T00:00:00Z', expired: false, isWinner: true, total: 1000 }],
}

describe('Purchasing: RFQs', () => {
  it('shows the empty state, opens the new RFQ dialog with linked required errors, and closes with Esc', async () => {
    renderAt(<RfqListPage />)
    expect(await screen.findByText('No request for quotation yet.')).toBeInTheDocument()
    await userEvent.click(screen.getAllByRole('button', { name: '+ New RFQ' })[0])
    const dialog = await screen.findByRole('dialog')
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => linked(dialog))
    expect(within(dialog).getByText('Add at least one line with a product code.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('a filter with no match offers Clear', async () => {
    renderAt(<RfqListPage />)
    await screen.findByText('No request for quotation yet.')
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'Sent')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No request for quotation yet.')).toBeInTheDocument()
  })

  const detail = () => renderAt(<RfqDetailPage />, '/rfqs/r1', '/rfqs/:id')
  const open = () => answer([[/rfqs\/r1\/comparison/, comparison], [/rfqs\/r1$/, rfq]])

  it('Send and Cancel RFQ ask first and say what happens; nothing is sent until confirmed', async () => {
    answer([[/rfqs\/r1$/, { ...rfq, status: 'Draft', quotations: [] }]])
    detail()
    await userEvent.click(await screen.findByRole('button', { name: 'Send to suppliers' }))
    let dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/can no longer be edited/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Send to suppliers' }))
    dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Send to suppliers' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/rfqs/r1/send'), undefined))
  })

  it('Award names the supplier, creates nothing before the confirmation, and the Quotation dialog is accessible', async () => {
    open()
    detail()
    await userEvent.click(await screen.findByRole('button', { name: 'Award: Acme' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/draft purchase order is created at Acme/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Enter quotation: Bolt' }))
    const q = await screen.findByRole('dialog')
    expect(await axe(q)).toHaveNoViolations()
    await userEvent.click(submitOf(q))
    await waitFor(() => expect(q.querySelector('[aria-invalid="true"]')).toBeTruthy())
    expect(post).not.toHaveBeenCalled()
  })

  it('a refusal without a reason shows the translated fallback, not the raw key', async () => {
    open()
    post.mockRejectedValueOnce({ response: { status: 500, data: {} } })
    detail()
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel RFQ' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel RFQ' }))
    expect(await screen.findByText('Something went wrong. Please try again.')).toBeInTheDocument()
    expect(screen.queryByText('common.error')).toBeNull()
  })
})

const invoice = {
  id: 'i1', invoiceNumber: 'INV-1', supplierId: 's1', supplierName: 'Acme', purchaseOrderId: 'po1', poNumber: 'PO-1', invoiceDate: '2031-01-01T00:00:00Z', dueDate: '2031-02-01T00:00:00Z',
  currencyCode: 'VND', netAmount: 1000, taxAmount: 100, totalAmount: 1100, status: 'Matched', holdReasons: null, approvalReason: null, rejectionReason: null, approvedBy: null, approvedAt: null, receivedValue: 1000, createdAt: '2031-01-01T00:00:00Z',
  lines: [], matchLines: [{ productCode: 'BOX', ordered: 10, received: 10, returned: 0, previouslyInvoiced: 0, invoiced: 10, orderPrice: 100, invoicedPrice: 100, priceVariancePercent: 0, quantityVariance: 0, verdict: 'Matched' }],
}

describe('Purchasing: supplier invoices', () => {
  const detail = () => renderAt(<SupplierInvoiceDetailPage />, '/inv/i1', '/inv/:id')

  it('Approve says the bill is booked and sends nothing before the confirmation', async () => {
    answer([[/supplier-invoices\/i1$/, invoice]])
    detail()
    await userEvent.click(await screen.findByRole('button', { name: 'Approve for payment' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/bill is booked in Accounting/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(post).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Approve for payment' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Approve for payment' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/supplier-invoices/i1/approve'), { reason: null }))
  })

  it('Reject needs a reason first, then asks, then sends the reason', async () => {
    answer([[/supplier-invoices\/i1$/, invoice]])
    detail()
    const reject = await screen.findByRole('button', { name: 'Reject' })
    expect(reject).toBeDisabled()
    await userEvent.type(screen.getByRole('textbox', { name: 'Reason' }), 'wrong prices')
    await userEvent.click(reject)
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/supplier must send a new invoice/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/supplier-invoices/i1/reject'), { reason: 'wrong prices' }))
  })

  it('an invoice on hold offers Approve with variance only with a reason, and shows the hold reasons', async () => {
    answer([[/supplier-invoices\/i1$/, { ...invoice, status: 'OnHold', holdReasons: 'Price off on BOX' }]])
    detail()
    expect(await screen.findByText('Price off on BOX')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Approve with variance' })).toBeDisabled()
    await userEvent.type(screen.getByRole('textbox', { name: 'Reason' }), 'agreed with the supplier')
    expect(screen.getByRole('button', { name: 'Approve with variance' })).toBeEnabled()
  })

  it('the record dialog validates, and the list shows the empty state', async () => {
    renderAt(<SupplierInvoiceListPage />)
    expect(await screen.findByText('No supplier invoice yet.')).toBeInTheDocument()
    await userEvent.click(screen.getAllByRole('button', { name: '+ Record invoice' })[0])
    const dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => linked(dialog))
    expect(post).not.toHaveBeenCalled()
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('the settings refuse a tolerance above 100 and save with a toast', async () => {
    answer([[/purchasing-settings/, { quantityTolerancePercent: 0, priceTolerancePercent: 2, autoApproveMatched: false }]])
    renderAt(<PurchasingSettingsPage />)
    const price = await screen.findByLabelText('Price tolerance (%)')
    await userEvent.clear(price)
    await userEvent.type(price, '150')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Enter a number from 0 to 100.')).toBeInTheDocument()
    expect(vi.mocked(apiClient.put)).not.toHaveBeenCalled()
    await userEvent.clear(price)
    await userEvent.type(price, '3')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(vi.mocked(apiClient.put)).toHaveBeenCalled())
    expect(await screen.findByText('Saved.')).toBeInTheDocument()
  })
})

describe('Purchasing: returns to vendor', () => {
  const draft = { id: 'v1', returnNumber: 'RET-1', poNumber: 'PO-1', supplierName: 'Acme', currencyCode: 'VND', status: 'Draft', value: 500, createdAt: '2031-01-01T00:00:00Z' }

  it('Ship says the stock leaves and sends nothing before the confirmation; Cancel asks too', async () => {
    answer([[/vendor-returns$/, { items: [draft], totalCount: 1 }]])
    renderAt(<VendorReturnListPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Ship RET-1' }))
    let dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/leave the warehouse stock now/)).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    await userEvent.click(screen.getByRole('button', { name: 'Cancel RET-1' }))
    dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel return' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(expect.stringContaining('/vendor-returns/v1/cancel'), undefined))
  })

  it('a refused Ship is reported with the server reason', async () => {
    answer([[/vendor-returns$/, { items: [draft], totalCount: 1 }]])
    post.mockRejectedValueOnce({ response: { status: 400, data: { detail: 'Not enough stock to ship.' } } })
    renderAt(<VendorReturnListPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Ship RET-1' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Ship' }))
    expect(await screen.findByText('Not enough stock to ship.')).toBeInTheDocument()
  })

  it('the new return and credit dialogs validate with linked errors', async () => {
    answer([[/vendor-returns$/, { items: [{ ...draft, status: 'Shipped' }], totalCount: 1 }]])
    renderAt(<VendorReturnListPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Record credit note RET-1' }))
    let dialog = await screen.findByRole('dialog')
    expect(await axe(dialog)).toHaveNoViolations()
    await userEvent.click(submitOf(dialog))
    await waitFor(() => linked(dialog))
    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())

    await userEvent.click(screen.getByRole('button', { name: '+ New return' }))
    dialog = await screen.findByRole('dialog')
    await userEvent.click(submitOf(dialog))
    await waitFor(() => linked(dialog))
    expect(post).not.toHaveBeenCalled()
  })
})

describe('Purchasing: translations', () => {
  it('every key the Purchasing pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./{RfqPages,SupplierInvoicePages,VendorReturnPages}.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const src of Object.values(files)) {
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    // the verdict key is built from the match result
    for (const v of ['Matched', 'QuantityVariance', 'PriceVariance', 'QuantityAndPriceVariance', 'NotOnOrder']) {
      if (typeof lookup(en, `procurement.verdict.${v}`) !== 'string') missing.push(`en verdict ${v}`)
      if (typeof lookup(vi, `procurement.verdict.${v}`) !== 'string') missing.push(`vi verdict ${v}`)
    }
    expect(missing).toEqual([])
    expect(i18n).toBeDefined()
  })
})
