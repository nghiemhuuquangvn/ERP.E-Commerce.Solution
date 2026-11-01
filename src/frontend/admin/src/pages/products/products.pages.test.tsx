import { describe, it, expect, vi, beforeEach } from 'vitest'
import type { ComponentType } from 'react'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { axe } from '@/test/axe'

// Every Product API goes through the one axios client: answer every GET with an empty list that works as
// both a paged result and a plain array.
vi.mock('@/api/client', () => {
  const empty = () => Promise.resolve({ data: Object.assign([], { items: [], totalCount: 0, total: 0, products: [], page: 1, pageSize: 20 }) })
  const none = () => Promise.resolve({ data: {} })
  return { apiClient: { get: vi.fn(empty), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) } }
})

import { BrandsPage } from './BrandsPage'
import { CategoriesPage } from './CategoriesPage'
import { PriceListsPage } from './PriceListsPage'
import { ProductAttributesPage } from './ProductAttributesPage'
import { ProductFormPage } from './ProductFormPage'
import { ProductImagesTab } from './ProductImagesTab'
import { ProductListPage } from './ProductListPage'
import { ProductUomConversionsTab } from './ProductUomConversionsTab'
import { ProductVariantsTab } from './ProductVariantsTab'

function renderAt(ui: React.ReactElement, path = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  useAuthStore.setState({
    user: { id: 'u1', email: 'a@b.c', tenantId: 't1', roles: ['Admin'], permissions: [] },
    isAuthenticated: true,
  })
})

/** The first button outside tables and tab strips: the page's (or tab's) add button. */
async function clickAdd() {
  const add = await waitFor(() => {
    const b = screen.getAllByRole('button').find((x) => x.closest('table') === null && !x.closest('[role=tablist]'))
    if (!b) throw new Error('no add button yet')
    return b
  })
  await userEvent.click(add)
  return screen.findByRole('dialog')
}

// [name, page, empty text (null = the page has no list), required errors expected in the dialog]
const DIALOG_PAGES: Array<[string, ComponentType, string | null]> = [
  ['Brands', BrandsPage, 'No records found.'],
  ['Categories', CategoriesPage, 'No records found.'],
  ['Product attributes', ProductAttributesPage, 'No records found.'],
  ['Price lists', PriceListsPage, null],
  ['Variants tab', () => <ProductVariantsTab productId="p1" />, 'No records found.'],
  ['UoM conversions tab', () => <ProductUomConversionsTab productId="p1" />, 'No records found.'],
]

describe.each(DIALOG_PAGES)('Products: %s', (_name, Page, emptyText) => {
  it('shows the empty state, opens the add dialog, closes with Esc, and is accessible', async () => {
    renderAt(<Page />)
    if (emptyText) expect(await screen.findByText(emptyText)).toBeInTheDocument()

    const dialog = await clickAdd()
    expect(within(dialog).getByRole('heading')).toBeInTheDocument()
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()

    // submit the empty form: errors (if any field is required) are linked to their controls
    const submit = within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')
    expect(submit).toBeTruthy()
    await userEvent.click(submit!)
    await waitFor(() => {
      for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
        const described = control.getAttribute('aria-describedby')
        if (described && !document.getElementById(described)) throw new Error('error not linked')
      }
    })

    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(document.body.style.overflow).not.toBe('hidden')
  })
})

describe('Products: required fields', () => {
  it.each([
    ['Brands', BrandsPage, 2],
    ['Categories', CategoriesPage, 1],
    ['Product attributes', ProductAttributesPage, 2],
    ['Price lists', PriceListsPage, 2],
  ] as Array<[string, ComponentType, number]>)('%s: an empty form shows %i linked required errors', async (_n, Page, expected) => {
    renderAt(<Page />)
    const dialog = await clickAdd()
    await userEvent.click(within(dialog).getAllByRole('button').find((b) => b.getAttribute('type') === 'submit')!)
    await waitFor(() => expect(dialog.querySelectorAll('[aria-invalid="true"]').length).toBeGreaterThanOrEqual(expected))
    for (const control of dialog.querySelectorAll('[aria-invalid="true"]')) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
  })
})

describe('Products: product pages', () => {
  it('the list shows the empty state', async () => {
    renderAt(<ProductListPage />)
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
  })

  it('the list offers Clear when a search hides everything', async () => {
    renderAt(<ProductListPage />)
    await userEvent.type(await screen.findByRole('textbox'), 'zzz')
    expect(await screen.findByText('No results')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear' }))
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
  })

  it('the new product form refuses empty name and SKU with linked errors, and is accessible', async () => {
    const { container } = renderAt(
      <Routes>
        <Route path="/products/new" element={<ProductFormPage />} />
      </Routes>,
      '/products/new',
    )
    await userEvent.click(await screen.findByRole('button', { name: 'Save' }))
    const alerts = await screen.findAllByRole('alert')
    expect(alerts.length).toBe(2)
    const invalid = container.querySelectorAll('[aria-invalid="true"]')
    expect(invalid.length).toBe(2)
    for (const control of invalid) {
      expect(document.getElementById(control.getAttribute('aria-describedby') ?? 'none')).toBeTruthy()
    }
    expect(await axe(container)).toHaveNoViolations()
  })

  it('the images tab shows its empty state and is accessible', async () => {
    const { container } = renderAt(<ProductImagesTab productId="p1" />)
    expect(await screen.findByText('No records found.')).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Products: translations', () => {
  it('every key the Products pages use exists in English and Vietnamese', async () => {
    const files = import.meta.glob('./*.tsx', { query: '?raw', import: 'default', eager: true }) as Record<string, string>
    const en = (await import('@/i18n/locales/en.json')).default as Record<string, unknown>
    const vi = (await import('@/i18n/locales/vi.json')).default as Record<string, unknown>
    const get = (o: Record<string, unknown>, k: string) => k.split('.').reduce<unknown>((a, p) => (a == null ? a : (a as Record<string, unknown>)[p]), o)
    const missing: string[] = []
    for (const [file, src] of Object.entries(files)) {
      if (file.endsWith('.test.tsx')) continue
      const keys = [...src.matchAll(/\bt\('([a-zA-Z0-9_.]+)'/g), ...src.matchAll(/labelKey: '([a-zA-Z0-9_.]+)'/g)].map((m) => m[1])
      for (const k of keys) {
        if (typeof get(en, k) !== 'string') missing.push(`en ${k}`)
        if (typeof get(vi, k) !== 'string') missing.push(`vi ${k}`)
      }
    }
    expect(missing).toEqual([])
  })
})
