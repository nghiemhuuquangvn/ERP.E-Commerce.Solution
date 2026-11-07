import { vi } from 'vitest'
import type { ReactElement } from 'react'
import { render } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ToastProvider } from '@/components/ui/Toast'
import { useAuthStore } from '@/stores/auth.store'
import { usePlantStore } from '@/components/manufacturing/shared'
import { apiClient } from '@/api/client'

// Shared by the Manufacturing page tests: a router-like fake of the HTTP client (the first matching substring wins), the signed-in user and the plant.
export const get = vi.mocked(apiClient.get)
export const post = vi.mocked(apiClient.post)
export const put = vi.mocked(apiClient.put)
export const del = vi.mocked(apiClient.delete)
export const BASE = '/mfg/api/v1/manufacturing'

export const PLANT = { id: 'p1', code: 'PL01', name: 'Binh Duong', timeZoneId: 'Asia/Ho_Chi_Minh', rawMaterialWarehouseId: 'w1', finishedGoodsWarehouseId: 'w2', quarantineWarehouseId: 'w3', scrapWarehouseId: null, defaultCalendarId: null, isActive: true }

type Reply = unknown | ((url: string, config?: { params?: Record<string, unknown> }) => unknown)

/** Answers GET requests: the entries are tried in order, the first whose key is contained in the url wins; an unknown url answers an empty array. */
export function answer(entries: [string, Reply][]) {
  get.mockImplementation(((url: string, config?: { params?: Record<string, unknown> }) => {
    const hit = entries.find(([key]) => url.includes(key))
    const data = hit ? (typeof hit[1] === 'function' ? (hit[1] as (u: string, c?: unknown) => unknown)(url, config) : hit[1]) : []
    return Promise.resolve({ data })
  }) as never)
}

export function setup(permissions: string[] = ['manufacturing:masterdata:view', 'manufacturing:masterdata:manage', 'manufacturing:bom:view', 'manufacturing:bom:manage', 'manufacturing:bom:approve', 'manufacturing:routing:view', 'manufacturing:routing:manage', 'manufacturing:routing:approve', 'manufacturing:settings:manage', 'manufacturing:item-profile:manage']) {
  get.mockReset(); post.mockReset(); put.mockReset(); del.mockReset()
  post.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  put.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  del.mockImplementation((() => Promise.resolve({ data: {} })) as never)
  useAuthStore.setState({ user: { id: 'me', email: 'me@b.c', tenantId: 't1', roles: [], permissions }, isAuthenticated: true } as never)
  usePlantStore.setState({ plantId: 'p1' })
  answer([[`${BASE}/plants`, [PLANT]]])
}

export function renderAt(ui: ReactElement, path = '/', route = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ToastProvider>
        <MemoryRouter initialEntries={[path]}>
          <Routes><Route path={route} element={ui} /></Routes>
        </MemoryRouter>
      </ToastProvider>
    </QueryClientProvider>,
  )
}

export const refusal = (detail: string, errorCode = 'MFG_VALIDATION') => ({ response: { status: 400, data: { detail, errorCode } } })
