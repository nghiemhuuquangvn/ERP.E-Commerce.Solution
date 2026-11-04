import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import i18n from '@/i18n'
import { renderScreen } from './render'

const mockPush = jest.fn()
let mockParams: Record<string, string> = {}
jest.mock('expo-router', () => ({
  useRouter: () => ({ push: mockPush, navigate: jest.fn(), replace: jest.fn() }),
  useLocalSearchParams: () => mockParams,
  usePathname: () => '/',
  Stack: { Screen: () => null },
}))
jest.mock('@/api/client', () => ({ isNetworkError: (e: { response?: unknown }) => !e?.response, apiClient: {}, gatewayClient: {} }))
jest.mock('@/modules/purchase/procurementApi', () => ({
  ...jest.requireActual('@/modules/purchase/procurementApi'),
  procurementApi: {
    supplier: jest.fn(), supplierRatings: jest.fn(), supplierItems: jest.fn(), rfq: jest.fn(), rfqComparison: jest.fn(),
    invoice: jest.fn(), approveInvoice: jest.fn(), rejectInvoice: jest.fn(), vendorReturn: jest.fn(), shipReturn: jest.fn(), cancelReturn: jest.fn(), creditReturn: jest.fn(),
  },
}))
jest.mock('@/modules/sales/api', () => ({
  ...jest.requireActual('@/modules/sales/api'),
  salesExtrasApi: {
    quotation: jest.fn(), sendQuotation: jest.fn(), acceptQuotation: jest.fn(), rejectQuotation: jest.fn(), convertQuotation: jest.fn(),
    creditLimits: jest.fn(), creditLimit: jest.fn(), setCreditLimit: jest.fn(),
  },
}))
jest.mock('@/modules/hrm/api', () => ({
  ...jest.requireActual('@/modules/hrm/api'),
  hrmApi: { employees: jest.fn().mockResolvedValue({ items: [{ id: 'e1', fullName: 'Nguyen An' }], total: 1 }) },
  hrmExtrasApi: { paySlip: jest.fn(), payPaySlip: jest.fn(), cancelPaySlip: jest.fn() },
}))

import { InvoiceDetailScreen, RfqDetailScreen, ReturnDetailScreen, SupplierDetailScreen } from '@/modules/purchase/PurchaseDetailScreens'
import QuotationDetailScreen from '@/modules/sales/QuotationDetailScreen'
import PaySlipDetailScreen from '@/modules/hrm/PaySlipDetailScreen'
import { CreditLimitDetailScreen, CreditLimitsScreen } from '@/modules/sales/CreditLimitScreens'
import { procurementApi } from '@/modules/purchase/procurementApi'
import { salesExtrasApi } from '@/modules/sales/api'
import { hrmExtrasApi } from '@/modules/hrm/api'
import { useAuthStore } from '@/stores/auth.store'
import { useToastStore } from '@/components/ui/Toast'

const proc = jest.mocked(procurementApi)
const sales = jest.mocked(salesExtrasApi)
const hr = jest.mocked(hrmExtrasApi)

const signInAs = (...permissions: string[]) =>
  useAuthStore.setState({ status: 'signedIn', permissions, licensedModules: [], user: { id: 'u1', email: 'a@b.c', name: 'Ann', roles: [] }, accessToken: 'a', refreshToken: 'r' })

beforeEach(() => {
  jest.clearAllMocks()
  mockParams = { id: 'x1' }
  void i18n.changeLanguage('en')
  useToastStore.setState({ items: [] })
  useAuthStore.setState({ status: 'signedOut', user: null, accessToken: null, refreshToken: null, permissions: [], licensedModules: [] })
})

/** Opens an action's sheet and returns nothing: the confirm button is the last one with that name. */
const openAction = async (name: string) => fireEvent.press(await screen.findByRole('button', { name }))
const confirm = (name: string) => fireEvent.press(screen.getAllByRole('button', { name }).at(-1)!)

describe('Supplier detail', () => {
  it('shows contact, terms, ratings and items', async () => {
    signInAs('supplier:read')
    proc.supplier.mockResolvedValue({ id: 'x1', code: 'SUP-7', name: 'An Phat Plastics', contactPerson: 'Wei', phone: '+84', email: 'a@b.c', taxCode: '0301', paymentTermDays: 60, currencyCode: 'USD', isActive: true, address: { street: '1 Le Loi', city: 'HCMC', country: 'VN' } })
    proc.supplierRatings.mockResolvedValue({ averageRating: 4.5, totalRatings: 2, ratings: [{ id: 'r1', rating: 5, comment: 'Fast delivery' }] })
    proc.supplierItems.mockResolvedValue([{ id: 'i1', productId: 'abcdef12-0000', supplierItemCode: 'CAB-6', lastPurchasePrice: 12, leadTimeDays: 5, isPreferred: true }])
    renderScreen(<SupplierDetailScreen />)
    expect(await screen.findByText('An Phat Plastics')).toBeTruthy()
    expect(screen.getByLabelText('Payment terms: 60 days')).toBeTruthy()
    expect(screen.getByText('1 Le Loi, HCMC, VN')).toBeTruthy()
    expect(await screen.findByText('Average 4.5 of 5 (2 ratings)')).toBeTruthy()
    expect(screen.getByText('Fast delivery')).toBeTruthy()
    expect(await screen.findByText('CAB-6')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /approve|ship|send|pay/i })).toBeNull()
  })

  it('says "no access" on a 403', async () => {
    signInAs()
    proc.supplier.mockRejectedValue({ response: { status: 403, data: {} } })
    proc.supplierRatings.mockRejectedValue({ response: { status: 403, data: {} } })
    proc.supplierItems.mockRejectedValue({ response: { status: 403, data: {} } })
    renderScreen(<SupplierDetailScreen />)
    expect(await screen.findByText('You do not have access to this.')).toBeTruthy()
  })
})

describe('RFQ detail', () => {
  it('shows lines, invited suppliers and the quotations with the lowest and the awarded one', async () => {
    signInAs('rfq:read')
    proc.rfq.mockResolvedValue({
      id: 'x1', rfqNumber: 'RFQ-1', title: 'Cables', status: 'Sent', createdAt: '2026-11-01T00:00:00Z', neededBy: '2026-11-20T00:00:00Z',
      lines: [{ id: 'l1', productCode: 'C6', productName: 'Cable CAT6', quantity: 100, unit: 'pc' }],
      invitations: [{ supplierId: 's1', supplierName: 'Acme', state: 'Quoted' }, { supplierId: 's2', supplierName: 'Beta', state: 'Invited' }], quotations: [],
    })
    proc.rfqComparison.mockResolvedValue({ suppliers: [
      { supplierId: 's1', supplierName: 'Acme', quoteNumber: 'Q-1', currencyCode: 'USD', leadTimeDays: 5, validUntil: '', expired: false, isWinner: false, total: 900 },
      { supplierId: 's3', supplierName: 'Gamma', quoteNumber: 'Q-3', currencyCode: 'USD', leadTimeDays: 9, validUntil: '', expired: true, isWinner: false, total: 500 },
    ] })
    renderScreen(<RfqDetailScreen />)
    expect(await screen.findByText('RFQ-1')).toBeTruthy()
    expect(screen.getByText('Cable CAT6')).toBeTruthy()
    expect(screen.getByText('Invited')).toBeTruthy()
    expect(await screen.findByText(/Q-1 · 5 days lead time · lowest/)).toBeTruthy()
    expect(screen.getByText(/Q-3 · 9 days lead time · expired/)).toBeTruthy()
  })
})

const invoice = (over: object = {}) => ({
  id: 'x1', invoiceNumber: 'INV-9', supplierId: 's', supplierName: 'Acme', purchaseOrderId: 'po', poNumber: 'PO-1', invoiceDate: '2026-11-02T00:00:00Z', dueDate: '2026-12-02T00:00:00Z',
  currencyCode: 'USD', netAmount: 100, taxAmount: 10, totalAmount: 110, receivedValue: 100, status: 'Matched', createdAt: '2026-11-02T00:00:00Z',
  lines: [{ productCode: 'C6', quantity: 10, unitPrice: 10 }],
  matchLines: [{ productCode: 'C6', ordered: 10, received: 10, returned: 0, previouslyInvoiced: 0, invoiced: 10, orderPrice: 10, invoicedPrice: 10, priceVariancePercent: 0, quantityVariance: 0, verdict: 'Matched' }],
  ...over,
})

describe('Supplier invoice detail', () => {
  it('shows the totals and the three-way match, and offers approve and reject to someone who may', async () => {
    signInAs('supplier_invoice:approve', 'supplier_invoice:manage')
    proc.invoice.mockResolvedValue(invoice() as never)
    renderScreen(<InvoiceDetailScreen />)
    expect(await screen.findByText('INV-9')).toBeTruthy()
    expect(screen.getByText('Three-way match')).toBeTruthy()
    expect(screen.getByText(/ordered 10 · received 10/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Reject' })).toBeTruthy()
  })

  it('offers nothing to someone who may only read', async () => {
    signInAs('supplier_invoice:read')
    proc.invoice.mockResolvedValue(invoice() as never)
    renderScreen(<InvoiceDetailScreen />)
    await screen.findByText('INV-9')
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Reject' })).toBeNull()
  })

  it('approve asks first and runs only after the confirmation', async () => {
    signInAs('supplier_invoice:approve')
    proc.invoice.mockResolvedValue(invoice() as never)
    proc.approveInvoice.mockResolvedValue({} as never)
    renderScreen(<InvoiceDetailScreen />)
    await openAction('Approve')
    expect(proc.approveInvoice).not.toHaveBeenCalled()
    expect(screen.getByText('Approve invoice INV-9?')).toBeTruthy()
    confirm('Approve')
    await waitFor(() => expect(proc.approveInvoice).toHaveBeenCalledWith('x1'))
    await waitFor(() => expect(useToastStore.getState().items[0]?.message).toBe('Invoice INV-9 approved.'))
  })

  it('an invoice on hold shows why and its approval needs the variance permission and a reason', async () => {
    signInAs('supplier_invoice:approve', 'supplier_invoice:approve_variance')
    proc.invoice.mockResolvedValue(invoice({ status: 'OnHold', holdReasons: 'Price off on C6' }) as never)
    proc.approveInvoice.mockResolvedValue({} as never)
    renderScreen(<InvoiceDetailScreen />)
    expect(await screen.findByText('Price off on C6')).toBeTruthy()
    await openAction('Approve variance')
    confirm('Approve variance')
    expect(await screen.findByText('Enter a reason.')).toBeTruthy()
    expect(proc.approveInvoice).not.toHaveBeenCalled()
    fireEvent.changeText(screen.getByLabelText('Reason *'), 'Agreed with the supplier')
    confirm('Approve variance')
    await waitFor(() => expect(proc.approveInvoice).toHaveBeenCalledWith('x1', 'Agreed with the supplier'))
  })

  it('without the variance permission an invoice on hold cannot be approved', async () => {
    signInAs('supplier_invoice:approve')
    proc.invoice.mockResolvedValue(invoice({ status: 'OnHold', holdReasons: 'x' }) as never)
    renderScreen(<InvoiceDetailScreen />)
    await screen.findByText('INV-9')
    expect(screen.queryByRole('button', { name: 'Approve variance' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
  })

  it('reject needs a reason and sends it', async () => {
    signInAs('supplier_invoice:manage')
    proc.invoice.mockResolvedValue(invoice() as never)
    proc.rejectInvoice.mockResolvedValue({} as never)
    renderScreen(<InvoiceDetailScreen />)
    await openAction('Reject')
    confirm('Reject')
    expect(await screen.findByText('Enter a reason.')).toBeTruthy()
    fireEvent.changeText(screen.getByLabelText('Reason *'), '  wrong prices ')
    confirm('Reject')
    await waitFor(() => expect(proc.rejectInvoice).toHaveBeenCalledWith('x1', 'wrong prices'))
  })

  it('shows the server refusal inside the sheet and keeps it open', async () => {
    signInAs('supplier_invoice:approve')
    proc.invoice.mockResolvedValue(invoice() as never)
    proc.approveInvoice.mockRejectedValue({ response: { status: 400, data: { detail: 'The invoice is not matched.' } } })
    renderScreen(<InvoiceDetailScreen />)
    await openAction('Approve')
    confirm('Approve')
    expect(await screen.findByText('The invoice is not matched.')).toBeTruthy()
    expect(screen.getByText('Approve invoice INV-9?')).toBeTruthy()
  })
})

const vreturn = (over: object = {}) => ({ id: 'x1', returnNumber: 'RET-1', purchaseOrderId: 'po', poNumber: 'PO-1', supplierId: 's', supplierName: 'Acme', currencyCode: 'USD', reason: 'Damaged', status: 'Draft', value: 50, createdAt: '2026-11-01T00:00:00Z', creditAmount: 0, lines: [{ productCode: 'C6', productName: 'Cable', quantity: 5, unitPrice: 10 }], ...over })

describe('Vendor return detail', () => {
  it('a draft is shipped or cancelled after a confirmation', async () => {
    signInAs('vendor_return:manage')
    proc.vendorReturn.mockResolvedValue(vreturn() as never)
    proc.shipReturn.mockResolvedValue({} as never)
    renderScreen(<ReturnDetailScreen />)
    expect(await screen.findByText('RET-1')).toBeTruthy()
    expect(screen.getByText('Damaged')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Cancel return' })).toBeTruthy()
    await openAction('Ship')
    expect(proc.shipReturn).not.toHaveBeenCalled()
    confirm('Ship')
    await waitFor(() => expect(proc.shipReturn).toHaveBeenCalledWith('x1'))
  })

  it('recording the credit asks for the credit note and an amount, refusing a bad one', async () => {
    signInAs('vendor_return:manage')
    proc.vendorReturn.mockResolvedValue(vreturn({ status: 'Shipped' }) as never)
    proc.creditReturn.mockResolvedValue({} as never)
    renderScreen(<ReturnDetailScreen />)
    await openAction('Record credit')
    expect((screen.getByLabelText('Credit amount *') as { props: { value: string } }).props.value).toBe('50')
    confirm('Record credit')
    expect(await screen.findByText('This field is required.')).toBeTruthy()
    fireEvent.changeText(screen.getByLabelText('Credit note number *'), 'CN-7')
    fireEvent.changeText(screen.getByLabelText('Credit amount *'), '-3')
    confirm('Record credit')
    expect(await screen.findByText('Enter a number of 0 or more.')).toBeTruthy()
    expect(proc.creditReturn).not.toHaveBeenCalled()
    fireEvent.changeText(screen.getByLabelText('Credit amount *'), '45,5')
    confirm('Record credit')
    await waitFor(() => expect(proc.creditReturn).toHaveBeenCalledWith('x1', 'CN-7', 45.5))
  })

  it('a credited return offers nothing', async () => {
    signInAs('vendor_return:manage')
    proc.vendorReturn.mockResolvedValue(vreturn({ status: 'Credited', creditedAt: '2026-11-03T00:00:00Z', creditNoteNumber: 'CN-1', creditAmount: 50 }) as never)
    renderScreen(<ReturnDetailScreen />)
    await screen.findByText('RET-1')
    expect(screen.queryByRole('button', { name: 'Record credit' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Ship' })).toBeNull()
  })
})

const quotation = (over: object = {}) => ({ quotationId: 'x1', quotationNo: 'QT-7', customerId: 'c', customerName: 'Binh', customerEmail: 'b@x.io', status: 'Draft', totalAmount: 900, validUntil: '2026-12-01T00:00:00Z', lineItems: [{ productId: 'p', productName: 'Shirt', quantity: 3, unitPrice: 300 }], ...over })

describe('Quotation detail', () => {
  it('a draft is sent after a confirmation', async () => {
    signInAs('sales_order:update')
    sales.quotation.mockResolvedValue(quotation() as never)
    sales.sendQuotation.mockResolvedValue({})
    renderScreen(<QuotationDetailScreen />)
    expect(await screen.findByText('QT-7')).toBeTruthy()
    expect(screen.getByText('Shirt')).toBeTruthy()
    await openAction('Send')
    expect(sales.sendQuotation).not.toHaveBeenCalled()
    confirm('Send')
    await waitFor(() => expect(sales.sendQuotation).toHaveBeenCalledWith('x1'))
  })

  it('a sent quotation is accepted or rejected', async () => {
    signInAs('sales_order:update')
    sales.quotation.mockResolvedValue(quotation({ status: 'Sent' }) as never)
    sales.rejectQuotation.mockResolvedValue({})
    renderScreen(<QuotationDetailScreen />)
    expect(await screen.findByRole('button', { name: 'Accepted' })).toBeTruthy()
    await openAction('Reject')
    confirm('Reject')
    await waitFor(() => expect(sales.rejectQuotation).toHaveBeenCalledWith('x1'))
  })

  it('an accepted quotation is converted with a shipping address, every line required', async () => {
    signInAs('sales_order:create')
    sales.quotation.mockResolvedValue(quotation({ status: 'Accepted' }) as never)
    sales.convertQuotation.mockResolvedValue({})
    renderScreen(<QuotationDetailScreen />)
    await openAction('Convert to order')
    confirm('Convert to order')
    expect((await screen.findAllByText('This field is required.')).length).toBe(5)
    expect(sales.convertQuotation).not.toHaveBeenCalled()
    for (const [label, value] of [['Street', '1 Le Loi'], ['City', 'HCMC'], ['State / province', 'HCM'], ['Postal code', '700000'], ['Country', 'Vietnam']])
      fireEvent.changeText(screen.getByLabelText(`${label} *`), value)
    confirm('Convert to order')
    await waitFor(() => expect(sales.convertQuotation).toHaveBeenCalledWith('x1', { street: '1 Le Loi', city: 'HCMC', state: 'HCM', postalCode: '700000', country: 'Vietnam' }))
  })

  it('a converted quotation links to its order and offers nothing', async () => {
    signInAs('sales_order:update', 'sales_order:create')
    sales.quotation.mockResolvedValue(quotation({ status: 'Converted', convertedOrderId: 'o9' }) as never)
    renderScreen(<QuotationDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Open the order' }))
    expect(mockPush).toHaveBeenCalledWith('/sales/o9')
    expect(screen.queryByRole('button', { name: 'Send' })).toBeNull()
  })
})

const slip = (over: object = {}) => ({
  id: 'x1', paySlipNumber: 'PS-1', employeeId: 'e1', year: 2026, month: 10, grossSalary: 20000000, deductions: 0, overtimeHours: 4, overtimePay: 600000, netSalary: 17000000, paymentStatus: 'Pending',
  baseSalary: 18000000, allowances: 2000000, workedDays: 21, standardDays: 22, employeeSocial: 1600000, employeeHealth: 300000, employeeUnemployment: 200000,
  employerSocial: 3000000, employerHealth: 500000, employerUnemployment: 200000, taxableIncome: 12000000, personalIncomeTax: 900000, dependentCount: 2, ...over,
})

describe('Pay slip detail', () => {
  it('shows the calculation and the net amount with the employee name', async () => {
    signInAs('hrm:payroll:manage', 'hrm:employees:view')
    hr.paySlip.mockResolvedValue(slip() as never)
    renderScreen(<PaySlipDetailScreen />)
    expect(await screen.findByText('Nguyen An')).toBeTruthy()
    expect(screen.getByLabelText(/Worked days: 21 \/ 22/)).toBeTruthy()
    expect(screen.getByLabelText(/Taxable income \(2 dependents\)/)).toBeTruthy()
    expect(screen.getByLabelText(/Insurance paid by the company/)).toBeTruthy()
  })

  it('a slip entered by hand says so and shows only the totals', async () => {
    signInAs('hrm:payroll:manage')
    hr.paySlip.mockResolvedValue(slip({ baseSalary: 0, workedDays: 0, deductions: 100000 }) as never)
    renderScreen(<PaySlipDetailScreen />)
    expect(await screen.findByText('This slip was entered by hand: only the totals are known.')).toBeTruthy()
    expect(screen.queryByLabelText(/Social insurance/)).toBeNull()
  })

  it('pay asks first, with an optional method, and runs after the confirmation', async () => {
    signInAs('hrm:payroll:manage')
    hr.paySlip.mockResolvedValue(slip() as never)
    hr.payPaySlip.mockResolvedValue({})
    renderScreen(<PaySlipDetailScreen />)
    await openAction('Pay')
    expect(hr.payPaySlip).not.toHaveBeenCalled()
    fireEvent.changeText(screen.getByLabelText('Payment method (optional)'), 'Bank transfer')
    confirm('Pay')
    await waitFor(() => expect(hr.payPaySlip).toHaveBeenCalledWith('x1', 'Bank transfer'))
  })

  it('cancel needs a reason', async () => {
    signInAs('hrm:payroll:manage')
    hr.paySlip.mockResolvedValue(slip() as never)
    hr.cancelPaySlip.mockResolvedValue({})
    renderScreen(<PaySlipDetailScreen />)
    await openAction('Cancel slip')
    confirm('Cancel slip')
    expect(await screen.findByText('Enter a reason.')).toBeTruthy()
    fireEvent.changeText(screen.getByLabelText('Reason *'), 'wrong month')
    confirm('Cancel slip')
    await waitFor(() => expect(hr.cancelPaySlip).toHaveBeenCalledWith('x1', 'wrong month'))
  })

  it('a paid slip offers nothing', async () => {
    signInAs('hrm:payroll:manage')
    hr.paySlip.mockResolvedValue(slip({ paymentStatus: 'Paid' }) as never)
    renderScreen(<PaySlipDetailScreen />)
    await screen.findAllByText('PS-1')
    expect(screen.queryByRole('button', { name: 'Pay' })).toBeNull()
  })
})

const profile = (over: object = {}) => ({ customerId: '12345678-0000-0000-0000-000000000001', customerName: 'Ann Tran', creditLimit: 1000, currentBalance: 950, availableCredit: 50, updatedAt: '2026-11-03T00:00:00Z', ...over })

describe('Credit limits', () => {
  it('the list shows the name, limit, balance, what is available and a word for the level', async () => {
    signInAs('sales_order:read')
    sales.creditLimits.mockResolvedValue({ items: [profile(), profile({ customerId: 'b', customerName: '', creditLimit: 500, currentBalance: 600, availableCredit: -100 })], total: 2 } as never)
    renderScreen(<CreditLimitsScreen />)
    expect(await screen.findByText('Ann Tran')).toBeTruthy()
    expect(screen.getByText('Low')).toBeTruthy()
    expect(screen.getByText('Over the limit')).toBeTruthy()
    expect(screen.getByText('Available: $50.00')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: /Ann Tran/ }))
    expect(mockPush).toHaveBeenCalledWith('/sales/credit/12345678-0000-0000-0000-000000000001')
  })

  it('an empty list explains that customers without a limit are not capped', async () => {
    signInAs('sales_order:read')
    sales.creditLimits.mockResolvedValue({ items: [], total: 0 })
    renderScreen(<CreditLimitsScreen />)
    expect(await screen.findByText('No customer has a credit limit.')).toBeTruthy()
  })

  it('the detail shows the figures and changing the limit asks first, validates and sends the number', async () => {
    signInAs('sales_order:read', 'sales_order:approve')
    mockParams = { customerId: profile().customerId }
    sales.creditLimit.mockResolvedValue(profile() as never)
    sales.setCreditLimit.mockResolvedValue(profile() as never)
    renderScreen(<CreditLimitDetailScreen />)
    expect(await screen.findByText('Ann Tran')).toBeTruthy()
    expect(screen.getByLabelText('Available: $50.00')).toBeTruthy()
    await openAction('Change limit')
    expect(sales.setCreditLimit).not.toHaveBeenCalled()
    expect((screen.getByLabelText('New credit limit *') as { props: { value: string } }).props.value).toBe('1000')
    fireEvent.changeText(screen.getByLabelText('New credit limit *'), 'abc')
    confirm('Change limit')
    expect(await screen.findByText('Enter a number of 0 or more.')).toBeTruthy()
    fireEvent.changeText(screen.getByLabelText('New credit limit *'), '2500')
    confirm('Change limit')
    await waitFor(() => expect(sales.setCreditLimit).toHaveBeenCalledWith('12345678-0000-0000-0000-000000000001', 2500))
  })

  it('without sales_order:approve there is no Change limit button', async () => {
    signInAs('sales_order:read')
    mockParams = { customerId: profile().customerId }
    sales.creditLimit.mockResolvedValue(profile() as never)
    renderScreen(<CreditLimitDetailScreen />)
    await screen.findByText('Ann Tran')
    expect(screen.queryByRole('button', { name: 'Change limit' })).toBeNull()
  })

  it('a customer without a profile shows "no limit set"', async () => {
    signInAs('sales_order:read', 'sales_order:approve')
    mockParams = { customerId: 'z' }
    sales.creditLimit.mockResolvedValue(null)
    renderScreen(<CreditLimitDetailScreen />)
    expect(await screen.findByText('No credit limit is set for this customer.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Change limit' })).toBeNull()
  })
})
