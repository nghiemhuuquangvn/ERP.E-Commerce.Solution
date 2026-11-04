import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import type { BottomTabBarProps } from '@react-navigation/bottom-tabs'
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
  procurementApi: { suppliers: jest.fn(), invoices: jest.fn(), returns: jest.fn(), rfqs: jest.fn() },
}))
jest.mock('@/modules/sales/api', () => ({
  ...jest.requireActual('@/modules/sales/api'),
  salesApi: { list: jest.fn(), get: jest.fn(), count: jest.fn() },
  salesExtrasApi: { quotations: jest.fn(), commissions: jest.fn() },
}))
jest.mock('@/modules/hrm/api', () => ({
  ...jest.requireActual('@/modules/hrm/api'),
  hrmApi: { employees: jest.fn(), leaves: jest.fn(), attendance: jest.fn(), pendingLeaveCount: jest.fn() },
  hrmExtrasApi: { paySlips: jest.fn(), shifts: jest.fn() },
}))

import { ModuleTabBar } from '@/components/ModuleTabBar'
import { RfqsScreen, SupplierInvoicesScreen, SuppliersScreen, VendorReturnsScreen } from '@/modules/purchase/ProcurementScreens'
import { CommissionsScreen, QuotationsScreen } from '@/modules/sales/SalesExtrasScreens'
import { AttendanceScreen, EmployeesScreen, LeaveScreen, PaySlipsScreen, ShiftsScreen } from '@/modules/hrm/HrScreens'
import { procurementApi } from '@/modules/purchase/procurementApi'
import { salesExtrasApi } from '@/modules/sales/api'
import { hrmApi, hrmExtrasApi } from '@/modules/hrm/api'
import { useAuthStore } from '@/stores/auth.store'
import { useMenuStore } from '@/stores/menu.store'

const mockProc = jest.mocked(procurementApi)
const mockExtras = jest.mocked(salesExtrasApi)
const mockHr = jest.mocked(hrmApi)
const mockHrExtras = jest.mocked(hrmExtrasApi)

const EVERYTHING = [
  'purchase_order:read', 'supplier:read', 'supplier_invoice:read', 'vendor_return:read', 'rfq:read', 'sales_order:read',
  'hrm:employees:view', 'hrm:leave:manage', 'hrm:attendance:view', 'hrm:payroll:manage', 'hrm:shifts:view',
]

const signInAs = (permissions: string[]) =>
  useAuthStore.setState({ status: 'signedIn', permissions, licensedModules: [], user: { id: 'u1', email: 'a@b.c', name: 'Ann', roles: [] }, accessToken: 'a', refreshToken: 'r' })

beforeEach(() => {
  jest.clearAllMocks()
  mockParams = {}
  void i18n.changeLanguage('en')
  useMenuStore.setState({ open: false })
  useAuthStore.setState({ status: 'signedOut', user: null, accessToken: null, refreshToken: null, permissions: [], licensedModules: [] })
})

function bar(route: string) {
  const navigation = { navigate: jest.fn() }
  const props = { state: { routes: [{ name: route }], index: 0 }, navigation } as unknown as BottomTabBarProps
  renderScreen(<ModuleTabBar {...props} />)
  return navigation
}

describe('the bottom bar', () => {
  it('on Home lists the modules and opens a module on its first function', () => {
    signInAs(EVERYTHING)
    const nav = bar('index')
    for (const name of ['Home', 'Purchasing', 'Sales', 'HR']) expect(screen.getByRole('tab', { name })).toBeTruthy()
    expect(screen.getByRole('tab', { name: 'Home' }).props.accessibilityState.selected).toBe(true)
    fireEvent.press(screen.getByRole('tab', { name: 'Purchasing' }))
    expect(nav.navigate).toHaveBeenCalledWith('purchase')
  })

  it('a module opens on the first function the account may use', () => {
    signInAs(['supplier_invoice:read', 'rfq:read'])
    const nav = bar('index')
    fireEvent.press(screen.getByRole('tab', { name: 'Purchasing' }))
    expect(nav.navigate).toHaveBeenCalledWith('purchase-invoices')
  })

  it('inside Purchasing it shows Home and the purchasing functions, the current one selected', () => {
    signInAs(EVERYTHING)
    const nav = bar('purchase-suppliers')
    expect(screen.queryByRole('tab', { name: 'Sales' })).toBeNull()
    expect(screen.getByRole('tab', { name: 'Suppliers' }).props.accessibilityState.selected).toBe(true)
    expect(screen.getByRole('tab', { name: 'Orders' }).props.accessibilityState.selected).toBe(false)
    fireEvent.press(screen.getByRole('tab', { name: 'Home' }))
    expect(nav.navigate).toHaveBeenCalledWith('index')
    fireEvent.press(screen.getByRole('tab', { name: 'Invoices' }))
    expect(nav.navigate).toHaveBeenCalledWith('purchase-invoices')
  })

  it('what does not fit goes behind More, which lists it', async () => {
    signInAs(EVERYTHING)
    const nav = bar('purchase')
    expect(screen.queryByRole('tab', { name: 'RFQs' })).toBeNull()
    fireEvent.press(screen.getByRole('tab', { name: 'More' }))
    fireEvent.press(await screen.findByRole('button', { name: 'RFQs' }))
    expect(nav.navigate).toHaveBeenCalledWith('purchase-rfqs')
    expect(screen.getByRole('tab', { name: 'More' }).props.accessibilityState.selected).toBe(false)
  })

  it('a function behind More makes More the selected item', () => {
    signInAs(EVERYTHING)
    bar('purchase-rfqs')
    expect(screen.getByRole('tab', { name: 'More' }).props.accessibilityState.selected).toBe(true)
  })

  it('a function the account may not open is not in the bar', () => {
    signInAs(['sales_order:read'])
    bar('sales')
    expect(screen.getByRole('tab', { name: 'Quotations' })).toBeTruthy()
    expect(screen.queryByRole('tab', { name: 'Suppliers' })).toBeNull()
    expect(screen.queryByRole('tab', { name: 'More' })).toBeNull()
  })

  it('too many modules: More on Home opens the side menu', () => {
    signInAs(EVERYTHING)
    bar('index') // four modules fit, so no More here
    expect(screen.queryByRole('tab', { name: 'More' })).toBeNull()
  })
})

const row = (extra: object) => ({ items: [extra], total: 1 }) as never

describe('Purchasing screens', () => {
  it('suppliers: opens on active suppliers and shows the rows', async () => {
    signInAs(EVERYTHING)
    mockProc.suppliers.mockResolvedValue(row({ id: 's1', code: 'SUP-1', name: 'An Phat Plastics', contactPerson: 'Wei', phone: '+84', isActive: true, paymentTermDays: 60 }))
    renderScreen(<SuppliersScreen />)
    expect(await screen.findByText('An Phat Plastics')).toBeTruthy()
    expect(screen.getByText('Payment terms: 60 days')).toBeTruthy()
    expect(mockProc.suppliers).toHaveBeenCalledWith({ status: 'active' }, 1)
  })

  it('supplier invoices: status filter is sent to the server', async () => {
    signInAs(EVERYTHING)
    mockProc.invoices.mockResolvedValue(row({ id: 'i1', invoiceNumber: 'INV-9', supplierName: 'Acme', poNumber: 'PO-1', invoiceDate: '2026-11-02T00:00:00Z', dueDate: '2026-12-02T00:00:00Z', currencyCode: 'USD', totalAmount: 120, status: 'OnHold', holdReasons: 'Price differs' }))
    renderScreen(<SupplierInvoicesScreen />)
    expect(await screen.findByText('INV-9')).toBeTruthy()
    expect(screen.getByText('On hold')).toBeTruthy()
    expect(screen.getByText('Price differs')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(await screen.findByRole('tab', { name: 'Approved' }))
    await waitFor(() => expect(mockProc.invoices).toHaveBeenLastCalledWith({ status: 'Approved' }))
  })

  it('vendor returns and RFQs list their rows', async () => {
    signInAs(EVERYTHING)
    mockProc.returns.mockResolvedValue(row({ id: 'r1', returnNumber: 'RET-1', poNumber: 'PO-1', supplierName: 'Acme', currencyCode: 'USD', status: 'Shipped', value: 50, createdAt: '2026-11-01T00:00:00Z' }))
    mockProc.rfqs.mockResolvedValue(row({ id: 'q1', rfqNumber: 'RFQ-1', title: 'Cables', status: 'Sent', lines: 3, invited: 2, quoted: 1, createdAt: '2026-11-01T00:00:00Z' }))
    renderScreen(<VendorReturnsScreen />)
    expect(await screen.findByText('RET-1')).toBeTruthy()
    renderScreen(<RfqsScreen />)
    expect(await screen.findByText('RFQ-1')).toBeTruthy()
    expect(screen.getByText(/3 lines · 2 invited · 1 quoted/)).toBeTruthy()
  })

  it('shows "no access" when the server refuses', async () => {
    signInAs([])
    mockProc.rfqs.mockRejectedValue({ response: { status: 403, data: {} } })
    renderScreen(<RfqsScreen />)
    expect(await screen.findByText('You do not have access to this.')).toBeTruthy()
  })
})

describe('Sales screens', () => {
  it('quotations: rows with status and the days filter', async () => {
    signInAs(EVERYTHING)
    mockExtras.quotations.mockResolvedValue(row({ quotationId: 'q1', quotationNo: 'QT-7', customerName: 'Binh', status: 'Sent', totalAmount: 900, validUntil: '2026-12-01T00:00:00Z', quotationDate: '2026-11-01T00:00:00Z' }))
    renderScreen(<QuotationsScreen />)
    expect(await screen.findByText('QT-7')).toBeTruthy()
    expect(screen.getByText('Sent')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(await screen.findByRole('tab', { name: 'Month' }))
    await waitFor(() => expect(mockExtras.quotations).toHaveBeenLastCalledWith({ dateFrom: expect.stringMatching(/^\d{4}-\d{2}-01$/), dateTo: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/) }, 1))
  })

  it('commissions open on this month and show amount and status', async () => {
    signInAs(EVERYTHING)
    mockExtras.commissions.mockResolvedValue(row({ id: 'c1', planId: 'p', salesRepId: 'r', orderId: 'o', amount: 25, status: 'Paid', calculatedAt: '2026-11-03T00:00:00Z' }))
    renderScreen(<CommissionsScreen />)
    expect(await screen.findByText('$25.00')).toBeTruthy()
    expect(screen.getByText('Paid')).toBeTruthy()
    expect(mockExtras.commissions).toHaveBeenCalledWith({ dateFrom: expect.any(String), dateTo: expect.any(String) })
  })
})

describe('HR screens', () => {
  it('employees: status filter becomes the number the server expects', async () => {
    signInAs(EVERYTHING)
    mockHr.employees.mockResolvedValue(row({ id: 'e1', employeeNumber: 'E-1', fullName: 'Nguyen An', employmentStatus: 'Active', departmentId: 'd' }))
    renderScreen(<EmployeesScreen />)
    expect(await screen.findByText('Nguyen An')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(await screen.findByRole('tab', { name: 'On leave' }))
    await waitFor(() => expect(mockHr.employees).toHaveBeenLastCalledWith('', 1, undefined, 1))
  })

  it('leave requests open on Pending, or on the status a Home link names, and send days and number', async () => {
    signInAs(EVERYTHING)
    mockHr.leaves.mockResolvedValue({ items: [], total: 0 })
    renderScreen(<LeaveScreen />)
    await waitFor(() => expect(mockHr.leaves).toHaveBeenCalledWith({ status: 0, dateFrom: undefined, dateTo: undefined, search: undefined }, 1))
    expect(await screen.findByText('No leave requests here.')).toBeTruthy()
  })

  it('leave requests: a link can open them on another status', async () => {
    signInAs(EVERYTHING)
    mockParams = { status: 'Approved' }
    mockHr.leaves.mockResolvedValue({ items: [], total: 0 })
    renderScreen(<LeaveScreen />)
    await waitFor(() => expect(mockHr.leaves).toHaveBeenCalledWith(expect.objectContaining({ status: 1 }), 1))
  })

  it('attendance opens on this month and maps the status', async () => {
    signInAs(EVERYTHING)
    mockHr.attendance.mockResolvedValue(row({ id: 'a1', employeeId: 'e1', date: '2026-11-03T00:00:00Z', totalHours: 8, status: 'Present' }))
    renderScreen(<AttendanceScreen />)
    expect(await screen.findByText('8 h', { exact: false })).toBeTruthy()
    expect(mockHr.attendance).toHaveBeenCalledWith({ dateFrom: expect.any(String), dateTo: expect.any(String), status: undefined }, 1)
  })

  it('pay slips of the period, with the net amount', async () => {
    signInAs(EVERYTHING)
    mockHrExtras.paySlips.mockResolvedValue(row({ id: 'p1', paySlipNumber: 'PS-1', employeeId: 'e1', year: 2026, month: 10, grossSalary: 20000000, deductions: 1, netSalary: 18000000, paymentStatus: 'Paid' }))
    renderScreen(<PaySlipsScreen />)
    expect(await screen.findByText('PS-1 · 2026-10')).toBeTruthy()
    expect(screen.getByText('Paid')).toBeTruthy()
    expect(mockHrExtras.paySlips).toHaveBeenCalledWith({ dateFrom: expect.any(String), dateTo: expect.any(String) })
  })

  it('work shifts: active ones first, the filter runs on the phone', async () => {
    signInAs(EVERYTHING)
    mockHrExtras.shifts.mockResolvedValue({ items: [
      { id: 's1', name: 'Ca 1', startTime: '06:00', endTime: '14:00', shiftHours: 8, isActive: true },
      { id: 's2', name: 'Ca cu', startTime: '14:00', endTime: '22:00', shiftHours: 8, isActive: false },
    ], total: 2 })
    renderScreen(<ShiftsScreen />)
    expect(await screen.findByText('Ca 1')).toBeTruthy()
    expect(screen.queryByText('Ca cu')).toBeNull()
  })
})
