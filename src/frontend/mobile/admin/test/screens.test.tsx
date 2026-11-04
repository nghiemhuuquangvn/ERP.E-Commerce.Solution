import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'
import { makeToken } from './token'

const mockPush = jest.fn()
let mockParams: Record<string, string> = {}
jest.mock('expo-router', () => ({
  useRouter: () => ({ push: mockPush, replace: jest.fn() }),
  useLocalSearchParams: () => mockParams,
  Stack: { Screen: () => null },
}))

// Factories create the mocks (a top-level const is not set yet when an import first needs the module); the tests reach them below.
jest.mock('@/api/auth', () => ({ ADMIN_USER_TYPE: 2, authApi: { login: jest.fn(), verifyTwoFactor: jest.fn(), resendTwoFactor: jest.fn(), refresh: jest.fn(), logout: jest.fn() } }))
jest.mock('@/api/client', () => ({ isNetworkError: (e: { response?: unknown }) => !e?.response, apiClient: {}, gatewayClient: {} }))
jest.mock('@/modules/purchase/api', () => ({ ...jest.requireActual('@/modules/purchase/api'), purchaseApi: { list: jest.fn(), get: jest.fn(), count: jest.fn(), approve: jest.fn(), approveFinance: jest.fn(), reject: jest.fn(), cancel: jest.fn() } }))
jest.mock('@/modules/sales/api', () => ({ ...jest.requireActual('@/modules/sales/api'), salesApi: { list: jest.fn(), get: jest.fn(), count: jest.fn(), approve: jest.fn(), reject: jest.fn(), cancel: jest.fn() } }))
jest.mock('@/modules/hrm/api', () => ({ ...jest.requireActual('@/modules/hrm/api'), hrmApi: { employees: jest.fn(), employee: jest.fn(), leaves: jest.fn(), leave: jest.fn(), pendingLeaveCount: jest.fn(), approveLeave: jest.fn(), rejectLeave: jest.fn(), cancelLeave: jest.fn(), attendance: jest.fn() } }))
jest.mock('@/modules/dashboard/api', () => ({ dashboardApi: { sales: jest.fn() } }))

import LoginScreen from '@/app/(auth)/login'
import PurchaseListScreen from '@/modules/purchase/PurchaseListScreen'
import PurchaseDetailScreen from '@/modules/purchase/PurchaseDetailScreen'
import SalesDetailScreen from '@/modules/sales/SalesDetailScreen'
import LeaveDetailScreen from '@/modules/hrm/LeaveDetailScreen'
import DashboardScreen from '@/modules/dashboard/DashboardScreen'
import { useAuthStore } from '@/stores/auth.store'
import { useToastStore } from '@/components/ui/Toast'
import { authApi } from '@/api/auth'
import { purchaseApi, type PurchaseOrder } from '@/modules/purchase/api'
import { salesApi } from '@/modules/sales/api'
import { hrmApi } from '@/modules/hrm/api'
import { dashboardApi } from '@/modules/dashboard/api'

const mockAuth = jest.mocked(authApi)
const mockPo = jest.mocked(purchaseApi)
const mockSo = jest.mocked(salesApi)
const mockHr = jest.mocked(hrmApi)
const mockDash = jest.mocked(dashboardApi)

const signInAs = (permissions: string[]) =>
  useAuthStore.setState({ status: 'signedIn', permissions, licensedModules: [], user: { id: 'u1', email: 'a@b.c', name: 'Ann', roles: [] }, accessToken: 'a', refreshToken: 'r' })

const po = (over: Partial<PurchaseOrder> = {}): PurchaseOrder => ({
  id: 'po1', poNumber: 'PO-00123', supplier: { id: 's', name: 'Acme Ltd' }, status: 'SUBMITTED', orderDate: '2026-11-02T00:00:00Z',
  expectedDeliveryDate: '2026-11-10T00:00:00Z', totalAmount: 4200, currency: 'USD', notes: 'Dock B',
  items: [{ id: 'i1', productCode: 'C6', productName: 'Cable CAT6', quantity: 10, unit: 'pc', unitPrice: 12, totalPrice: 120 }], ...over,
})

beforeEach(() => {
  jest.clearAllMocks()
  mockParams = {}
  useAuthStore.setState({ status: 'signedOut', user: null, accessToken: null, refreshToken: null, permissions: [], licensedModules: [] })
  useToastStore.setState({ items: [] })
})

describe('Sign in', () => {
  it('asks for both fields and sends nothing when they are empty', () => {
    renderScreen(<LoginScreen />)
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    expect(screen.getAllByText('This field is required.')).toHaveLength(2)
    expect(mockAuth.login).not.toHaveBeenCalled()
  })

  it('signs in as an admin user and stores the session', async () => {
    mockAuth.login.mockResolvedValue({ accessToken: makeToken({ sub: 'u1', email: 'a@b.c', name: 'Ann', permission: ['sales_order:read'] }), refreshToken: 'r1' })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'secret123')
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    await waitFor(() => expect(useAuthStore.getState().status).toBe('signedIn'))
    expect(mockAuth.login).toHaveBeenCalledWith('a@b.c', 'secret123')
  })

  it('goes through the two-step code when the account has it', async () => {
    mockAuth.login.mockResolvedValue({ requiresTwoFactor: true, challengeId: 'c1', twoFactorMethod: 'Email' })
    mockAuth.verifyTwoFactor.mockResolvedValue({ accessToken: makeToken({ sub: 'u1' }), refreshToken: 'r1' })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'secret123')
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    fireEvent.changeText(await screen.findByLabelText('Code *'), '48a1b2c3d4')
    fireEvent.press(screen.getByRole('button', { name: 'Verify' }))
    await waitFor(() => expect(mockAuth.verifyTwoFactor).toHaveBeenCalledWith('c1', '481234'))
    await waitFor(() => expect(useAuthStore.getState().status).toBe('signedIn'))
  })

  it('does not accept a short code', async () => {
    mockAuth.login.mockResolvedValue({ requiresTwoFactor: true, challengeId: 'c1' })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'secret123')
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    fireEvent.changeText(await screen.findByLabelText('Code *'), '12')
    fireEvent.press(screen.getByRole('button', { name: 'Verify' }))
    expect(await screen.findByText('Enter the 6-digit code.')).toBeTruthy()
    expect(mockAuth.verifyTwoFactor).not.toHaveBeenCalled()
  })

  it('says the server cannot be reached, not that the password is wrong', async () => {
    mockAuth.login.mockRejectedValue(new Error('Network Error'))
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'secret123')
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/Cannot reach the server/)
  })

  it('a plain 401 reads "Wrong email or password." instead of "Unauthorized."', async () => {
    mockAuth.login.mockRejectedValue({ response: { status: 401, data: { detail: 'Unauthorized.' } } })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'x')
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Wrong email or password.')
  })

  it('shows the server sentence when the sign-in is refused', async () => {
    mockAuth.login.mockRejectedValue({ response: { status: 400, data: { detail: 'Account is locked.' } } })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'x')
    fireEvent.press(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Account is locked.')
  })
})

describe('Purchase order detail', () => {
  beforeEach(() => {
    mockParams = { id: 'po1' }
    mockPo.get.mockResolvedValue(po())
  })

  it('offers approve, reject and cancel to someone who may', async () => {
    signInAs(['purchase_order:approve', 'purchase_order:cancel'])
    renderScreen(<PurchaseDetailScreen />)
    expect(await screen.findByRole('button', { name: 'Approve' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Reject' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Cancel order' })).toBeTruthy()
    expect(screen.getByText('Acme Ltd')).toBeTruthy()
  })

  it('offers nothing to someone who may only read', async () => {
    signInAs(['purchase_order:read'])
    renderScreen(<PurchaseDetailScreen />)
    await screen.findByText('Acme Ltd')
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
  })

  it('approve asks first and runs only after the confirmation', async () => {
    signInAs(['purchase_order:approve'])
    mockPo.approve.mockResolvedValue({})
    renderScreen(<PurchaseDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Approve' }))
    expect(mockPo.approve).not.toHaveBeenCalled()
    expect(screen.getByText('Approve PO-00123?')).toBeTruthy()
    fireEvent.press(screen.getAllByRole('button', { name: 'Approve' }).at(-1)!)
    await waitFor(() => expect(mockPo.approve).toHaveBeenCalledWith('po1'))
    await waitFor(() => expect(useToastStore.getState().items[0]?.message).toBe('PO-00123 approved.'))
  })

  it('reject needs a reason and sends it', async () => {
    signInAs(['purchase_order:approve'])
    mockPo.reject.mockResolvedValue({})
    renderScreen(<PurchaseDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Reject' }))
    fireEvent.press(screen.getAllByRole('button', { name: 'Reject' }).at(-1)!)
    expect(await screen.findByText('Enter a reason.')).toBeTruthy()
    expect(mockPo.reject).not.toHaveBeenCalled()
    fireEvent.changeText(screen.getByLabelText('Reason *'), '  Price is above the quote  ')
    fireEvent.press(screen.getAllByRole('button', { name: 'Reject' }).at(-1)!)
    await waitFor(() => expect(mockPo.reject).toHaveBeenCalledWith('po1', 'Price is above the quote'))
  })

  it('shows the server refusal inside the sheet and keeps it open', async () => {
    signInAs(['purchase_order:approve'])
    mockPo.approve.mockRejectedValue({ response: { status: 400, data: { detail: 'The order is no longer submitted.' } } })
    renderScreen(<PurchaseDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Approve' }))
    fireEvent.press(screen.getAllByRole('button', { name: 'Approve' }).at(-1)!)
    expect(await screen.findByText('The order is no longer submitted.')).toBeTruthy()
    expect(screen.getByText('Approve PO-00123?')).toBeTruthy()
  })

  it('a 403 says the account is not allowed', async () => {
    signInAs(['purchase_order:approve'])
    mockPo.approve.mockRejectedValue({ response: { status: 403, data: {} } })
    renderScreen(<PurchaseDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Approve' }))
    fireEvent.press(screen.getAllByRole('button', { name: 'Approve' }).at(-1)!)
    expect(await screen.findByText('Your account is not allowed to do this.')).toBeTruthy()
  })

  it('finance approval shows only for the finance step', async () => {
    signInAs(['purchase_order:finance_approve'])
    mockPo.get.mockResolvedValue(po({ status: 'PENDING_FINANCE_APPROVAL' }))
    renderScreen(<PurchaseDetailScreen />)
    expect(await screen.findByRole('button', { name: 'Finance approve' })).toBeTruthy()
  })
})

describe('Sales order detail', () => {
  it('cancelling needs a reason, like the server', async () => {
    mockParams = { id: 'so1' }
    signInAs(['sales_order:cancel'])
    mockSo.get.mockResolvedValue({
      orderId: 'so1', orderNo: 'SO-10482', customerName: 'Linh Tran', status: 'Approved', totalAmount: 626.99, orderDate: '2026-11-04T00:00:00Z',
      isAnomalous: false, anomalyReason: null, customerEmail: 'l@x.y', discountAmount: 0, trackingNumber: null, carrier: null, notes: null,
      lineItems: [{ productId: 'p', productName: 'CozyNest K20', quantity: 1, unitPrice: 626.99, shippedQuantity: 0 }],
      shippingAddress: { street: '12 Le Loi', city: 'Da Nang', state: '', postalCode: '', country: 'VN' },
    })
    mockSo.cancel.mockResolvedValue({})
    renderScreen(<SalesDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Cancel order' }))
    fireEvent.press(screen.getAllByRole('button', { name: 'Cancel order' }).at(-1)!)
    expect(await screen.findByText('Enter a reason.')).toBeTruthy()
    fireEvent.changeText(screen.getByLabelText('Reason *'), 'Customer asked')
    fireEvent.press(screen.getAllByRole('button', { name: 'Cancel order' }).at(-1)!)
    await waitFor(() => expect(mockSo.cancel).toHaveBeenCalledWith('so1', 'Customer asked'))
  })

  it('flags an unusual order', async () => {
    mockParams = { id: 'so2' }
    signInAs(['sales_order:read'])
    mockSo.get.mockResolvedValue({
      orderId: 'so2', orderNo: 'SO-10477', customerName: 'Hana', status: 'Submitted', totalAmount: 48900, orderDate: '2026-11-03T00:00:00Z',
      isAnomalous: true, anomalyReason: 'Ten times the usual amount', customerEmail: '', discountAmount: 0, trackingNumber: null, carrier: null, notes: null,
      lineItems: [], shippingAddress: { street: '', city: '', state: '', postalCode: '', country: '' },
    })
    renderScreen(<SalesDetailScreen />)
    expect(await screen.findByText('Ten times the usual amount')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
  })
})

describe('Leave request detail', () => {
  it('approves a pending request after the confirmation', async () => {
    mockParams = { id: 'l1' }
    signInAs(['hrm:leave:manage'])
    mockHr.leave.mockResolvedValue({ id: 'l1', leaveRequestNumber: 'LV-1', employeeId: 'e1', leaveType: 'Annual', startDate: '2026-11-05T00:00:00Z', endDate: '2026-11-07T00:00:00Z', daysRequested: 3, leaveStatus: 'Pending', reason: 'Family' })
    mockHr.approveLeave.mockResolvedValue({})
    renderScreen(<LeaveDetailScreen />)
    fireEvent.press(await screen.findByRole('button', { name: 'Approve' }))
    fireEvent.press(screen.getAllByRole('button', { name: 'Approve' }).at(-1)!)
    await waitFor(() => expect(mockHr.approveLeave).toHaveBeenCalledWith('l1'))
  })
})

describe('Purchase order list', () => {
  it('shows rows, narrows by status and opens a record', async () => {
    signInAs(['purchase_order:read'])
    mockPo.list.mockResolvedValue({ items: [po()], total: 1 })
    renderScreen(<PurchaseListScreen />)
    fireEvent.press(await screen.findByRole('button', { name: /PO-00123, Acme Ltd/ }))
    expect(mockPush).toHaveBeenCalledWith('/purchase/po1')
    fireEvent.press(screen.getByRole('button', { name: 'Open filters' }))
    fireEvent.press(await screen.findByRole('tab', { name: 'Approved' }))
    await waitFor(() => expect(mockPo.list).toHaveBeenCalledWith({ status: 'APPROVED' }, 1))
  })

  it('opens on the status Home linked to', async () => {
    signInAs(['purchase_order:read'])
    mockParams = { status: 'SUBMITTED' }
    mockPo.list.mockResolvedValue({ items: [], total: 0 })
    renderScreen(<PurchaseListScreen />)
    await waitFor(() => expect(mockPo.list).toHaveBeenCalledWith({ status: 'SUBMITTED' }, 1))
    expect(await screen.findByText('No purchase orders with this status.')).toBeTruthy()
  })

  it('shows a retry when loading fails, and "no access" on a 403', async () => {
    signInAs(['purchase_order:read'])
    mockPo.list.mockRejectedValueOnce({ response: { status: 500, data: { detail: 'Service down.' } } })
    renderScreen(<PurchaseListScreen />)
    expect(await screen.findByText('Service down.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy()
  })

  it('no access', async () => {
    signInAs([])
    mockPo.list.mockRejectedValue({ response: { status: 403, data: {} } })
    renderScreen(<PurchaseListScreen />)
    expect(await screen.findByText('You do not have access to this.')).toBeTruthy()
  })
})

describe('Home', () => {
  it('shows the figures and the things waiting for this user', async () => {
    signInAs(['reports:sales:view', 'purchase_order:read', 'purchase_order:approve'])
    mockDash.sales.mockResolvedValue({ totalRevenue: 12340, totalOrders: 86, averageOrderValue: 143.5, revenueByDay: [{ date: '2026-11-01', revenue: 100, orderCount: 1 }, { date: '2026-11-02', revenue: 300, orderCount: 2 }] })
    mockPo.count.mockResolvedValue(3)
    renderScreen(<DashboardScreen />)
    expect(await screen.findByLabelText('Revenue: $12,340.00')).toBeTruthy()
    expect(screen.getByLabelText('Orders: 86')).toBeTruthy()
    fireEvent.press(await screen.findByRole('button', { name: '3 purchase orders to approve' }))
    expect(mockPush).toHaveBeenCalledWith({ pathname: '/purchase', params: { status: 'SUBMITTED' } })
  })

  it('without the report permission says so instead of showing zeros', async () => {
    signInAs([])
    mockDash.sales.mockRejectedValue({ response: { status: 403, data: {} } })
    renderScreen(<DashboardScreen />)
    expect(await screen.findByText('You do not have access to this.')).toBeTruthy()
  })
})
