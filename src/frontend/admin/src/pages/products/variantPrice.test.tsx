import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { axe } from '@/test/axe'

// R111: the Variants tab shows the retail price (from the price lists) beside the cost and sets it through POST pricing/variants.
const listVariants = vi.fn()
const productPrices = vi.fn()
const setVariantPrice = vi.fn()

vi.mock('@/api/products.api', () => ({ productsApi: { listVariants: (...a: unknown[]) => listVariants(...a), deleteVariant: vi.fn() } }))
vi.mock('@/api/product/attributes.api', () => ({ attributesApi: { list: () => Promise.resolve([]), getValues: () => Promise.resolve([]) } }))
vi.mock('@/api/product/pricing.api', () => ({
  pricingApi: { productPrices: (...a: unknown[]) => productPrices(...a), setVariantPrice: (...a: unknown[]) => setVariantPrice(...a) },
}))

import { ProductVariantsTab } from './ProductVariantsTab'

const variants = [
  { id: 'v1', sku: 'TS-1-S', cost: 40, barcode: null },
  { id: 'v2', sku: 'TS-1-M', cost: 42, barcode: null },
]

function renderTab() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <ProductVariantsTab productId="p1" />
      </ToastProvider>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  listVariants.mockResolvedValue(variants)
  productPrices.mockResolvedValue([
    { variantId: 'v1', sku: 'TS-1-S', cost: 40, retailPrice: 120 },
    { variantId: 'v2', sku: 'TS-1-M', cost: 42, retailPrice: null },
  ])
  setVariantPrice.mockResolvedValue({})
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.c', tenantId: 't1', roles: ['Admin'], permissions: ['products:edit'] },
    isAuthenticated: true,
  })
})

describe('Variants tab: retail price', () => {
  it('shows the retail price beside the cost, and "Not set" for a variant without one', async () => {
    renderTab()

    const row1 = (await screen.findByText('TS-1-S')).closest('tr')!
    expect(within(row1).getByText('120.00')).toBeInTheDocument()
    expect(within(row1).getByText('40.00')).toBeInTheDocument()
    const row2 = screen.getByText('TS-1-M').closest('tr')!
    expect(within(row2).getByText('Not set')).toBeInTheDocument()
  })

  it('sets the price: sends the variant, base price and discounted price, then closes', async () => {
    const user = userEvent.setup()
    renderTab()
    const row = (await screen.findByText('TS-1-M')).closest('tr')!

    await user.click(within(row).getByRole('button', { name: 'Set price' }))
    const dialog = await screen.findByRole('dialog')
    const base = within(dialog).getByLabelText(/^Price/)
    await user.clear(base)
    await user.type(base, '150')
    await user.type(within(dialog).getByLabelText(/Discounted price/), '129.5')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(setVariantPrice).toHaveBeenCalledWith({ variantId: 'v2', basePrice: 150, discountedPrice: 129.5 }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('sends no discounted price when the field is left empty', async () => {
    const user = userEvent.setup()
    renderTab()
    const row = (await screen.findByText('TS-1-S')).closest('tr')!

    await user.click(within(row).getByRole('button', { name: 'Set price' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(setVariantPrice).toHaveBeenCalledWith({ variantId: 'v1', basePrice: 120, discountedPrice: null }))
  })

  it('refuses a discounted price that is not below the price and sends nothing', async () => {
    const user = userEvent.setup()
    renderTab()
    const row = (await screen.findByText('TS-1-S')).closest('tr')!

    await user.click(within(row).getByRole('button', { name: 'Set price' }))
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/Discounted price/), '130')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('The discounted price must be lower than the price.')).toBeInTheDocument()
    expect(setVariantPrice).not.toHaveBeenCalled()
  })

  it('shows the server refusal inside the dialog and keeps it open', async () => {
    setVariantPrice.mockRejectedValue({ response: { data: { detail: 'Variant with ID v1 not found' } } })
    const user = userEvent.setup()
    renderTab()
    const row = (await screen.findByText('TS-1-S')).closest('tr')!

    await user.click(within(row).getByRole('button', { name: 'Set price' }))
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByRole('alert')).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })

  it('offers no Set price button without the products:edit permission', async () => {
    useAuthStore.setState({ user: { id: 'u1', email: 'a@b.c', tenantId: 't1', roles: ['Viewer'], permissions: [] }, isAuthenticated: true })
    renderTab()

    await screen.findByText('TS-1-S')
    expect(screen.queryByRole('button', { name: 'Set price' })).not.toBeInTheDocument()
  })

  it('has no accessibility violations with the dialog open', async () => {
    const user = userEvent.setup()
    renderTab()
    const row = (await screen.findByText('TS-1-S')).closest('tr')!
    await user.click(within(row).getByRole('button', { name: 'Set price' }))
    const dialog = await screen.findByRole('dialog')

    expect(await axe(dialog)).toHaveNoViolations()
  })
})
