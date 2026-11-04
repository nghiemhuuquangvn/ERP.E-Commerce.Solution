jest.mock('@/api/client', () => ({ gatewayClient: { get: jest.fn() }, apiClient: {} }))

import { gatewayClient } from '@/api/client'
import { purchaseApi } from '@/modules/purchase/api'

const get = jest.mocked(gatewayClient.get)
const rows = (n: number) => Array.from({ length: n }, (_, i) => ({ id: `po${i}` }))

describe('purchase order list (the server answers a bare array)', () => {
  beforeEach(() => get.mockReset())

  it('a full page means there may be another one', async () => {
    get.mockResolvedValue({ data: rows(20) })
    const p = await purchaseApi.list(undefined, 1, 20)
    expect(p.items).toHaveLength(20)
    expect(p.total).toBeGreaterThan(20)
  })

  it('a short page is the end, with the real total', async () => {
    get.mockResolvedValue({ data: rows(7) })
    const p = await purchaseApi.list(undefined, 3, 20)
    expect(p.total).toBe(47)
  })

  it('also accepts a { items, totalCount } page', async () => {
    get.mockResolvedValue({ data: { items: rows(2), totalCount: 9 } })
    expect(await purchaseApi.list('SUBMITTED', 1, 20)).toEqual({ items: rows(2), total: 9 })
    expect(get).toHaveBeenCalledWith('/po/api/v1/purchase-orders', { params: { status: 'SUBMITTED', pageNumber: 1, pageSize: 20 } })
  })

  it('counts the waiting orders from the rows returned', async () => {
    get.mockResolvedValue({ data: rows(3) })
    expect(await purchaseApi.count('SUBMITTED')).toBe(3)
  })

})
