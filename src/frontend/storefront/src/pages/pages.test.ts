import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VueQueryPlugin, QueryClient } from '@tanstack/vue-query'
import { createMemoryHistory, createRouter } from 'vue-router'
import { axe } from '@/test/axe'
import { testI18n } from '@/test/setup'
import { useAuthStore } from '@/stores/auth.store'
import { useCartStore } from '@/stores/cart.store'
import { useLocaleStore } from '@/stores/locale.store'

// The API modules are fakes: the pages are under test, not the network.
const productsApi = vi.hoisted(() => ({ list: vi.fn(), featured: vi.fn(), get: vi.fn() }))
const ordersApi = vi.hoisted(() => ({ create: vi.fn(), get: vi.fn(), myOrders: vi.fn() }))
const paymentsApi = vi.hoisted(() => ({ methods: vi.fn(), checkout: vi.fn(), status: vi.fn() }))
const shippingApi = vi.hoisted(() => ({ feeFor: vi.fn(), fee: vi.fn() }))
vi.mock('@/api/products.api', () => ({ productsApi }))
vi.mock('@/api/orders.api', () => ({ ordersApi }))
vi.mock('@/api/payments.api', () => ({ paymentsApi }))
vi.mock('@/api/shipping.api', () => ({ shippingApi }))
vi.mock('@/api/client', () => ({ apiClient: { post: vi.fn(), get: vi.fn() } }))

import HomePage from './HomePage.vue'
import ProductListPage from './ProductListPage.vue'
import ProductDetailPage from './ProductDetailPage.vue'
import CartPage from './CartPage.vue'
import CheckoutPage from './CheckoutPage.vue'
import PaymentResultPage from './PaymentResultPage.vue'
import OrderConfirmationPage from './OrderConfirmationPage.vue'
import OrderHistoryPage from './account/OrderHistoryPage.vue'
import ProfilePage from './account/ProfilePage.vue'
import LoginPage from './auth/LoginPage.vue'
import RegisterPage from './auth/RegisterPage.vue'
import StorefrontLayout from '@/layouts/StorefrontLayout.vue'
import ToastHost from '@/components/ui/ToastHost.vue'

const product = (over: Record<string, unknown> = {}) => ({ id: 'p1', sku: 'S1', name: 'Blue shirt', description: 'Soft cotton', price: 25, categoryName: 'Shirts', stockQuantity: 5, status: 1, ...over })

let wrapper: VueWrapper | null = null
async function mountPage(component: object, url = '/', options: { auth?: boolean; cart?: boolean } = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  if (options.auth) {
    const auth = useAuthStore()
    auth.accessToken = 'token'
    auth.user = { id: 'u1', email: 'a@b.c', fullName: 'Ann Lee' }
  }
  if (options.cart) {
    const cart = useCartStore()
    cart.addItem({ productId: 'p1', productName: 'Blue shirt', unitPrice: 25 }, 2)
  }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ['/', '/products', '/products/:id', '/cart', '/checkout', '/auth/login', '/auth/register', '/account/orders', '/account/profile', '/payment/result', '/order-confirmation/:id'].map((path) => ({
      path,
      component: { template: '<div />' },
    })),
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
  productsApi.featured.mockResolvedValue([product(), product({ id: 'p2', name: 'Red hat', stockQuantity: 0 })])
  productsApi.list.mockResolvedValue({ items: [product(), product({ id: 'p2', name: 'Red hat' })], totalCount: 45, pageNumber: 1, pageSize: 20 })
  productsApi.get.mockResolvedValue(product())
  paymentsApi.methods.mockResolvedValue([{ code: 'COD', isGateway: false }, { code: 'VNPay', isGateway: true }])
  shippingApi.feeFor.mockResolvedValue({ fee: 30000, isFree: false, freeShippingThreshold: 500000 })
  ordersApi.create.mockResolvedValue({ orderId: 'o1' })
  ordersApi.myOrders.mockResolvedValue([])
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = null
  document.body.innerHTML = ''
})

const labelled = (w: VueWrapper, text: string | RegExp) => {
  const label = w.findAll('label').find((l) => (typeof text === 'string' ? l.text().startsWith(text) : text.test(l.text())))
  if (!label) throw new Error('no label ' + text)
  return w.get('#' + CSS.escape(label.attributes('for')!))
}

describe('Home and product list', () => {
  it('Home shows the featured products, has no accessibility violation and offers a retry when loading fails', async () => {
    const { wrapper: w } = await mountPage(HomePage)
    expect(w.findAll('li').length).toBe(2)
    expect(w.text()).toContain('Out of stock')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    w.unmount()

    productsApi.featured.mockRejectedValueOnce(new Error('down'))
    const { wrapper: failed } = await mountPage(HomePage)
    expect(failed.get('[role=alert]').text()).toContain('could not be loaded')
    productsApi.featured.mockResolvedValue([product()])
    await failed.get('[role=alert] button').trigger('click')
    await flushPromises()
    expect(failed.findAll('li').length).toBe(1)
  })

  it('the list has labelled filters, a page counter and an empty state that clears the search', async () => {
    const { wrapper: w } = await mountPage(ProductListPage)
    expect(labelled(w, 'Search')).toBeTruthy()
    expect(labelled(w, 'Sort by')).toBeTruthy()
    expect(w.text()).toContain('Page 1 of 3')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()

    productsApi.list.mockResolvedValue({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 })
    await labelled(w, 'Search').setValue('zzz')
    await flushPromises()
    expect(w.text()).toContain('No products found.')
    productsApi.list.mockResolvedValue({ items: [product()], totalCount: 1, pageNumber: 1, pageSize: 20 })
    await w.get('button:not([type=submit])').trigger('click')
    await flushPromises()
    expect((labelled(w, 'Search').element as HTMLInputElement).value).toBe('')
    expect(w.findAll('li').length).toBe(1)
  })
})

describe('Product detail', () => {
  it('adds the chosen quantity to the cart and says so', async () => {
    const { wrapper: w } = await mountPage(ProductDetailPage, '/products/p1')
    const host = mount(ToastHost, { attachTo: document.body, global: { plugins: [testI18n] } })
    expect(w.get('h1').text()).toBe('Blue shirt')
    expect(w.text()).toContain('In stock')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    await w.get('button[aria-label="One more"]').trigger('click')
    const add = w.findAll('button').find((b) => b.text() === 'Add to Cart')!
    await add.trigger('click')
    expect(useCartStore().items[0]).toMatchObject({ productId: 'p1', quantity: 2 })
    expect(host.text()).toContain('Added to cart')
    host.unmount()
  })

  it('an out-of-stock product cannot be added and a missing product says so', async () => {
    productsApi.get.mockResolvedValue(product({ stockQuantity: 0 }))
    const { wrapper: w } = await mountPage(ProductDetailPage, '/products/p1')
    const buttons = w.findAll('button').filter((b) => b.text() === 'Out of stock')
    expect(buttons.length).toBeGreaterThan(0)
    for (const b of buttons) expect(b.attributes('disabled')).toBeDefined()
    w.unmount()

    productsApi.get.mockResolvedValue(null)
    const { wrapper: missing } = await mountPage(ProductDetailPage, '/products/zzz')
    expect(missing.text()).toContain('could not be found')
  })
})

describe('Cart', () => {
  it('has named steppers and remove buttons, changes the quantity and shows the totals', async () => {
    const { wrapper: w } = await mountPage(CartPage, '/cart', { cart: true })
    expect(w.text()).toContain('$50.00')
    expect(w.get('[aria-label="Remove Blue shirt from the cart"]')).toBeTruthy()
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    await w.get('button[aria-label="One fewer"]').trigger('click')
    expect(useCartStore().items[0].quantity).toBe(1)
    await w.get('[aria-label="Remove Blue shirt from the cart"]').trigger('click')
    await flushPromises()
    expect(w.text()).toContain('Your cart is empty.')
  })
})

describe('Checkout', () => {
  async function fillAddress(w: VueWrapper) {
    await labelled(w, 'Street address').setValue('1 Main St')
    await labelled(w, 'Phone').setValue('0901234567')
    await labelled(w, 'City').setValue('Hanoi')
    await labelled(w, 'Country').setValue('Vietnam')
  }

  it('step one says what is missing, under the field, and does not move on', async () => {
    const { wrapper: w } = await mountPage(CheckoutPage, '/checkout', { auth: true, cart: true })
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    await w.get('form').trigger('submit')
    expect(w.findAll('[aria-invalid=true]').length).toBe(4)
    for (const el of w.findAll('[aria-invalid=true]')) expect(document.getElementById(el.attributes('aria-describedby')!)).toBeTruthy()
    expect(w.find('li[aria-current=step]').text()).toContain('Shipping')

    await labelled(w, 'Phone').setValue('12')
    await w.get('form').trigger('submit')
    expect(w.text()).toContain('Enter a valid phone number')
  })

  it('walks the three steps, shows the delivery fee and the amount on the button, and places the order with today\'s payload', async () => {
    const { wrapper: w, router } = await mountPage(CheckoutPage, '/checkout', { auth: true, cart: true })
    await fillAddress(w)
    await w.get('form').trigger('submit')
    await flushPromises()
    expect(w.find('li[aria-current=step]').text()).toContain('Payment')
    expect(w.get('fieldset legend').text()).toBe('Payment Method')
    expect(w.findAll('input[type=radio]').length).toBe(2)
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()

    const next = w.findAll('button').find((b) => b.text().startsWith('Review Order'))!
    await next.trigger('click')
    await flushPromises()
    expect(shippingApi.feeFor).toHaveBeenCalledWith(50, '', [{ productId: 'p1', quantity: 2 }])
    expect(w.text()).toContain('Delivery')
    const place = w.findAll('button').find((b) => b.text().startsWith('Place Order'))!
    expect(place.text()).toContain('₫')

    await place.trigger('click')
    await flushPromises()
    expect(ordersApi.create).toHaveBeenCalledTimes(1)
    expect(ordersApi.create.mock.calls[0][0]).toEqual({
      customerId: 'u1',
      customerName: 'Ann Lee',
      customerEmail: 'a@b.c',
      shippingAddress: { street: '1 Main St', ward: '', city: 'Hanoi', state: '', postalCode: '', country: 'Vietnam' },
      customerPhone: '0901234567',
      lineItems: [{ productId: 'p1', productName: 'Blue shirt', unitPrice: 25, quantity: 2 }],
      paymentMethod: 'COD',
    })
    expect(useCartStore().items).toHaveLength(0)
    expect(router.currentRoute.value.fullPath).toBe('/order-confirmation/o1')
  })

  it('an online method sends the customer to the gateway, a refused order shows an alert', async () => {
    paymentsApi.checkout.mockResolvedValue({ paymentUrl: 'about:blank#gateway' })
    const { wrapper: w } = await mountPage(CheckoutPage, '/checkout', { auth: true, cart: true })
    await fillAddress(w)
    await w.get('form').trigger('submit')
    await flushPromises()
    await w.findAll('input[type=radio]')[1].setValue(true)
    await w.findAll('button').find((b) => b.text().startsWith('Review Order'))!.trigger('click')
    await flushPromises()
    expect(w.findAll('button').find((b) => b.text().startsWith('Place order and pay'))).toBeTruthy()

    ordersApi.create.mockRejectedValueOnce(new Error('boom'))
    await w.findAll('button').find((b) => b.text().startsWith('Place order and pay'))!.trigger('click')
    await flushPromises()
    expect(w.get('[role=alert]').text()).toContain('Failed to place order')
    expect(paymentsApi.checkout).not.toHaveBeenCalled()
  })

  it('the delivery fee line says when it could not be worked out', async () => {
    shippingApi.feeFor.mockRejectedValue(new Error('x'))
    const { wrapper: w } = await mountPage(CheckoutPage, '/checkout', { auth: true, cart: true })
    await fillAddress(w)
    await w.get('form').trigger('submit')
    await flushPromises()
    await w.findAll('button').find((b) => b.text().startsWith('Review Order'))!.trigger('click')
    await flushPromises()
    expect(w.text()).toContain('Could not be worked out')
  })
})

describe('Payment result and order confirmation', () => {
  it.each([
    ['Captured', 'Payment received'],
    ['Failed', 'The payment was not completed'],
    ['Cancelled', 'This payment was cancelled'],
    ['Pending', 'waiting for payment'],
  ])('shows a %s payment in words with one status', async (status, text) => {
    paymentsApi.status.mockResolvedValue({ paymentId: 'x', orderId: 'o1', status, provider: 'VNPay', method: 'VNPay', amount: 1000, currency: 'VND', failureReason: null, capturedAt: null })
    const { wrapper: w } = await mountPage(PaymentResultPage, '/payment/result?orderId=o1', { auth: true })
    expect(w.text()).toContain(text)
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
  })

  it('an unverified return is explained and the confirmation page states the total and the delivery fee', async () => {
    paymentsApi.status.mockResolvedValue(null)
    const { wrapper: w } = await mountPage(PaymentResultPage, '/payment/result?orderId=o1&status=invalid', { auth: true })
    expect(w.text()).toContain('could not verify the payment return')
    w.unmount()

    ordersApi.get.mockResolvedValue({ orderId: 'o1', orderNo: 'SO-1', status: 'Confirmed', totalAmount: 150000, shippingFee: 30000, orderDate: '2026-11-02', paymentMethod: 'COD', lineItems: [] })
    const { wrapper: c } = await mountPage(OrderConfirmationPage, '/order-confirmation/o1', { auth: true })
    expect(c.text()).toContain('SO-1')
    expect(c.text()).toContain('delivery')
    expect(c.text()).toContain('pay the courier')
    expect(await axe(c.element as HTMLElement)).toHaveNoViolations()
  })
})

describe('Account', () => {
  it('lists the orders with a translated status, or says there are none', async () => {
    const { wrapper: empty } = await mountPage(OrderHistoryPage, '/account/orders', { auth: true })
    expect(empty.text()).toContain("You haven't placed any orders yet.")
    expect(await axe(empty.element as HTMLElement)).toHaveNoViolations()
    empty.unmount()

    ordersApi.myOrders.mockResolvedValue([{ orderId: 'o1', orderNo: 'SO-1', status: 'Delivered', totalAmount: 10, orderDate: '2026-11-01', customerName: 'Ann' }])
    const { wrapper: w } = await mountPage(OrderHistoryPage, '/account/orders', { auth: true })
    expect(w.text()).toContain('SO-1')
    expect(w.text()).toContain('Delivered')
  })

  it('the profile shows the name and e-mail and signs out', async () => {
    const { wrapper: w, router } = await mountPage(ProfilePage, '/account/profile', { auth: true })
    expect(w.text()).toContain('Ann Lee')
    expect(w.text()).toContain('a@b.c')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    await w.get('button').trigger('click')
    await flushPromises()
    expect(useAuthStore().isAuthenticated).toBe(false)
    expect(router.currentRoute.value.fullPath).toBe('/')
  })
})

describe('Sign in and register', () => {
  it('sign in has labelled required fields, shows a refusal in an alert and goes to where the customer was heading', async () => {
    const { wrapper: w, router } = await mountPage(LoginPage, '/auth/login?redirect=/checkout')
    expect(labelled(w, 'Email').attributes('required')).toBeDefined()
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    const auth = useAuthStore()
    const login = vi.spyOn(auth, 'login').mockRejectedValueOnce(new Error('401'))
    await labelled(w, 'Email').setValue('a@b.c')
    await labelled(w, 'Password').setValue('secret')
    await w.get('form').trigger('submit')
    await flushPromises()
    expect(w.get('[role=alert]').text()).toContain('Invalid email or password.')
    login.mockResolvedValueOnce(undefined)
    await w.get('form').trigger('submit')
    await flushPromises()
    expect(login).toHaveBeenCalledWith('a@b.c', 'secret')
    expect(router.currentRoute.value.fullPath).toBe('/checkout')
  })

  it('register is translated, labelled and reports a failure; the Vietnamese texts are used when the language is Vietnamese', async () => {
    testI18n.global.locale.value = 'vi'
    const { wrapper: w } = await mountPage(RegisterPage, '/auth/register')
    expect(w.get('h1').text()).toBe('Tạo tài khoản')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
    const auth = useAuthStore()
    vi.spyOn(auth, 'register').mockRejectedValueOnce(new Error('409'))
    await labelled(w, 'Tên').setValue('Ann')
    await labelled(w, 'Họ').setValue('Lee')
    await labelled(w, 'Email').setValue('a@b.c')
    await labelled(w, 'Mật khẩu').setValue('password1')
    await w.get('form').trigger('submit')
    await flushPromises()
    expect(w.get('[role=alert]').text()).toContain('Đăng ký không thành công')
  })
})

describe('Shell', () => {
  it('has a skip link, a labelled cart link with the count, and a menu drawer that opens from the keyboard and closes with Esc', async () => {
    const { wrapper: w } = await mountPage(StorefrontLayout, '/', { cart: true })
    expect(w.get('a[href="#main"]').text()).toBe('Skip to content')
    expect(w.get('a[href="/cart"]').attributes('aria-label')).toBe('Cart, 2 item(s)')
    expect(w.get('main#main')).toBeTruthy()
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()

    const menu = w.get('button[aria-label="Open the menu"]')
    ;(menu.element as HTMLButtonElement).focus()
    await menu.trigger('click')
    await flushPromises()
    const dialog = document.querySelector('[role=dialog]') as HTMLElement
    expect(dialog).toBeTruthy()
    expect(dialog.contains(document.activeElement)).toBe(true)
    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await flushPromises()
    expect(document.querySelector('[role=dialog]')).toBeNull()
  })

  it('changes the language, and signed-in customers see their name and a sign-out', async () => {
    const { wrapper: w } = await mountPage(StorefrontLayout, '/', { auth: true })
    expect(w.text()).toContain('Ann Lee')
    await w.get('select').setValue('vi')
    expect(useLocaleStore().locale).toBe('vi')
  })
})

describe('Translations', () => {
  it('every key the storefront uses exists in English and Vietnamese', async () => {
    const files = import.meta.glob(['../**/*.vue', '../**/*.ts', '!../**/*.test.ts', '!../test/**', '!../i18n/**'], { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi_ = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const lookup = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    // keys built at run time: payment method names / hints, payment and order statuses, chat tools
    const dynamic = ['COD', 'VNPay', 'MoMo', 'ZaloPay'].flatMap((m) => [`checkout.methods.${m}`, `checkout.methodHints.${m}`])
    const missing: string[] = []
    for (const src of Object.values(files)) {
      for (const k of [...[...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g)].map((m) => m[1]), ...dynamic]) {
        if (typeof lookup(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof lookup(vi_, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect([...new Set(missing)]).toEqual([])
  })
})
