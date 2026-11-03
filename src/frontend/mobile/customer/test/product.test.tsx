import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import { renderScreen } from './render'

const mockRouter = { push: jest.fn(), replace: jest.fn(), back: jest.fn() }
jest.mock('expo-router', () => ({ useRouter: () => mockRouter, useLocalSearchParams: () => ({ id: 'p1' }) }))

const mockGet = jest.fn()
jest.mock('@/api/client', () => ({ apiClient: { get: (...a: unknown[]) => mockGet(...a), post: jest.fn() } }))

import ProductScreen from '@/app/product/[id]'
import { useCartStore } from '@/stores/cart.store'
import { useToastStore } from '@/components/ui/Toast'
import { ToastHost } from '@/components/ui'

const product = { id: 'p1', sku: 'S1', name: 'Blue shirt', description: 'Soft cotton', price: 12.5, imageUrl: 'https://img/x.png', categoryName: 'Clothes', stockQuantity: 3 }

beforeEach(() => {
  jest.clearAllMocks()
  useCartStore.setState({ items: [], couponCode: null, couponDiscount: 0 })
  useToastStore.setState({ items: [] })
})

const open = async () => {
  renderScreen(<><ProductScreen /><ToastHost /></>)
  await screen.findByRole('header', { name: 'Blue shirt' })
}

describe('Product screen', () => {
  it('asks the API for the product in the app language and shows it', async () => {
    mockGet.mockResolvedValue({ data: product })
    await open()
    expect(mockGet).toHaveBeenCalledWith('/v1/products/p1', { params: { languageCode: 'en' } })
    expect(screen.getByText('$12.50')).toBeTruthy()
    expect(screen.getByText('In stock')).toBeTruthy()
    expect(screen.getByText('Soft cotton')).toBeTruthy()
    expect(screen.getByText('Clothes')).toBeTruthy()
  })

  it('adds the chosen quantity to the cart with the same fields as the web shop, shows a toast and resets the quantity', async () => {
    mockGet.mockResolvedValue({ data: product })
    await open()
    fireEvent.press(screen.getByRole('button', { name: 'More Quantity' }))
    fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }))
    expect(useCartStore.getState().items).toEqual([{ productId: 'p1', productName: 'Blue shirt', unitPrice: 12.5, imageUrl: 'https://img/x.png', quantity: 2 }])
    expect(await screen.findByText('Added to your cart')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }))
    expect(useCartStore.getState().items[0].quantity).toBe(3)
    await act(async () => {})
  })

  it('keeps the quantity between 1 and the stock', async () => {
    mockGet.mockResolvedValue({ data: product })
    await open()
    const fewer = () => fireEvent.press(screen.getByRole('button', { name: 'Fewer Quantity' }))
    const more = () => fireEvent.press(screen.getByRole('button', { name: 'More Quantity' }))
    fewer()
    more(); more(); more(); more()
    fireEvent.press(screen.getByRole('button', { name: 'Add to cart' }))
    expect(useCartStore.getState().items[0].quantity).toBe(3)
  })

  it('cannot be added when out of stock', async () => {
    mockGet.mockResolvedValue({ data: { ...product, stockQuantity: 0 } })
    await open()
    expect(screen.getAllByText('Out of stock')).toHaveLength(2) // the stock note and the disabled button
    const button = screen.getByRole('button', { name: 'Out of stock' })
    expect(button.props.accessibilityState).toMatchObject({ disabled: true })
    fireEvent.press(button)
    expect(useCartStore.getState().items).toHaveLength(0)
  })

  it('shows an error with a retry button', async () => {
    mockGet.mockRejectedValueOnce(new Error('down'))
    renderScreen(<ProductScreen />)
    expect(await screen.findByText('We could not load this product. Please try again.')).toBeTruthy()
    mockGet.mockResolvedValue({ data: product })
    fireEvent.press(screen.getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('header', { name: 'Blue shirt' })).toBeTruthy()
  })

  it('shows a not-found state that leads back to the shop', async () => {
    mockGet.mockResolvedValue({ data: null })
    renderScreen(<ProductScreen />)
    expect(await screen.findByText('This product could not be found.')).toBeTruthy()
    fireEvent.press(screen.getByRole('button', { name: 'Browse Products' }))
    expect(mockRouter.replace).toHaveBeenCalledWith('/(tabs)/search')
  })

  it('has a back button', async () => {
    mockGet.mockResolvedValue({ data: product })
    await open()
    fireEvent.press(screen.getByRole('button', { name: '‹ Back' }))
    expect(mockRouter.back).toHaveBeenCalled()
    await waitFor(() => expect(mockGet).toHaveBeenCalled())
  })
})
