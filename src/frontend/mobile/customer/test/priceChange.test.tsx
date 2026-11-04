import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'

const mockRouter = { push: jest.fn(), replace: jest.fn() }
jest.mock('expo-router', () => ({
  useRouter: () => mockRouter,
  useLocalSearchParams: () => ({ id: 'p1' }),
  Link: ({ children }: { children: React.ReactNode }) => children,
}))

const mockGet = jest.fn()
const mockPost = jest.fn()
jest.mock('@/api/client', () => ({ isNetworkError: (e: { response?: unknown }) => !e?.response, apiClient: { get: (...a: unknown[]) => mockGet(...a), post: (...a: unknown[]) => mockPost(...a) } }))
jest.mock('@/api/payments', () => ({
  fetchPaymentMethods: jest.fn(() => Promise.resolve([{ code: 'COD', name: 'Cash', isGateway: false }])),
  startCheckout: jest.fn(),
  fetchPaymentStatus: jest.fn(() => Promise.resolve(null)),
}))

import CartScreen from '@/app/(tabs)/cart'
import ProductScreen from '@/app/product/[id]'
import { useAuthStore } from '@/stores/auth.store'
import { useCartStore } from '@/stores/cart.store'
import { ToastHost } from '@/components/ui'

// R113: the server prices every order line. The cart is remembered between visits, so it is checked against the shop's current prices when it opens,
// and an order refused because a price moved reloads the prices and says so instead of a generic error.
const seed = (unitPrice: number) =>
  useCartStore.setState({ items: [{ productId: 'p1', productName: 'Blue shirt', unitPrice, quantity: 2 }], priceChanges: [] })

const shopPrice = (price: number) => mockGet.mockResolvedValue({ data: { id: 'p1', name: 'Blue shirt', price, stockQuantity: 5 } })

beforeEach(() => {
  jest.clearAllMocks()
  useCartStore.setState({ items: [], couponCode: null, couponDiscount: 0, priceChanges: [] })
  useAuthStore.setState({ user: { id: 'u1', email: 'a@b.c', fullName: 'Ann' }, accessToken: 't', isAuthenticated: true })
})

describe('Cart: prices are checked when the cart opens', () => {
  it('takes the shop\'s price, lists the change in a notice and totals the new price', async () => {
    seed(10)
    shopPrice(12)
    renderScreen(<CartScreen />)

    expect(await screen.findByText(/Some prices changed/)).toBeTruthy()
    expect(screen.getByText(/Blue shirt: \$10\.00 → \$12\.00/)).toBeTruthy()
    expect(screen.getByText(/Check the total/)).toBeTruthy()
    expect(useCartStore.getState().items[0].unitPrice).toBe(12)
    expect(await screen.findByRole('button', { name: 'Place Order · $24.00' })).toBeTruthy()
  })

  it('says nothing when no price moved', async () => {
    seed(12)
    shopPrice(12)
    renderScreen(<CartScreen />)
    await screen.findByRole('radio', { name: 'Cash on delivery' })

    expect(screen.queryByText(/Some prices changed/)).toBeNull()
  })

  it('takes a product that has no price any more out of the cart', async () => {
    seed(10)
    shopPrice(0)
    renderScreen(<CartScreen />)

    await waitFor(() => expect(useCartStore.getState().items).toHaveLength(0))
    expect(useCartStore.getState().priceChanges[0].now).toBe(0)
  })

  it('keeps a line whose product cannot be read right now', async () => {
    seed(10)
    mockGet.mockRejectedValue(new Error('down'))
    renderScreen(<CartScreen />)
    await screen.findByRole('radio', { name: 'Cash on delivery' })

    expect(useCartStore.getState().items[0].unitPrice).toBe(10)
    expect(screen.queryByText(/Some prices changed/)).toBeNull()
  })

  it('closing the notice hides it', async () => {
    seed(10)
    shopPrice(12)
    renderScreen(<CartScreen />)
    await screen.findByText(/Some prices changed/)

    fireEvent.press(screen.getByRole('button', { name: 'Close' }))

    await waitFor(() => expect(screen.queryByText(/Some prices changed/)).toBeNull())
  })

  it('does not remember the notice with the cart', () => {
    seed(10)
    useCartStore.setState({ priceChanges: [{ productId: 'p1', productName: 'Blue shirt', was: 10, now: 12 }] })

    const persisted = (useCartStore as unknown as { persist: { getOptions: () => { partialize?: (s: unknown) => Record<string, unknown> } } }).persist.getOptions().partialize!(useCartStore.getState())

    expect(Object.keys(persisted)).toEqual(['items', 'couponCode', 'couponDiscount'])
  })
})

describe('Cart: an order refused because of a price', () => {
  const refused = (detail: string) => Object.assign(new Error('400'), { response: { status: 400, data: { detail } } })

  it('reloads the prices and shows the notice instead of a generic error; nothing is cleared', async () => {
    seed(10)
    shopPrice(10)
    renderScreen(<><CartScreen /><ToastHost /></>)
    await screen.findByRole('radio', { name: 'Cash on delivery' })
    shopPrice(15)   // the price moved after the cart was opened
    mockPost.mockRejectedValueOnce(refused('The price of Blue shirt changed from 10.00 to 15.00.'))

    fireEvent.press(screen.getByRole('button', { name: 'Place Order · $20.00' }))

    expect(await screen.findByText(/Blue shirt: \$10\.00 → \$15\.00/)).toBeTruthy()
    expect(screen.queryByText('Failed to place order. Please try again.')).toBeNull()
    expect(useCartStore.getState().items).toHaveLength(1)
    expect(useCartStore.getState().items[0].unitPrice).toBe(15)
  })

  it('shows the server\'s own sentence when the refusal is not about a price', async () => {
    seed(10)
    shopPrice(10)
    renderScreen(<><CartScreen /><ToastHost /></>)
    await screen.findByRole('radio', { name: 'Cash on delivery' })
    mockPost.mockRejectedValueOnce(refused('Blue shirt has no price set, so it cannot be ordered.'))

    fireEvent.press(screen.getByRole('button', { name: 'Place Order · $20.00' }))

    expect(await screen.findByText('Blue shirt has no price set, so it cannot be ordered.')).toBeTruthy()
    expect(useCartStore.getState().items).toHaveLength(1)
  })

  it('falls back to the generic text when the request got no answer', async () => {
    seed(10)
    shopPrice(10)
    renderScreen(<><CartScreen /><ToastHost /></>)
    await screen.findByRole('radio', { name: 'Cash on delivery' })
    mockPost.mockRejectedValueOnce(new Error('Network Error'))

    fireEvent.press(screen.getByRole('button', { name: 'Place Order · $20.00' }))

    expect(await screen.findByText('Failed to place order. Please try again.')).toBeTruthy()
  })
})

describe('A product with no price', () => {
  it('the product screen says "Price not available" and cannot add it to the cart', async () => {
    mockGet.mockResolvedValue({ data: { id: 'p1', name: 'Blue shirt', description: '', price: 0, stockQuantity: 5, categoryName: '' } })
    renderScreen(<ProductScreen />)

    expect((await screen.findAllByText('Price not available')).length).toBeGreaterThan(0)
    const add = screen.getByRole('button', { name: 'Price not available' })
    expect(add.props.accessibilityState?.disabled ?? add.props['aria-disabled']).toBeTruthy()
    await act(async () => { fireEvent.press(add) })
    expect(useCartStore.getState().items).toHaveLength(0)
  })
})
