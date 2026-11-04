import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'

// R113: the server prices every order / quotation line. The price field shows the system price and is locked unless the user holds sales_order:price_override
// (the permission itself, never the role name); a user without it sends no price at all.
const createOrder = vi.fn()
const createQuotation = vi.fn()
const listProducts = vi.fn()
vi.mock('@/api/orders.api', () => ({ ordersApi: { create: (...a: unknown[]) => createOrder(...a) } }))
vi.mock('@/api/quotations.api', () => ({ quotationsApi: { create: (...a: unknown[]) => createQuotation(...a) } }))
vi.mock('@/api/products.api', () => ({ productsApi: { list: (...a: unknown[]) => listProducts(...a) } }))

// The picker is its own tested component: here it just hands back one product.
const picked = { current: { id: 'p1', name: 'Blue shirt', price: 25 } as { id: string; name: string; price: number } }
vi.mock('@/components/shared/ProductPickerModal', () => ({
  ProductPickerModal: ({ open, onConfirm }: { open: boolean; onConfirm: (p: unknown[]) => void }) =>
    open ? <button onClick={() => onConfirm([picked.current])}>pick the product</button> : null,
}))

import { OrderFormPage } from './OrderFormPage'
import { QuotationFormPage } from '../sales/QuotationFormPage'

function renderAt(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route path="/" element={ui} />
            <Route path="*" element={<div>elsewhere</div>} />
          </Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

const signIn = (permissions: string[], roles = ['Staff']) =>
  useAuthStore.setState({ user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles, permissions }, isAuthenticated: true })

const field = (label: string) => screen.getByText(label).parentElement!.querySelector('input')!

async function fillOrder(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByPlaceholderText('Enter customer ID'), 'c1')
  await user.type(field('Customer Name'), 'An')
  await user.type(field('Customer Email'), 'an@x.y')
  await user.type(screen.getByPlaceholderText('Street'), '1 Main')
  await user.type(screen.getByPlaceholderText('City'), 'Hanoi')
  await user.type(screen.getByPlaceholderText('State'), 'HN')
  await user.type(screen.getByPlaceholderText('Postal Code'), '100000')
  await user.click(screen.getByText('…', { selector: 'button' }))
  await user.click(await screen.findByText('pick the product'))
}

beforeEach(() => {
  vi.clearAllMocks()
  picked.current = { id: 'p1', name: 'Blue shirt', price: 25 }
  createOrder.mockResolvedValue({ orderId: 'o1' })
  createQuotation.mockResolvedValue({ quotationId: 'q1' })
  listProducts.mockResolvedValue({ products: [{ id: 'p1', name: 'Blue shirt', price: 25 }], total: 1 })
})

describe('Order form: the price comes from the system', () => {
  it('without the override permission the price field shows the system price, is read-only, and no price is sent', async () => {
    signIn([])
    const user = userEvent.setup()
    renderAt(<OrderFormPage />)
    await fillOrder(user)

    const price = screen.getByLabelText('Unit Price')
    expect(price).toHaveValue(25)
    expect(price).toHaveAttribute('readonly')
    expect(screen.getByText(/Prices come from the system price list/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createOrder).toHaveBeenCalledTimes(1))
    expect(createOrder.mock.calls[0][0].lineItems).toEqual([{ productId: 'p1', productName: 'Blue shirt', quantity: 1, unitPrice: undefined }])
  })

  it('a user whose role is Admin but who was not granted the permission is locked too', async () => {
    signIn([], ['Admin'])
    const user = userEvent.setup()
    renderAt(<OrderFormPage />)
    await fillOrder(user)

    expect(screen.getByLabelText('Unit Price')).toHaveAttribute('readonly')
  })

  it('with the override permission the price is editable and the typed price is sent', async () => {
    signIn(['sales_order:price_override'])
    const user = userEvent.setup()
    renderAt(<OrderFormPage />)
    await fillOrder(user)

    const price = screen.getByLabelText('Unit Price')
    expect(price).not.toHaveAttribute('readonly')
    expect(screen.getByText(/recorded as a manual price on the order/)).toBeInTheDocument()
    await user.clear(price)
    await user.type(price, '20')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createOrder).toHaveBeenCalledTimes(1))
    expect(createOrder.mock.calls[0][0].lineItems[0].unitPrice).toBe(20)
  })

  it('a product with no system price is refused before anything is sent', async () => {
    picked.current = { id: 'p1', name: 'Blue shirt', price: 0 }
    signIn([])
    const user = userEvent.setup()
    renderAt(<OrderFormPage />)
    await fillOrder(user)

    expect(screen.getAllByText(/has no price set/).length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(createOrder).not.toHaveBeenCalled()
  })

  it('someone who may override is asked to type a price for a product without one', async () => {
    picked.current = { id: 'p1', name: 'Blue shirt', price: 0 }
    signIn(['sales_order:price_override'])
    const user = userEvent.setup()
    renderAt(<OrderFormPage />)
    await fillOrder(user)

    expect(screen.getAllByText(/type the price to use/).length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(createOrder).not.toHaveBeenCalled()

    const price = screen.getByLabelText('Unit Price')
    await user.clear(price)
    await user.type(price, '9')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(createOrder).toHaveBeenCalledTimes(1))
  })

  it('shows the server\'s own refusal instead of a generic text', async () => {
    createOrder.mockRejectedValue({ response: { data: { detail: 'The price of Blue shirt changed from 20.00 to 25.00. Review the prices and try again.' } } })
    signIn([])
    const user = userEvent.setup()
    renderAt(<OrderFormPage />)
    await fillOrder(user)

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(/The price of Blue shirt changed from 20.00 to 25.00/)).toBeInTheDocument()
  })
})

describe('Quotation form: the same rule', () => {
  async function fillQuotation(user: ReturnType<typeof userEvent.setup>) {
    await user.type(screen.getByText('Customer ID').parentElement!.querySelector('input')!, 'c1')
    await user.type(field('Customer Name'), 'An')
    await user.type(field('Customer Email'), 'an@x.y')
    await waitFor(() => expect(screen.getByRole('option', { name: 'Blue shirt' })).toBeInTheDocument())
    await user.selectOptions(screen.getByRole('combobox'), 'p1')
  }

  it('without the permission the price is read-only and not sent', async () => {
    signIn([])
    const user = userEvent.setup()
    renderAt(<QuotationFormPage />)
    await fillQuotation(user)

    const price = screen.getByLabelText('Unit Price')
    expect(price).toHaveValue(25)
    expect(price).toHaveAttribute('readonly')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createQuotation).toHaveBeenCalledTimes(1))
    expect(createQuotation.mock.calls[0][0].lineItems[0].unitPrice).toBeUndefined()
  })

  it('with the permission the typed price is sent', async () => {
    signIn(['sales_order:price_override'])
    const user = userEvent.setup()
    renderAt(<QuotationFormPage />)
    await fillQuotation(user)

    const price = screen.getByLabelText('Unit Price')
    await user.clear(price)
    await user.type(price, '19.5')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(createQuotation).toHaveBeenCalledTimes(1))
    expect(createQuotation.mock.calls[0][0].lineItems[0].unitPrice).toBe(19.5)
  })
})
