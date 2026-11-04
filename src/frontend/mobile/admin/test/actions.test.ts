import { invoiceActions, returnActions } from '@/modules/purchase/actions'
import { lowestTotal } from '@/modules/purchase/PurchaseDetailScreens'
import { quotationActions } from '@/modules/sales/api'
import { paySlipActions, type PaySlip } from '@/modules/hrm/api'
import { employerCost, isCalculated } from '@/modules/hrm/PaySlipDetailScreen'
import { creditLevel, displayName } from '@/modules/sales/CreditLimitScreens'
import { parseAmount } from '@/components/ApprovalBar'

const holds = (...p: string[]) => (x: string) => p.includes(x)

describe('supplier invoice actions (status + permission)', () => {
  const all = holds('supplier_invoice:approve', 'supplier_invoice:approve_variance', 'supplier_invoice:manage')

  it('a matched invoice can be approved or rejected', () => {
    expect(invoiceActions('Matched', all)).toEqual(['approve', 'reject'])
  })

  it('an invoice on hold needs the variance permission to be approved; reject only needs manage', () => {
    expect(invoiceActions('OnHold', all)).toEqual(['approveVariance', 'reject'])
    expect(invoiceActions('OnHold', holds('supplier_invoice:approve', 'supplier_invoice:manage'))).toEqual(['reject'])
    expect(invoiceActions('OnHold', holds('supplier_invoice:approve_variance'))).toEqual([])
  })

  it('a recorded invoice can only be rejected; approved and rejected ones offer nothing', () => {
    expect(invoiceActions('Recorded', all)).toEqual(['reject'])
    expect(invoiceActions('Approved', all)).toEqual([])
    expect(invoiceActions('Rejected', all)).toEqual([])
  })

  it('without permissions nothing is offered', () => {
    expect(invoiceActions('Matched', holds())).toEqual([])
    expect(invoiceActions('Matched', holds('supplier_invoice:approve'))).toEqual(['approve'])
    expect(invoiceActions('Matched', holds('supplier_invoice:manage'))).toEqual(['reject'])
  })
})

describe('vendor return actions', () => {
  it('a draft is shipped or cancelled, a shipped one credited, the rest nothing', () => {
    const manage = holds('vendor_return:manage')
    expect(returnActions('Draft', manage)).toEqual(['ship', 'cancel'])
    expect(returnActions('Shipped', manage)).toEqual(['credit'])
    expect(returnActions('Credited', manage)).toEqual([])
    expect(returnActions('Cancelled', manage)).toEqual([])
    expect(returnActions('Draft', holds('vendor_return:read'))).toEqual([])
  })
})

describe('quotation actions', () => {
  it('draft: send; sent: accept or reject; accepted: convert', () => {
    const all = holds('sales_order:update', 'sales_order:create')
    expect(quotationActions('Draft', all)).toEqual(['send'])
    expect(quotationActions('Sent', all)).toEqual(['accept', 'reject'])
    expect(quotationActions('Accepted', all)).toEqual(['convert'])
    expect(quotationActions('Converted', all)).toEqual([])
    expect(quotationActions('Expired', all)).toEqual([])
  })

  it('converting needs create, the other steps need update', () => {
    expect(quotationActions('Accepted', holds('sales_order:update'))).toEqual([])
    expect(quotationActions('Sent', holds('sales_order:create'))).toEqual([])
    expect(quotationActions('Draft', holds('sales_order:read'))).toEqual([])
  })
})

describe('pay slip actions', () => {
  it('only a pending slip can be paid or cancelled, and only with the payroll permission', () => {
    expect(paySlipActions('Pending', holds('hrm:payroll:manage'))).toEqual(['pay', 'cancel'])
    expect(paySlipActions('Paid', holds('hrm:payroll:manage'))).toEqual([])
    expect(paySlipActions('Cancelled', holds('hrm:payroll:manage'))).toEqual([])
    expect(paySlipActions('Pending', holds())).toEqual([])
  })
})

describe('small rules of the detail screens', () => {
  it('amounts: a non-negative number, a comma works as the decimal point, anything else is refused', () => {
    expect(parseAmount('1250.5')).toBe(1250.5)
    expect(parseAmount(' 1250,5 ')).toBe(1250.5)
    expect(parseAmount('0')).toBe(0)
    expect(parseAmount('-1')).toBeNull()
    expect(parseAmount('')).toBeNull()
    expect(parseAmount('abc')).toBeNull()
    expect(parseAmount('1.250,5')).toBeNull()
  })

  it('the lowest quotation ignores expired ones', () => {
    const s = (total: number, expired = false) => ({ supplierId: String(total), supplierName: '', quoteNumber: '', currencyCode: 'USD', leadTimeDays: 1, validUntil: '', expired, isWinner: false, total })
    expect(lowestTotal({ suppliers: [s(300), s(120, true), s(200)] })).toBe(200)
    expect(lowestTotal({ suppliers: [s(120, true)] })).toBeNull()
    expect(lowestTotal(undefined)).toBeNull()
  })

  it('credit level: over when nothing is left, low under a tenth, otherwise fine', () => {
    expect(creditLevel({ creditLimit: 1000, availableCredit: -5 })).toBe('over')
    expect(creditLevel({ creditLimit: 1000, availableCredit: 0 })).toBe('over')
    expect(creditLevel({ creditLimit: 1000, availableCredit: 99 })).toBe('low')
    expect(creditLevel({ creditLimit: 1000, availableCredit: 100 })).toBe('ok')
    expect(creditLevel({ creditLimit: 0, availableCredit: 50 })).toBe('ok')
  })

  it('a customer with no order name is shown by the start of the id', () => {
    expect(displayName({ customerName: 'Ann', customerId: '12345678-aaaa' })).toBe('Ann')
    expect(displayName({ customerName: '', customerId: '12345678-aaaa' })).toBe('12345678')
  })

  it('a pay slip is calculated when it has a base salary or worked days; the employer cost adds the three insurances', () => {
    const slip = (extra: Partial<PaySlip>): PaySlip => ({ id: 'p', paySlipNumber: 'PS-1', employeeId: 'e', year: 2026, month: 10, grossSalary: 0, deductions: 0, netSalary: 0, paymentStatus: 'Pending', ...extra })
    expect(isCalculated(slip({ baseSalary: 10 }))).toBe(true)
    expect(isCalculated(slip({ workedDays: 20 }))).toBe(true)
    expect(isCalculated(slip({}))).toBe(false)
    expect(employerCost(slip({ employerSocial: 100, employerHealth: 20, employerUnemployment: 10 }))).toBe(130)
    expect(employerCost(slip({}))).toBe(0)
  })
})
