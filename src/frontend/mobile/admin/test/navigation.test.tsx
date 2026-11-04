import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'

const mockNavigate = jest.fn()
const mockPush = jest.fn()
let mockPath = '/'
jest.mock('expo-router', () => ({
  useRouter: () => ({ push: mockPush, navigate: mockNavigate, replace: jest.fn() }),
  usePathname: () => mockPath,
}))
jest.mock('@/api/auth', () => ({ ADMIN_USER_TYPE: 2, authApi: { logout: jest.fn().mockResolvedValue(undefined) } }))
jest.mock('@/api/client', () => ({ isNetworkError: (e: { response?: unknown }) => !e?.response, apiClient: {}, gatewayClient: {} }))
jest.mock('@/features/notifications/api', () => ({
  ...jest.requireActual('@/features/notifications/api'),
  notificationsApi: { history: jest.fn(), unreadCount: jest.fn(), markRead: jest.fn() },
}))

import { SideMenu, activeRoute } from '@/components/SideMenu'
import { MenuButton } from '@/components/MenuButton'
import { NotificationBell, badgeText } from '@/components/NotificationBell'
import NotificationsScreen from '@/features/notifications/NotificationsScreen'
import { notificationsApi, type AppNotification } from '@/features/notifications/api'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'
import { useMenuStore } from '@/stores/menu.store'
import i18n from '@/i18n'

const mockApi = jest.mocked(notificationsApi)

const signInAs = (permissions: string[], licensedModules: string[] = []) =>
  useAuthStore.setState({ status: 'signedIn', permissions, licensedModules, user: { id: 'u1', email: 'ann@x.io', name: 'Ann', roles: [] }, accessToken: 'a', refreshToken: 'r' })

const note = (over: Partial<AppNotification> = {}): AppNotification => ({ id: 'n1', subject: 'Order PO-1 approved', body: 'Your order was approved.', createdAt: '2026-11-03T10:00:00Z', isRead: false, ...over })

beforeEach(() => {
  jest.clearAllMocks()
  mockPath = '/'
  void i18n.changeLanguage('en')
  useMenuStore.setState({ open: false })
  useLocaleStore.setState({ locale: 'en' })
  useAuthStore.setState({ status: 'signedOut', user: null, accessToken: null, refreshToken: null, permissions: [], licensedModules: [] })
  mockApi.unreadCount.mockResolvedValue(0)
})

describe('badge text', () => {
  it('is empty at 0, the number up to 99 and "99+" beyond', () => {
    expect(badgeText(0)).toBe('')
    expect(badgeText(undefined)).toBe('')
    expect(badgeText(7)).toBe('7')
    expect(badgeText(99)).toBe('99')
    expect(badgeText(100)).toBe('99+')
  })

  it('the active route is the first path segment; the root is the Home tab file', () => {
    expect(activeRoute('/')).toBe('index')
    expect(activeRoute('/purchase')).toBe('purchase')
    expect(activeRoute('/notifications')).toBe('notifications')
  })
})

describe('Side menu', () => {
  it('opens from the header button and lists every module the account may open', async () => {
    signInAs(['purchase_order:read', 'sales_order:read', 'hrm:leave:manage'])
    renderScreen(<><MenuButton /><SideMenu /></>)
    expect(screen.queryByLabelText('Purchasing')).toBeNull()
    fireEvent.press(screen.getByLabelText('Open menu'))
    expect(await screen.findByLabelText('Purchasing')).toBeTruthy()
    for (const name of ['Home', 'Sales', 'HR']) expect(screen.getByLabelText(name)).toBeTruthy()
    expect(screen.getByText('Ann')).toBeTruthy()
    expect(screen.getByText('ann@x.io')).toBeTruthy()
  })

  it('never lists a module the account cannot use, and no Notifications row without notifications:view', async () => {
    signInAs(['purchase_order:read'])
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    expect(await screen.findByLabelText('Purchasing')).toBeTruthy()
    expect(screen.queryByLabelText('Sales')).toBeNull()
    expect(screen.queryByLabelText('HR')).toBeNull()
    expect(screen.queryByLabelText('Notifications')).toBeNull()
  })

  it('marks the current module and opens another one, closing the menu', async () => {
    signInAs(['purchase_order:read'])
    mockPath = '/purchase'
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    expect((await screen.findByLabelText('Purchasing')).props.accessibilityState.selected).toBe(true)
    expect(screen.getByLabelText('Home').props.accessibilityState.selected).toBe(false)
    fireEvent.press(screen.getByLabelText('Home'))
    expect(mockNavigate).toHaveBeenCalledWith('/(tabs)')
    expect(useMenuStore.getState().open).toBe(false)
  })

  it('navigates to a module route under the tabs', async () => {
    signInAs(['hrm:leave:manage'])
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    fireEvent.press(await screen.findByLabelText('HR'))
    expect(mockNavigate).toHaveBeenCalledWith('/(tabs)/hrm')
  })

  it('shows the Notifications row with the unread number and opens the inbox', async () => {
    signInAs(['notifications:view'])
    mockApi.unreadCount.mockResolvedValue(3)
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    expect(await screen.findByLabelText('3 unread')).toBeTruthy()
    fireEvent.press(screen.getByLabelText('Notifications'))
    expect(mockNavigate).toHaveBeenCalledWith('/notifications')
    expect(mockApi.unreadCount).toHaveBeenCalledWith('u1')
  })

  it('switches the language', async () => {
    signInAs([])
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    fireEvent.press(await screen.findByLabelText('Tiếng Việt'))
    expect(useLocaleStore.getState().locale).toBe('vi')
  })

  it('closes from the Close control', async () => {
    signInAs([])
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    fireEvent.press((await screen.findAllByLabelText('Close menu'))[0])
    expect(useMenuStore.getState().open).toBe(false)
  })

  it('signs out only after the confirmation', async () => {
    signInAs([])
    useMenuStore.setState({ open: true })
    renderScreen(<SideMenu />)
    fireEvent.press(await screen.findByText('Sign out'))
    expect(useMenuStore.getState().open).toBe(false)
    expect(useAuthStore.getState().status).toBe('signedIn')
    fireEvent.press((await screen.findAllByText('Sign out')).slice(-1)[0])
    await waitFor(() => expect(useAuthStore.getState().status).toBe('signedOut'))
  })
})

describe('Notification bell', () => {
  it('is not shown without notifications:view and makes no call', () => {
    signInAs(['purchase_order:read'])
    renderScreen(<NotificationBell />)
    expect(screen.queryByLabelText('Notifications')).toBeNull()
    expect(mockApi.unreadCount).not.toHaveBeenCalled()
  })

  it('shows no badge at 0 and the count (spoken in its label) when there are unread ones', async () => {
    signInAs(['notifications:view'])
    mockApi.unreadCount.mockResolvedValue(5)
    renderScreen(<NotificationBell />)
    expect(await screen.findByLabelText('Notifications, 5 unread')).toBeTruthy()
    expect(screen.getByText('5')).toBeTruthy()
  })

  it('caps the badge at 99+', async () => {
    signInAs(['notifications:view'])
    mockApi.unreadCount.mockResolvedValue(250)
    renderScreen(<NotificationBell />)
    expect(await screen.findByText('99+')).toBeTruthy()
  })

  it('stays without a badge when the count cannot be read', async () => {
    signInAs(['notifications:view'])
    mockApi.unreadCount.mockRejectedValue(new Error('down'))
    renderScreen(<NotificationBell />)
    await waitFor(() => expect(mockApi.unreadCount).toHaveBeenCalled())
    expect(screen.getByLabelText('Notifications')).toBeTruthy()
  })

  it('opens the inbox', () => {
    signInAs(['notifications:view'])
    renderScreen(<NotificationBell />)
    fireEvent.press(screen.getByLabelText('Notifications'))
    expect(mockPush).toHaveBeenCalledWith('/notifications')
  })
})

describe('Notifications inbox', () => {
  it('shows "no access" without notifications:view', () => {
    signInAs([])
    renderScreen(<NotificationsScreen />)
    expect(screen.getByText('You do not have access to this.')).toBeTruthy()
    expect(mockApi.history).not.toHaveBeenCalled()
  })

  it('lists the notifications of the signed-in user, unread ones marked in their label', async () => {
    signInAs(['notifications:view'])
    mockApi.history.mockResolvedValue({ items: [note(), note({ id: 'n2', subject: 'Leave approved', isRead: true })], total: 2 })
    renderScreen(<NotificationsScreen />)
    expect(await screen.findByLabelText('Unread: Order PO-1 approved')).toBeTruthy()
    expect(screen.getByLabelText('Read: Leave approved')).toBeTruthy()
    expect(mockApi.history).toHaveBeenCalledWith('u1', 1)
  })

  it('opening an unread one marks it read and lowers the unread number at once', async () => {
    signInAs(['notifications:view'])
    mockApi.history.mockResolvedValue({ items: [note()], total: 1 })
    mockApi.unreadCount.mockResolvedValue(1)
    mockApi.markRead.mockResolvedValue(undefined)
    renderScreen(<><NotificationBell /><NotificationsScreen /></>)
    expect(await screen.findByLabelText('Notifications, 1 unread')).toBeTruthy()
    fireEvent.press(await screen.findByLabelText('Unread: Order PO-1 approved'))
    await waitFor(() => expect(mockApi.markRead).toHaveBeenCalledWith('n1'))
    expect(await screen.findByLabelText('Read: Order PO-1 approved')).toBeTruthy()
    expect(screen.getByLabelText('Notifications')).toBeTruthy()
  })

  it('opening a read one does not call the server again', async () => {
    signInAs(['notifications:view'])
    mockApi.history.mockResolvedValue({ items: [note({ isRead: true })], total: 1 })
    renderScreen(<NotificationsScreen />)
    fireEvent.press(await screen.findByLabelText('Read: Order PO-1 approved'))
    expect(mockApi.markRead).not.toHaveBeenCalled()
  })

  it('shows an empty state', async () => {
    signInAs(['notifications:view'])
    mockApi.history.mockResolvedValue({ items: [], total: 0 })
    renderScreen(<NotificationsScreen />)
    expect(await screen.findByText('No notifications yet.')).toBeTruthy()
  })

  it('shows the server refusal as "no access" on a 403', async () => {
    signInAs(['notifications:view'])
    mockApi.history.mockRejectedValue({ response: { status: 403, data: {} } })
    renderScreen(<NotificationsScreen />)
    expect(await screen.findByText('You do not have access to this.')).toBeTruthy()
  })

  it('shows a network problem with Try again', async () => {
    signInAs(['notifications:view'])
    mockApi.history.mockRejectedValue(new Error('Network Error'))
    renderScreen(<NotificationsScreen />)
    expect(await screen.findByText('Cannot reach the server. Check the connection.')).toBeTruthy()
    expect(screen.getByText('Try again')).toBeTruthy()
  })
})
