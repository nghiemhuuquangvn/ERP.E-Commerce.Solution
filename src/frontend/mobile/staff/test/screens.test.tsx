import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'

jest.mock('expo-router', () => ({ useRouter: () => ({ push: jest.fn(), replace: jest.fn() }) }))

const mockGet = jest.fn()
const mockPost = jest.fn()
jest.mock('@/api/client', () => ({ apiClient: { get: (...a: unknown[]) => mockGet(...a), post: (...a: unknown[]) => mockPost(...a) } }))

import LoginScreen from '@/app/(auth)/login'
import InventoryScreen from '@/app/(tabs)/inventory'
import DeliveriesScreen from '@/app/(tabs)/deliveries'
import { useAuthStore } from '@/stores/auth.store'
import { useToastStore } from '@/components/ui/Toast'
import { ToastHost } from '@/components/ui'

beforeEach(() => {
  jest.clearAllMocks()
  useAuthStore.setState({ user: null, accessToken: null, isAuthenticated: false })
  useToastStore.setState({ items: [] })
})

describe('Login', () => {
  it('shows an error under each empty field and sends nothing', () => {
    renderScreen(<LoginScreen />)
    fireEvent.press(screen.getByRole('button', { name: 'Sign In' }))
    expect(screen.getAllByText('This field is required.')).toHaveLength(2)
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('posts the same body as before and stores the session', async () => {
    mockPost.mockResolvedValue({ data: { user: { id: 'u1', email: 'a@b.c', firstName: 'A' }, accessToken: 'tok' } })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'secret123')
    fireEvent.press(screen.getByRole('button', { name: 'Sign In' }))
    await waitFor(() => expect(useAuthStore.getState().isAuthenticated).toBe(true))
    expect(mockPost).toHaveBeenCalledWith('/v1/auth/login', { email: 'a@b.c', password: 'secret123' })
  })

  it('shows a danger alert when refused', async () => {
    mockPost.mockRejectedValue(new Error('401'))
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'bad')
    fireEvent.press(screen.getByRole('button', { name: 'Sign In' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
  })
})

describe('Inventory', () => {
  const item = { productId: 'p1', productName: 'Blue shirt', productSku: 'BS-1', availableQuantity: 3, totalQuantity: 10 }
  const openSheet = async () => {
    mockGet.mockResolvedValue({ data: { items: [item] } })
    renderScreen(<><InventoryScreen /><ToastHost /></>)
    fireEvent.press(await screen.findByRole('button', { name: 'Adjust stock of Blue shirt' }))
    expect(screen.getByText('Current: 10')).toBeTruthy()
  }
  const type = (v: string) => fireEvent.changeText(screen.getByLabelText('New quantity *'), v)
  const send = () => fireEvent.press(screen.getByRole('button', { name: 'Send request' }))

  it('files a stock-adjustment REQUEST (difference, numeric reason, note) and says a manager must approve it', async () => {
    mockPost.mockResolvedValue({ data: {} })
    await openSheet()
    type('7')
    fireEvent.changeText(screen.getByLabelText('Note (optional)'), '  two broken  ')
    send()
    await waitFor(() => expect(mockPost).toHaveBeenCalled())
    expect(mockPost).toHaveBeenCalledWith('/v1/stock-adjustments', { productId: 'p1', quantityDelta: -3, reason: 2, notes: 'two broken' })
    expect(await screen.findByText('Adjustment requested. The stock changes once a manager approves it.')).toBeTruthy()
    expect(mockGet).toHaveBeenCalledTimes(1) // the stock did not change, so the list is not read again
  })

  it('offers the four reasons, Correction first selected, and sends the chosen reason as its number', async () => {
    mockPost.mockResolvedValue({ data: {} })
    await openSheet()
    expect(screen.getByRole('radio', { name: 'Correction' }).props.accessibilityState).toMatchObject({ selected: true })
    for (const r of ['Damaged', 'Lost', 'Other']) expect(screen.getByRole('radio', { name: r })).toBeTruthy()
    fireEvent.press(screen.getByRole('radio', { name: 'Damaged' }))
    type('12')
    send()
    await waitFor(() => expect(mockPost).toHaveBeenCalled())
    expect(mockPost).toHaveBeenCalledWith('/v1/stock-adjustments', { productId: 'p1', quantityDelta: 2, reason: 0, notes: undefined })
  })

  it('refuses a quantity that is not a whole number, or is the current one, without sending anything', async () => {
    await openSheet()
    type('abc'); send()
    expect(await screen.findByText('Enter a whole number.')).toBeTruthy()
    type('-4'); send()
    expect(await screen.findByText('Enter a whole number.')).toBeTruthy()
    type('10'); send()
    expect(await screen.findByText('Nothing to change: that is the current quantity.')).toBeTruthy()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('tells the user when the role may not request adjustments (403), and keeps the sheet open', async () => {
    mockPost.mockRejectedValue(Object.assign(new Error('403'), { isAxiosError: true, response: { status: 403 } }))
    await openSheet()
    type('5'); send()
    expect(await screen.findByText('You are not allowed to request stock adjustments.')).toBeTruthy()
    expect(screen.getByLabelText('New quantity *')).toBeTruthy()
  })

  it('shows the generic toast for another failure', async () => {
    mockPost.mockRejectedValue(new Error('500'))
    await openSheet()
    type('5'); send()
    expect(await screen.findByText('Failed to adjust stock.')).toBeTruthy()
  })

  it('has a labelled search, and an error state with retry', async () => {
    mockGet.mockRejectedValueOnce(new Error('down'))
    renderScreen(<InventoryScreen />)
    expect(screen.getByLabelText('Search products')).toBeTruthy()
    expect(await screen.findByText('We could not load this. Please try again.')).toBeTruthy()
    mockGet.mockResolvedValue({ data: { items: [] } })
    fireEvent.press(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('No products found.')).toBeTruthy()
  })
})

describe('Deliveries', () => {
  it('marks a shipment in transit with the same request as before, named by its tracking number', async () => {
    mockGet.mockResolvedValue({ data: { items: [{ id: 's1', orderId: '12345678-aaaa', status: 'PickedUp', trackingNumber: 'TN1', estimatedDelivery: '' }] } })
    mockPost.mockResolvedValue({ data: {} })
    renderScreen(<DeliveriesScreen />)
    expect(await screen.findByText('Picked up')).toBeTruthy()
    expect(mockGet).toHaveBeenCalledWith('/v1/shipments', { params: { pageSize: 50 } })
    fireEvent.press(screen.getByRole('button', { name: 'Mark TN1 in transit' }))
    await waitFor(() => expect(mockPost).toHaveBeenCalled())
    expect(mockPost).toHaveBeenCalledWith('/v1/shipments/s1/tracking', { status: 'InTransit', location: 'Warehouse', description: 'Updated by staff' })
  })

  it('offers no action on a delivered shipment', async () => {
    mockGet.mockResolvedValue({ data: { items: [{ id: 's1', orderId: '12345678-aaaa', status: 'Delivered', trackingNumber: 'TN1', estimatedDelivery: '' }] } })
    renderScreen(<DeliveriesScreen />)
    expect(await screen.findByText('Delivered')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /in transit/ })).toBeNull()
  })

  it('shows the empty state', async () => {
    mockGet.mockResolvedValue({ data: { items: [] } })
    renderScreen(<DeliveriesScreen />)
    expect(await screen.findByText('No active shipments.')).toBeTruthy()
  })
})
