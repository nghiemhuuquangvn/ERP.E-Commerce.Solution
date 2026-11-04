jest.mock('@/api/client', () => ({ gatewayClient: { get: jest.fn() }, apiClient: {} }))

import { gatewayClient } from '@/api/client'
import { purchaseApi } from '@/modules/purchase/api'
import { procurementApi } from '@/modules/purchase/procurementApi'
import { onePage, pageOfArray } from '@/modules/usePagedList'

const get = jest.mocked(gatewayClient.get)
const rows = (n: number) => Array.from({ length: n }, (_, i) => ({ id: `po${i}` }))

describe('purchase order list (the server filters, counts and pages in the database)', () => {
  beforeEach(() => get.mockReset())

  it('sends every filter and reads the total', async () => {
    get.mockResolvedValue({ data: { items: rows(2), totalCount: 9 } })
    const page = await purchaseApi.list({ status: 'SUBMITTED', dateFrom: '2026-11-01', dateTo: '2026-11-30', search: 'PO-1' }, 2, 20)
    expect(page).toEqual({ items: rows(2), total: 9 })
    expect(get).toHaveBeenCalledWith('/po/api/v1/purchase-orders', {
      params: { status: 'SUBMITTED', dateFrom: '2026-11-01', dateTo: '2026-11-30', search: 'PO-1', pageNumber: 2, pageSize: 20 },
    })
  })

  it('counts the waiting orders with the total of a one-row page', async () => {
    get.mockResolvedValue({ data: { items: rows(1), totalCount: 37 } })
    expect(await purchaseApi.count('SUBMITTED')).toBe(37)
    expect(get).toHaveBeenCalledWith('/po/api/v1/purchase-orders', { params: { status: 'SUBMITTED', pageNumber: 1, pageSize: 1 } })
  })
})

describe('lists that answer a bare array', () => {
  beforeEach(() => get.mockReset())

  it('a full page means there may be another one, a short page is the end', () => {
    expect(pageOfArray(rows(20), 1, 20).total).toBeGreaterThan(20)
    expect(pageOfArray(rows(7), 3, 20).total).toBe(47)
  })

  it('a list answered whole is one page', () => {
    expect(onePage(rows(3))).toEqual({ items: rows(3), total: 3 })
  })

  it('suppliers: active / inactive become isActive, search and paging go to the server', async () => {
    get.mockResolvedValue({ data: rows(3) })
    const page = await procurementApi.suppliers({ status: 'inactive', search: 'plastics' }, 1)
    expect(page.items).toHaveLength(3)
    expect(get).toHaveBeenCalledWith('/po/api/v1/suppliers', { params: { isActive: false, search: 'plastics', pageNumber: 1, pageSize: 50 } })
    await procurementApi.suppliers({}, 1)
    expect(get).toHaveBeenLastCalledWith('/po/api/v1/suppliers', { params: { isActive: undefined, search: undefined, pageNumber: 1, pageSize: 50 } })
  })

  it('invoices, returns and RFQs send status, days and text and come back as one page', async () => {
    get.mockResolvedValue({ data: rows(2) })
    const params = { status: 'Approved', dateFrom: '2026-10-01', dateTo: '2026-12-31', search: 'INV' }
    expect(await procurementApi.invoices(params)).toEqual({ items: rows(2), total: 2 })
    expect(get).toHaveBeenLastCalledWith('/po/api/v1/supplier-invoices', { params })
    await procurementApi.returns(params)
    expect(get).toHaveBeenLastCalledWith('/po/api/v1/vendor-returns', { params })
    await procurementApi.rfqs(params)
    expect(get).toHaveBeenLastCalledWith('/po/api/v1/rfqs', { params })
  })
})
