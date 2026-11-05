import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'

const mockPush = jest.fn()
const mockNavigate = jest.fn()
let mockCount: number | undefined
jest.mock('expo-router', () => ({
  useRouter: () => ({ push: mockPush, navigate: mockNavigate, replace: jest.fn() }),
  usePathname: () => '/',
}))
jest.mock('@/api/auth', () => ({ ADMIN_USER_TYPE: 2, authApi: { logout: jest.fn().mockResolvedValue(undefined) } }))
jest.mock('@/api/client', () => ({ isNetworkError: (e: { response?: unknown }) => !e?.response, apiClient: {}, gatewayClient: {} }))
jest.mock('@/features/notifications/api', () => ({
  ...jest.requireActual('@/features/notifications/api'),
  notificationsApi: { history: jest.fn(), unreadCount: jest.fn(), markRead: jest.fn(), markAllRead: jest.fn() },
  devicesApi: { register: jest.fn().mockResolvedValue(undefined), unregister: jest.fn().mockResolvedValue(undefined) },
}))
jest.mock('expo-constants', () => ({ __esModule: true, default: { expoConfig: { extra: { pushEnabled: false } } } }))

import * as ExpoNotifications from 'expo-notifications'
import Constants from 'expo-constants'
import NotificationsScreen from '@/features/notifications/NotificationsScreen'
import { notificationTarget } from '@/features/notifications/target'
import { NotificationEffects } from '@/features/notifications/NotificationEffects'
import { registerThisDevice, unregisterThisDevice } from '@/features/notifications/push'
import { useNewNotificationAlert } from '@/features/notifications/useNewNotificationAlert'
import { devicesApi, notificationsApi, type AppNotification } from '@/features/notifications/api'
import * as unread from '@/features/notifications/useUnreadCount'
import { useAuthStore } from '@/stores/auth.store'
import i18n from '@/i18n'

const api = jest.mocked(notificationsApi)
const expoN = jest.mocked(ExpoNotifications)
const extra = () => (Constants.expoConfig as unknown as { extra: { pushEnabled: boolean } }).extra

const signInAs = (permissions: string[]) =>
  useAuthStore.setState({ status: 'signedIn', permissions, licensedModules: [], user: { id: 'u1', email: 'a@x.io', name: 'Ann', roles: [] }, accessToken: 'a', refreshToken: 'r' })

const note = (over: Partial<AppNotification> = {}): AppNotification => ({
  id: 'n1', subject: 'Purchase order PO-1 is waiting', body: 'PO-1 · 20.00 USD', createdAt: '2026-11-05T10:00:00Z', isRead: false,
  entityType: 'PurchaseOrder', entityId: 'po-1', routeKey: 'purchase-order', ...over,
})

beforeEach(() => {
  jest.clearAllMocks()
  void i18n.changeLanguage('en')
  api.unreadCount.mockResolvedValue(0)
  extra().pushEnabled = false
  mockCount = undefined
})

describe('notification target', () => {
  const all = () => true
  it('maps each record type to its detail screen', () => {
    expect(notificationTarget({ entityType: 'PurchaseOrder', entityId: 'a' }, all)).toBe('/purchase/a')
    expect(notificationTarget({ entityType: 'SalesOrder', entityId: 'b' }, all)).toBe('/sales/b')
    expect(notificationTarget({ entityType: 'LeaveRequest', entityId: 'c' }, all)).toBe('/hrm/leave/c')
  })
  it('is null without a link, for an unknown type, or without the permission to read the record', () => {
    expect(notificationTarget({}, all)).toBeNull()
    expect(notificationTarget({ entityType: 'PurchaseOrder', entityId: null }, all)).toBeNull()
    expect(notificationTarget({ entityType: 'Mystery', entityId: 'x' }, all)).toBeNull()
    expect(notificationTarget({ entityType: 'SalesOrder', entityId: 'b' }, (p) => p !== 'sales_order:read')).toBeNull()
  })
})

describe('inbox: link and mark all', () => {
  it('a tap marks the row read and opens the record', async () => {
    signInAs(['notifications:view', 'purchase_order:read'])
    api.history.mockResolvedValue({ items: [note()], total: 1 })
    api.markRead.mockResolvedValue(undefined)
    renderScreen(<NotificationsScreen />)
    fireEvent.press(await screen.findByLabelText('Unread: Purchase order PO-1 is waiting'))
    await waitFor(() => expect(api.markRead).toHaveBeenCalledWith('n1'))
    expect(mockPush).toHaveBeenCalledWith('/purchase/po-1')
  })

  it('without permission to read the record the row only expands', async () => {
    signInAs(['notifications:view'])
    api.history.mockResolvedValue({ items: [note({ isRead: true })], total: 1 })
    renderScreen(<NotificationsScreen />)
    fireEvent.press(await screen.findByLabelText('Read: Purchase order PO-1 is waiting'))
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('Mark all as read is hidden at 0 unread', async () => {
    signInAs(['notifications:view'])
    api.history.mockResolvedValue({ items: [note({ isRead: true })], total: 1 })
    renderScreen(<NotificationsScreen />)
    await screen.findByLabelText('Read: Purchase order PO-1 is waiting')
    expect(screen.queryByText('Mark all as read')).toBeNull()
  })

  it('Mark all as read flips every row at once and calls the server once', async () => {
    signInAs(['notifications:view'])
    api.unreadCount.mockResolvedValue(2)
    api.history.mockResolvedValue({ items: [note(), note({ id: 'n2', subject: 'Leave waiting' })], total: 2 })
    api.markAllRead.mockResolvedValue(2)
    renderScreen(<NotificationsScreen />)
    fireEvent.press(await screen.findByText('Mark all as read'))
    await waitFor(() => expect(api.markAllRead).toHaveBeenCalledTimes(1))
    expect(await screen.findByLabelText('Read: Purchase order PO-1 is waiting')).toBeTruthy()
    expect(screen.getByLabelText('Read: Leave waiting')).toBeTruthy()
  })
})

describe('phone alert when the unread count grows', () => {
  function Probe() {
    useNewNotificationAlert()
    return null
  }
  const setCount = (n: number | undefined) => {
    mockCount = n
  }

  beforeEach(() => {
    signInAs(['notifications:view'])
    jest.spyOn(unread, 'useUnreadCount').mockImplementation(() => mockCount)
    api.history.mockResolvedValue({ items: [note(), note({ id: 'n2', subject: 'Older', isRead: true })], total: 2 })
  })
  afterEach(() => jest.restoreAllMocks())

  it('only sets the baseline on the first value, then alerts with the newest unread message when the count grows', async () => {
    setCount(3)
    const { rerender } = render(<Probe />)
    await act(async () => { await Promise.resolve() })
    expect(expoN.scheduleNotificationAsync).not.toHaveBeenCalled()

    setCount(4)
    rerender(<Probe />)
    await waitFor(() => expect(expoN.scheduleNotificationAsync).toHaveBeenCalledTimes(1))
    expect(expoN.scheduleNotificationAsync.mock.calls[0][0]).toMatchObject({
      content: { title: 'Purchase order PO-1 is waiting', data: { entityType: 'PurchaseOrder', entityId: 'po-1' } },
      trigger: null,
    })
  })

  it('does not alert when the count falls or stays, and shows a message only once', async () => {
    setCount(2)
    const { rerender } = render(<Probe />)
    setCount(1)
    rerender(<Probe />)
    setCount(1)
    rerender(<Probe />)
    await act(async () => { await Promise.resolve() })
    expect(expoN.scheduleNotificationAsync).not.toHaveBeenCalled()

    setCount(2)
    rerender(<Probe />)
    await waitFor(() => expect(expoN.scheduleNotificationAsync).toHaveBeenCalledTimes(1))
    setCount(1)
    rerender(<Probe />)
    setCount(2)
    rerender(<Probe />)
    await act(async () => { await Promise.resolve() })
    expect(expoN.scheduleNotificationAsync).toHaveBeenCalledTimes(1) // n1 was already shown
  })
})

describe('push registration', () => {
  it('does nothing while push is switched off', async () => {
    expect(await registerThisDevice()).toBe(false)
    await unregisterThisDevice()
    expect(devicesApi.register).not.toHaveBeenCalled()
    expect(devicesApi.unregister).not.toHaveBeenCalled()
  })

  it('registers this phone with its token and removes it at sign-out when switched on', async () => {
    extra().pushEnabled = true
    expect(await registerThisDevice()).toBe(true)
    expect(devicesApi.register).toHaveBeenCalledWith('fcm-token', expect.any(String), expect.any(String))
    const deviceId = jest.mocked(devicesApi.register).mock.calls[0][1]
    await unregisterThisDevice()
    expect(devicesApi.unregister).toHaveBeenCalledWith(deviceId)
  })

  it('swallows a failing token call', async () => {
    extra().pushEnabled = true
    expoN.getDevicePushTokenAsync.mockRejectedValueOnce(new Error('no play services'))
    expect(await registerThisDevice()).toBe(false)
  })
})

describe('tapping a phone notification', () => {
  it('opens the record once with navigate, even when the same response is delivered again', async () => {
    signInAs(['notifications:view', 'hrm:leave:manage'])
    const listen = expoN.addNotificationResponseReceivedListener as jest.Mock
    renderScreen(<NotificationEffects />)
    await waitFor(() => expect(listen).toHaveBeenCalled())
    const deliver = listen.mock.calls[listen.mock.calls.length - 1][0]
    const response = { notification: { request: { identifier: 'tap-1', content: { data: { entityType: 'LeaveRequest', entityId: 'lr-1' } } } } }
    deliver(response)
    deliver(response)
    expect(mockNavigate).toHaveBeenCalledTimes(1)
    expect(mockNavigate).toHaveBeenCalledWith('/hrm/leave/lr-1')
    expect(mockPush).not.toHaveBeenCalled()
  })

  it('goes to the inbox when the record is not one the user may open', async () => {
    signInAs(['notifications:view'])
    const listen = expoN.addNotificationResponseReceivedListener as jest.Mock
    renderScreen(<NotificationEffects />)
    await waitFor(() => expect(listen).toHaveBeenCalled())
    listen.mock.calls[listen.mock.calls.length - 1][0]({ notification: { request: { identifier: 'tap-2', content: { data: { entityType: 'LeaveRequest', entityId: 'lr-2' } } } } })
    expect(mockNavigate).toHaveBeenCalledWith('/notifications')
  })
})
