import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import { Text } from 'react-native'
import { renderScreen } from './render'

const mockRouter = { push: jest.fn(), replace: jest.fn() }
jest.mock('expo-router', () => {
  const { Text: RNText } = require('react-native')
  return {
    useRouter: () => mockRouter,
    useLocalSearchParams: () => ({ id: 'o1' }),
    Link: ({ children }: { children: React.ReactNode }) => <RNText accessibilityRole="link">{children}</RNText>,
  }
})

const mockGet = jest.fn()
const mockPost = jest.fn()
jest.mock('@/api/client', () => ({ apiClient: { get: (...a: unknown[]) => mockGet(...a), post: (...a: unknown[]) => mockPost(...a) } }))
jest.mock('@/api/payments', () => ({
  fetchPaymentMethods: jest.fn(() => Promise.resolve([{ code: 'COD', name: 'Cash', isGateway: false }, { code: 'MoMo', name: 'MoMo', isGateway: true }])),
  startCheckout: jest.fn(),
  fetchPaymentStatus: jest.fn(() => Promise.resolve(null)),
}))

import LoginScreen from '@/app/(auth)/login'
import RegisterScreen from '@/app/(auth)/register'
import SearchScreen from '@/app/(tabs)/search'
import OrdersScreen from '@/app/(tabs)/orders'
import CartScreen from '@/app/(tabs)/cart'
import { useAuthStore } from '@/stores/auth.store'
import { useCartStore } from '@/stores/cart.store'
import { useToastStore } from '@/components/ui/Toast'
import { ToastHost } from '@/components/ui'

beforeEach(() => {
  jest.clearAllMocks()
  useAuthStore.setState({ user: null, accessToken: null, isAuthenticated: false })
  useCartStore.setState({ items: [], couponCode: null, couponDiscount: 0 })
  useToastStore.setState({ items: [] })
})

describe('Login', () => {
  it('has labelled fields and a named button', () => {
    renderScreen(<LoginScreen />)
    expect(screen.getByLabelText('Email *')).toBeTruthy()
    expect(screen.getByLabelText('Password *')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Sign In' })).toBeTruthy()
  })

  it('shows an error under each empty field and sends nothing', () => {
    renderScreen(<LoginScreen />)
    fireEvent.press(screen.getByRole('button', { name: 'Sign In' }))
    expect(screen.getAllByText('This field is required.')).toHaveLength(2)
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('posts the same body as before and stores the session', async () => {
    mockPost.mockResolvedValue({ data: { user: { id: 'u1', email: 'a@b.c' }, accessToken: 'tok' } })
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'secret123')
    fireEvent.press(screen.getByRole('button', { name: 'Sign In' }))
    await waitFor(() => expect(useAuthStore.getState().isAuthenticated).toBe(true))
    expect(mockPost).toHaveBeenCalledWith('/v1/auth/login', { email: 'a@b.c', password: 'secret123' })
  })

  it('shows a danger alert when the sign-in is refused', async () => {
    mockPost.mockRejectedValue(new Error('401'))
    renderScreen(<LoginScreen />)
    fireEvent.changeText(screen.getByLabelText('Email *'), 'a@b.c')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'bad')
    fireEvent.press(screen.getByRole('button', { name: 'Sign In' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.')
    expect(useAuthStore.getState().isAuthenticated).toBe(false)
  })
})

describe('Register', () => {
  it('posts first name, last name, email and password', async () => {
    mockPost.mockResolvedValue({ data: { user: { id: 'u1', email: 'n@x.y' }, accessToken: 'tok' } })
    renderScreen(<RegisterScreen />)
    fireEvent.changeText(screen.getByLabelText('First name *'), 'Nam')
    fireEvent.changeText(screen.getByLabelText('Last name'), 'Le')
    fireEvent.changeText(screen.getByLabelText('Email *'), 'n@x.y')
    fireEvent.changeText(screen.getByLabelText('Password *'), 'password1')
    fireEvent.press(screen.getByRole('button', { name: 'Create Account' }))
    await waitFor(() => expect(mockPost).toHaveBeenCalled())
    expect(mockPost).toHaveBeenCalledWith('/v1/auth/register', { firstName: 'Nam', lastName: 'Le', email: 'n@x.y', password: 'password1' })
  })

  it('does not send when a required field is empty', () => {
    renderScreen(<RegisterScreen />)
    fireEvent.press(screen.getByRole('button', { name: 'Create Account' }))
    expect(screen.getAllByText('This field is required.')).toHaveLength(3)
    expect(mockPost).not.toHaveBeenCalled()
  })
})

describe('Search', () => {
  it('shows products, then an empty state with a clear button when nothing matches', async () => {
    mockGet.mockResolvedValueOnce({ data: { products: [{ id: 'p1', name: 'Blue shirt', price: 10, stockQuantity: 0 }] } })
    renderScreen(<SearchScreen />)
    expect(await screen.findByRole('button', { name: 'Blue shirt, $10.00, Out of stock' })).toBeTruthy()

    mockGet.mockResolvedValue({ data: [] })
    fireEvent.changeText(screen.getByLabelText('Search products'), 'zzz')
    expect(await screen.findByText('No products found.')).toBeTruthy()
    mockGet.mockResolvedValue({ data: { products: [] } })
    fireEvent.press(screen.getByRole('button', { name: 'Clear the search' }))
    await waitFor(() => expect(screen.getByLabelText('Search products').props.value).toBe(''))
  })

  it('shows an error with a retry button', async () => {
    mockGet.mockRejectedValueOnce(new Error('down'))
    renderScreen(<SearchScreen />)
    expect(await screen.findByText('We could not load this. Please try again.')).toBeTruthy()
    mockGet.mockResolvedValue({ data: { products: [] } })
    fireEvent.press(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByText('No products found.')).toBeTruthy()
  })
})

describe('Orders', () => {
  it('shows each order as one named card with a translated status', async () => {
    mockGet.mockResolvedValue({ data: [{ orderId: 'o1', orderNo: 'SO-1', status: 'Shipped', totalAmount: 25, orderDate: '2026-11-01T00:00:00Z' }] })
    renderScreen(<OrdersScreen />)
    const card = await screen.findByRole('button', { name: /SO-1, Shipped, \$25\.00/ })
    fireEvent.press(card)
    expect(mockRouter.push).toHaveBeenCalledWith('/order/o1')
  })

  it('shows an empty state that leads to the shop', async () => {
    mockGet.mockResolvedValue({ data: [] })
    renderScreen(<OrdersScreen />)
    expect(await screen.findByText('No orders yet.')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Browse Products' }))
    expect(mockRouter.push).toHaveBeenCalledWith('/(tabs)/search')
  })
})

describe('Cart', () => {
  const seed = () => useCartStore.setState({ items: [{ productId: 'p1', productName: 'Blue shirt', unitPrice: 10, quantity: 2 }] })

  it('is empty with a way to the shop', async () => {
    renderScreen(<CartScreen />)
    await act(async () => {})
    expect(screen.getByText('Your cart is empty')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Browse Products' }))
    expect(mockRouter.push).toHaveBeenCalledWith('/(tabs)/search')
  })

  it('changes a quantity and removes a line by named buttons', async () => {
    seed()
    renderScreen(<CartScreen />)
    await act(async () => {})
    fireEvent.press(screen.getByRole('button', { name: 'More Blue shirt' }))
    expect(useCartStore.getState().items[0].quantity).toBe(3)
    fireEvent.press(screen.getByRole('button', { name: 'Remove Blue shirt' }))
    expect(useCartStore.getState().items).toHaveLength(0)
  })

  it('offers the shop payment methods as a radio group and names the amount on the order button', async () => {
    seed()
    renderScreen(<CartScreen />)
    expect(await screen.findByRole('radio', { name: 'Cash on delivery' })).toBeTruthy()
    expect(screen.getByRole('radio', { name: 'MoMo' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Place Order · $20.00' })).toBeTruthy()
  })

  it('sends the same order request as before', async () => {
    seed()
    useAuthStore.setState({ user: { id: 'u1', email: 'a@b.c', fullName: 'Ann' }, accessToken: 't', isAuthenticated: true })
    mockPost.mockResolvedValue({ data: { orderId: 'o9' } })
    renderScreen(<CartScreen />)
    await screen.findByRole('radio', { name: 'Cash on delivery' })
    fireEvent.press(screen.getByRole('button', { name: 'Place Order · $20.00' }))
    await waitFor(() => expect(mockPost).toHaveBeenCalled())
    expect(mockPost).toHaveBeenCalledWith('/v1/orders', {
      customerId: 'u1',
      customerName: 'Ann',
      customerEmail: 'a@b.c',
      lineItems: [{ productId: 'p1', productName: 'Blue shirt', unitPrice: 10, quantity: 2 }],
      paymentMethod: 'COD',
    })
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalledWith('/order/o9'))
    expect(useCartStore.getState().items).toHaveLength(0)
  })

  it('tells the customer with a toast when the order is refused', async () => {
    seed()
    useAuthStore.setState({ user: { id: 'u1', email: 'a@b.c' }, accessToken: 't', isAuthenticated: true })
    mockPost.mockRejectedValue(new Error('400'))
    renderScreen(<><CartScreen /><ToastHost /></>)
    await screen.findByRole('radio', { name: 'Cash on delivery' })
    fireEvent.press(screen.getByRole('button', { name: 'Place Order · $20.00' }))
    expect(await screen.findByText('Failed to place order. Please try again.')).toBeTruthy()
    expect(useCartStore.getState().items).toHaveLength(1)
    expect(Text).toBeTruthy()
  })
})
