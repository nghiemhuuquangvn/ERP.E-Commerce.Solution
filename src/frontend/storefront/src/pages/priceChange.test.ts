import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VueQueryPlugin, QueryClient } from '@tanstack/vue-query'
import { createMemoryHistory, createRouter } from 'vue-router'
import { axe } from '@/test/axe'
import { testI18n } from '@/test/setup'
import { useAuthStore } from '@/stores/auth.store'
import { useCartStore } from '@/stores/cart.store'
import { usePriceNoticeStore } from '@/stores/priceNotice.store'

// R113: the server prices every order line. The cart is remembered between visits, so it is checked against the shop's current prices when it is opened,
// and an order refused because a price moved reloads the prices and says so instead of a generic error.
const productsApi = vi.hoisted(() => ({ list: vi.fn(), featured: vi.fn(), get: vi.fn() }))
const ordersApi = vi.hoisted(() => ({ create: vi.fn(), get: vi.fn(), myOrders: vi.fn() }))
const paymentsApi = vi.hoisted(() => ({ methods: vi.fn(), checkout: vi.fn(), status: vi.fn() }))
const shippingApi = vi.hoisted(() => ({ feeFor: vi.fn(), fee: vi.fn() }))
vi.mock('@/api/products.api', () => ({ productsApi }))
vi.mock('@/api/orders.api', () => ({ ordersApi }))
vi.mock('@/api/payments.api', () => ({ paymentsApi }))
vi.mock('@/api/shipping.api', () => ({ shippingApi }))
vi.mock('@/api/client', () => ({ apiClient: { post: vi.fn(), get: vi.fn() } }))

import CartPage from './CartPage.vue'
import CheckoutPage from './CheckoutPage.vue'
import ProductDetailPage from './ProductDetailPage.vue'
import ProductCard from '@/components/product/ProductCard.vue'

const product = (over: Record<string, unknown> = {}) => ({ id: 'p1', sku: 'S1', name: 'Blue shirt', description: 'Soft cotton', price: 25, categoryName: 'Shirts', stockQuantity: 5, status: 1, ...over })

let wrapper: VueWrapper | null = null
async function mountPage(component: object, url: string, options: { auth?: boolean; cartPrice?: number } = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  if (options.auth) {
    const auth = useAuthStore()
    auth.accessToken = 'token'
    auth.user = { id: 'u1', email: 'a@b.c', fullName: 'Ann Lee' }
  }
  if (options.cartPrice !== undefined) useCartStore().addItem({ productId: 'p1', productName: 'Blue shirt', unitPrice: options.cartPrice }, 2)
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ['/', '/products', '/products/:id', '/cart', '/checkout', '/payment/result', '/order-confirmation/:id'].map((path) => ({ path, component: { template: '<div />' } })),
  })
  router.push(url)
  await router.isReady()
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  wrapper = mount(component, { attachTo: document.body, global: { plugins: [pinia, router, [VueQueryPlugin, { queryClient }], testI18n] } })
  await flushPromises()
  return { wrapper, router }
}

beforeEach(() => {
  vi.clearAllMocks()
  testI18n.global.locale.value = 'en'
  productsApi.get.mockResolvedValue(product())
  paymentsApi.methods.mockResolvedValue([{ code: 'COD', isGateway: false }])
  shippingApi.feeFor.mockResolvedValue({ fee: 30000, isFree: false, freeShippingThreshold: 500000 })
  ordersApi.create.mockResolvedValue({ orderId: 'o1' })
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  document.body.innerHTML = ''
})

describe('Cart: prices are checked when the cart opens', () => {
  it('takes the shop\'s current price, lists the change in a notice and totals the new price', async () => {
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cartPrice: 20 })

    expect(useCartStore().items[0].unitPrice).toBe(25)
    const notice = w.get('[data-testid=price-notice]')
    expect(notice.text()).toContain('Some prices changed')
    expect(notice.text()).toContain('Blue shirt: $20.00 → $25.00')
    expect(notice.text()).toContain('Check the total')
    expect(w.text()).toContain('$50.00')   // 2 x 25
  })

  it('says nothing when no price moved', async () => {
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cartPrice: 25 })

    expect(w.find('[data-testid=price-notice]').exists()).toBe(false)
  })

  it('takes a product that has no price any more out of the cart and says so', async () => {
    productsApi.get.mockResolvedValue(product({ price: 0 }))
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cartPrice: 25 })

    expect(useCartStore().items).toHaveLength(0)
    expect(usePriceNoticeStore().changes[0].now).toBe(0)
    expect(w.text()).toContain('your cart is empty'.replace('your cart', 'Your cart'))
  })

  it('keeps a line whose product cannot be read right now (the server decides at checkout)', async () => {
    productsApi.get.mockRejectedValue(new Error('down'))
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cartPrice: 20 })

    expect(useCartStore().items[0].unitPrice).toBe(20)
    expect(w.find('[data-testid=price-notice]').exists()).toBe(false)
  })

  it('closing the notice hides it', async () => {
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cartPrice: 20 })

    await w.findAll('button').find((b) => b.text() === 'Close')!.trigger('click')

    expect(w.find('[data-testid=price-notice]').exists()).toBe(false)
    expect(usePriceNoticeStore().changes).toHaveLength(0)
  })

  it('has no accessibility violation with the notice shown', async () => {
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cartPrice: 20 })

    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
  })
})

describe('Checkout: an order refused because of a price', () => {
  async function toReview(w: VueWrapper) {
    const label = (text: string) => w.get('#' + CSS.escape(w.findAll('label').find((l) => l.text().startsWith(text))!.attributes('for')!))
    await label('Street address').setValue('1 Main St')
    await label('Phone').setValue('0901234567')
    await label('City').setValue('Hanoi')
    await label('Country').setValue('Vietnam')
    await w.get('form').trigger('submit')
    await flushPromises()
    await w.findAll('button').find((b) => b.text().startsWith('Review Order'))!.trigger('click')
    await flushPromises()
  }

  const refused = (detail: string) => Object.assign(new Error('400'), { response: { status: 400, data: { detail } } })

  it('reloads the cart prices and shows the notice instead of a generic error; nothing is cleared', async () => {
    const { wrapper: w } = await mountPage(CheckoutPage, '/checkout', { auth: true, cartPrice: 25 })
    await toReview(w)
    productsApi.get.mockResolvedValue(product({ price: 30 }))   // the price moved after the page was opened
    ordersApi.create.mockRejectedValueOnce(refused('The price of Blue shirt changed from 25.00 to 30.00.'))

    await w.findAll('button').find((b) => b.text().startsWith('Place Order'))!.trigger('click')
    await flushPromises()

    expect(w.text()).toContain('Blue shirt: $25.00 → $30.00')
    expect(w.find('[role=alert]').exists()).toBe(false)
    expect(useCartStore().items).toHaveLength(1)
    expect(useCartStore().items[0].unitPrice).toBe(30)
    expect(w.findAll('button').find((b) => b.text().startsWith('Place Order'))!.attributes('disabled')).toBeUndefined()
  })

  it('shows the server\'s own sentence when the refusal is not about a price', async () => {
    const { wrapper: w } = await mountPage(CheckoutPage, '/checkout', { auth: true, cartPrice: 25 })
    await toReview(w)
    ordersApi.create.mockRejectedValueOnce(refused('Blue shirt has no price set, so it cannot be ordered.'))

    await w.findAll('button').find((b) => b.text().startsWith('Place Order'))!.trigger('click')
    await flushPromises()

    expect(w.get('[role=alert]').text()).toContain('Blue shirt has no price set')
    expect(useCartStore().items).toHaveLength(1)
  })

  it('falls back to the generic text when the request got no answer', async () => {
    const { wrapper: w } = await mountPage(CheckoutPage, '/checkout', { auth: true, cartPrice: 25 })
    await toReview(w)
    ordersApi.create.mockRejectedValueOnce(new Error('Network Error'))

    await w.findAll('button').find((b) => b.text().startsWith('Place Order'))!.trigger('click')
    await flushPromises()

    expect(w.get('[role=alert]').text()).toContain('Failed to place order')
  })
})

describe('A product with no price', () => {
  it('the product page says "Price not available" and cannot be added to the cart', async () => {
    productsApi.get.mockResolvedValue(product({ price: 0 }))
    const { wrapper: w } = await mountPage(ProductDetailPage, '/products/p1')

    expect(w.text()).toContain('Price not available')
    const add = w.findAll('button').filter((b) => b.text().includes('Price not available'))
    expect(add.length).toBeGreaterThan(0)
    for (const b of add) expect(b.attributes('disabled')).toBeDefined()
  })

  it('the product card says "Price not available" instead of $0.00', async () => {
    const pinia = createPinia()
    const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<div />' } }, { path: '/products/:id', component: { template: '<div />' } }] })
    wrapper = mount(ProductCard, { props: { product: product({ price: 0 }) as never }, global: { plugins: [pinia, router, testI18n] } })

    expect(wrapper.text()).toContain('Price not available')
    expect(wrapper.text()).not.toContain('$0.00')
  })
})
