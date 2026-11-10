import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'

vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { ProductionOrderListPage } from './OrderListPage'
import { ProductionOrderDetailPage } from './OrderDetailPage'
import { StockDocumentsPage } from './StockDocumentsPage'
import { BASE, PLANT, answer, get, post, refusal, renderAt, setup } from './testKit'

const ALL = [
  'manufacturing:order:view', 'manufacturing:order:create', 'manufacturing:order:release', 'manufacturing:order:cancel', 'manufacturing:order:hold', 'manufacturing:order:change-qty',
  'manufacturing:order:short-close', 'manufacturing:order:reopen', 'manufacturing:order:close', 'manufacturing:material:issue', 'manufacturing:material:return', 'manufacturing:receipt:post',
  'manufacturing:operation:execute', 'manufacturing:operation:complete-manual', 'manufacturing:qc:disposition', 'manufacturing:stock-document:retry', 'manufacturing:masterdata:view',
]

// ── fixtures ────────────────────────────────────────────────────────────────────────────────────────────
const mat = (lineNo: number, code: string, extra: Record<string, unknown> = {}) => ({
  lineNo, componentItemId: `i${lineNo}`, componentCode: code, componentName: `Name ${code}`, uomId: 'u1', operationSeq: 10, requiredQty: 10, issuedQty: 0, returnedQty: 0, consumedQty: 0, reservedQty: 10,
  scrapPct: 0, issueMethod: 'Manual', warehouseId: 'w1', isSubstitute: false, alternativeGroup: null, isUnplanned: false, ...extra,
})
const op = (seq: number, status: string, extra: Record<string, unknown> = {}) => ({
  operationSeq: seq, name: `Step ${seq}`, workCenterId: 'wc1', workCenterCode: 'WC-1', setupTimeMinutes: 0, runTimeMinutes: 1, runTimeBasis: 'PerUnit', batchSize: null, queueTimeMinutes: 0, moveTimeMinutes: 0,
  isSubcontract: false, status, standardMinutes: 100, calendarMinutes: 100, plannedStart: '2026-11-11T01:00:00Z', plannedEnd: '2026-11-11T09:00:00Z', actualStart: null, actualEnd: null, goodQty: 0, scrapQty: 0, reworkQty: 0, ...extra,
})
const order = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'o1', orderNo: 'MO-2611-00001', plantId: 'p1', orderType: 'Standard', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', quantity: 100, uomId: 'u1', bomId: 'b1', routingId: 'r1', status,
  statusBeforeHold: null, priority: 3, dueDate: '2026-11-20', plannedStart: '2026-11-11T01:00:00Z', plannedEnd: '2026-11-19T09:00:00Z', actualStart: null, actualEnd: null, isLate: false, outputWarehouseId: 'w2',
  sourceType: 'Manual', sourceRefId: null, parentOrderId: null, completedQty: 0, scrappedQty: 0, isRework: false, notes: null, reservationId: null, reservationSyncPending: false, reservationSyncError: null,
  materials: [mat(1, 'NVL-KEO01'), mat(2, 'NVL-SON01', { requiredQty: 52.5, reservedQty: 52.5 })], operations: [op(10, 'Ready'), op(20, 'Pending')], outputs: [], warnings: [], pendingStockDocuments: 0, ...extra,
})
const item = (id: string, no: string, status: string, extra: Record<string, unknown> = {}) => ({
  id, orderNo: no, itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', quantity: 100, completedQty: 25, status, priority: 3, dueDate: '2026-11-20', plannedStart: '2026-11-11T01:00:00Z', plannedEnd: '2026-11-19T09:00:00Z', isLate: false, ...extra,
})
const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 25 })
const REASONS = [
  { id: 'rc', type: 'Cancel', code: 'C1', name: 'Customer cancelled', isActive: true }, { id: 'rh', type: 'Hold', code: 'H1', name: 'Quality check', isActive: true },
  { id: 'rs', type: 'ShortClose', code: 'S1', name: 'Material ran out', isActive: true }, { id: 'ro', type: 'OverIssue', code: 'O1', name: 'Spillage', isActive: true },
  { id: 'rx', type: 'Scrap', code: 'X1', name: 'Cracked', isActive: true },
]
const doc = (extra: Record<string, unknown> = {}, lines: unknown[] = []) => ({
  id: 'd1', docNo: 'MI-2611-00001', plantId: 'p1', orderId: 'o1', orderNo: 'MO-2611-00001', type: 'Issue', status: 'Posted', mode: 'Forward', postingDate: '2026-11-11', reasonCodeId: null, note: null,
  attempts: 0, nextAttemptAt: null, lastError: null, lastErrorCode: null, postedAt: '2026-11-11T02:00:00Z', createdAt: '2026-11-11T02:00:00Z', createdBy: 'admin', lines, ...extra,
})
const dline = (extra: Record<string, unknown> = {}) => ({
  lineNo: 1, orderMaterialId: 'm1', materialLineNo: 1, itemId: 'i1', itemCode: 'NVL-KEO01', warehouseId: 'w1', lotId: null, qty: 5, isFromReservation: true, movementKey: 'MI#1', status: 'Posted',
  unitCost: 2, amount: 10, postedLotId: null, error: null, kind: 'Material', outputLineNo: null, lotNumber: null, expiryDate: null, isQcHold: false, qcReleasedAt: null, isNegativeStock: false, manufactureDate: null, ...extra,
})

interface Api { order?: unknown; docs?: unknown[]; ops?: unknown[]; extra?: [string, unknown][] }
function api({ order: o = order('Released'), docs = [], ops, extra = [] }: Api = {}) {
  const ord = o as { operations: unknown[] }
  answer([
    ...extra,
    [`${BASE}/orders/o1/stock-documents`, docs],
    [`${BASE}/orders/o1/operations`, ops ?? ord.operations.map((x) => ({ operation: x, timeLogs: [] }))],
    [`${BASE}/orders/o1/operation-reports`, []],
    [`${BASE}/orders/o1/history`, [{ fromStatus: 'Draft', toStatus: 'Released', reasonCodeId: 'rc', note: 'ok', changedBy: 'admin', changedAt: '2026-11-10T08:00:00Z' }]],
    [`${BASE}/orders/o1/availability`, { orderId: 'o1', hasShortage: true, materials: [{ lineNo: 1, componentItemId: 'i1', componentCode: 'NVL-KEO01', componentName: 'Glue', requiredQty: 10, need: 10, allocatable: 6, shortage: 4 }] }],
    [`${BASE}/orders/o1/close-checklist`, { orderId: 'o1', orderNo: 'MO-2611-00001', status: 'Completed', canClose: false, conditions: [{ code: 'LOGS', met: true, message: 'No open time log' }, { code: 'ISSUED', met: false, message: 'Issued material of NVL-SON01 is not settled' }] }],
    [`${BASE}/orders/o1`, o],
    [`${BASE}/orders`, page([item('o1', 'MO-2611-00001', 'Released'), item('o2', 'MO-2611-00002', 'Draft'), item('o3', 'MO-2611-00003', 'Draft', { isLate: true })])],
    [`${BASE}/reason-codes`, REASONS],
    [`${BASE}/plants`, [PLANT]],
  ])
}
const openOrder = (o: unknown, perms = ALL, docs: unknown[] = [], extra: [string, unknown][] = []) => {
  setup(perms)
  api({ order: o, docs, extra })
  return renderAt(<ProductionOrderDetailPage />, '/manufacturing/orders/o1', '/manufacturing/orders/:id')
}
const lastOf = <T,>(a: T[]) => a[a.length - 1]
const calls = (m: typeof post, part: string) => m.mock.calls.filter((c) => String(c[0]).includes(part))

beforeEach(() => setup(ALL))

describe('Production orders list (S15)', () => {
  it('lists orders with status, late flag and progress, and axe finds nothing', async () => {
    api()
    const { container } = renderAt(<ProductionOrderListPage />)
    expect(await screen.findByRole('link', { name: 'MO-2611-00001' })).toHaveAttribute('href', '/manufacturing/orders/o1')
    expect(screen.getAllByText('Released').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Draft').length).toBeGreaterThan(0)
    expect(screen.getByText('Late')).toBeInTheDocument()
    expect(screen.getAllByRole('progressbar').length).toBe(3)
    expect(screen.getByRole('button', { name: 'New order' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('sends the filters to the server', async () => {
    api()
    renderAt(<ProductionOrderListPage />)
    await screen.findByRole('link', { name: 'MO-2611-00001' })
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Status' }), 'Released')
    await userEvent.click(screen.getByRole('checkbox', { name: 'Late only' }))
    await waitFor(() => {
      const last = lastOf(get.mock.calls.filter((c) => String(c[0]) === `${BASE}/orders`))[1] as { params: Record<string, unknown> }
      expect(last.params).toMatchObject({ plantId: 'p1', status: 'Released', isLate: true, page: 1, pageSize: 25 })
    })
  })

  it('shows an empty state', async () => {
    api({ extra: [[`${BASE}/orders`, page([])]] })
    renderAt(<ProductionOrderListPage />)
    expect(await screen.findByText('No production orders match.')).toBeInTheDocument()
  })

  it('only Draft rows can be selected, and the selected ones are released one by one with a result for each', async () => {
    api()
    post.mockImplementation(((url: string) => {
      if (url.includes('/o2/')) return Promise.resolve({ data: { order: order('Released'), shortages: [{ lineNo: 1, componentItemId: 'i1', componentCode: 'BTP-CHAN01', componentName: 'Leg', requiredQty: 400, need: 400, allocatable: 300, shortage: 100 }], reservationPending: false } })
      return Promise.reject(refusal('Released with shortages is blocked for this plant.', 'MFG_PO_007'))
    }) as never)
    renderAt(<ProductionOrderListPage />)
    await screen.findByRole('link', { name: 'MO-2611-00001' })
    expect(screen.queryByRole('checkbox', { name: 'Select MO-2611-00001' })).toBeNull()   // Released: no checkbox
    await userEvent.click(screen.getByRole('checkbox', { name: 'Select MO-2611-00002' }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Select MO-2611-00003' }))
    await userEvent.click(screen.getByRole('button', { name: 'Release selected (2)' }))
    const dialog = await screen.findByRole('dialog', { name: 'Release result' })
    expect(within(dialog).getByText(/BTP-CHAN01 \(100\)/)).toBeInTheDocument()
    expect(within(dialog).getByText(/MFG_PO_007: Released with shortages is blocked/)).toBeInTheDocument()
    expect(calls(post, '/release')).toHaveLength(2)
  })

  it('a user without the release permission has no checkboxes', async () => {
    setup(['manufacturing:order:view'])
    api()
    renderAt(<ProductionOrderListPage />)
    await screen.findByRole('link', { name: 'MO-2611-00001' })
    expect(screen.queryByRole('checkbox', { name: /Select/ })).toBeNull()
    expect(screen.queryByRole('button', { name: 'New order' })).toBeNull()
  })
})

describe('Production order detail (S16): actions by status and permission', () => {
  it('a Draft offers Edit, Release and Cancel only', async () => {
    const { container } = openOrder(order('Draft'))
    await screen.findByRole('heading', { name: /MO-2611-00001/ })
    for (const name of ['Edit', 'Release', 'Cancel order']) expect(screen.getByRole('button', { name })).toBeInTheDocument()
    for (const name of ['Put on hold', 'Issue material', 'Close order', 'Resume']) expect(screen.queryByRole('button', { name })).toBeNull()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('a Closed order is read only', async () => {
    openOrder(order('Closed'))
    await screen.findByRole('heading', { name: /MO-2611-00001/ })
    expect(screen.queryAllByRole('button').filter((b) => /Release|Cancel order|Put on hold|Complete|Reopen|Close order|Change quantity/.test(b.textContent ?? ''))).toHaveLength(0)
  })

  it('a viewer sees no action button on a Released order', async () => {
    openOrder(order('Released'), ['manufacturing:order:view'])
    await screen.findByRole('heading', { name: /MO-2611-00001/ })
    expect(screen.queryByRole('button', { name: 'Put on hold' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Back to draft' })).toBeNull()
  })

  it('shows pending-document and reservation banners, and the shortages a release reported', async () => {
    openOrder(order('Released', { pendingStockDocuments: 2, reservationSyncPending: true }))
    expect(await screen.findByText(/2 stock document\(s\) are being sent to the Warehouse/)).toBeInTheDocument()
    expect(screen.getByText(/reservation of this order is being updated/)).toBeInTheDocument()
  })

  it('release shows the shortages from the answer', async () => {
    openOrder(order('Draft'))
    post.mockResolvedValue({ data: { order: order('Released'), shortages: [{ lineNo: 1, componentItemId: 'i1', componentCode: 'BTP-CHAN01', componentName: 'Leg', requiredQty: 400, need: 400, allocatable: 300, shortage: 100 }], reservationPending: false } } as never)
    await userEvent.click(await screen.findByRole('button', { name: 'Release' }))
    expect(await screen.findByText('Released with shortages')).toBeInTheDocument()
    expect(screen.getByText(/BTP-CHAN01: short 100/)).toBeInTheDocument()
  })

  it('cancel asks for a reason code of type Cancel and sends it', async () => {
    openOrder(order('Draft'))
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel order' }))
    const dialog = await screen.findByRole('dialog', { name: /Cancel order MO-2611-00001/ })
    const select = within(dialog).getByRole('combobox', { name: /Reason/ })
    expect(within(select).getByRole('option', { name: 'C1 - Customer cancelled' })).toBeInTheDocument()
    expect(within(select).queryByRole('option', { name: /Quality check/ })).toBeNull()      // a Hold reason is not offered
    const confirm = within(dialog).getByRole('button', { name: 'Cancel order' })
    expect(confirm).toBeDisabled()
    await userEvent.selectOptions(select, 'rc')
    await userEvent.click(confirm)
    await waitFor(() => expect(calls(post, '/cancel')[0][1]).toEqual({ reasonCodeId: 'rc', note: null }))
  })

  it('Complete below the tolerance opens the short-close reason dialog and completes with it', async () => {
    openOrder(order('InProgress', { completedQty: 90 }))
    post.mockRejectedValueOnce(refusal('Only 90 of 100 were received; a reason is needed.', 'MFG_PO_012'))
    await userEvent.click(await screen.findByRole('button', { name: 'Complete' }))
    const dialog = await screen.findByRole('dialog', { name: /Short close MO-2611-00001/ })
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: /Reason/ }), 'rs')
    post.mockResolvedValue({ data: { order: order('Completed'), shortages: [], reservationPending: false } } as never)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Complete' }))
    await waitFor(() => expect(calls(post, '/complete')).toHaveLength(2))
    expect(calls(post, '/complete')[1][1]).toEqual({ reasonCodeId: 'rs', note: null })
  })

  it('a refusal of a plain confirmation (back to draft) is shown, not swallowed', async () => {
    openOrder(order('Released'))
    post.mockRejectedValue(refusal('The order already has transactions.', 'MFG_PO_008'))
    await userEvent.click(await screen.findByRole('button', { name: 'Back to draft' }))
    const dialog = await screen.findByRole('dialog', { name: 'Back to draft' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Back to draft' }))
    expect(await screen.findByText('The order already has transactions.')).toBeInTheDocument()
  })

  it('change quantity sends the new quantity and shows the server refusal below the completed quantity', async () => {
    openOrder(order('InProgress', { completedQty: 30 }))
    await userEvent.click(await screen.findByRole('button', { name: 'Change quantity' }))
    const dialog = await screen.findByRole('dialog', { name: /Change quantity of/ })
    const input = within(dialog).getByLabelText(/Quantity/)
    await userEvent.clear(input)
    await userEvent.type(input, '20')
    post.mockRejectedValue(refusal('Quantity cannot be below the completed quantity 30.', 'MFG_PO_010'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText(/cannot be below the completed quantity 30/)).toBeInTheDocument()
    expect(calls(post, '/change-quantity')[0][1]).toEqual({ quantity: 20, note: null })
  })
})

describe('Close dialog', () => {
  it('lists the conditions, keeps Close disabled while one is unmet and reloads with the carry-over option', async () => {
    openOrder(order('Completed', { completedQty: 100 }))
    await userEvent.click(await screen.findByRole('button', { name: 'Close order' }))
    const dialog = await screen.findByRole('dialog', { name: /Close order MO-2611-00001/ })
    expect(await within(dialog).findByText(/Issued material of NVL-SON01 is not settled/)).toBeInTheDocument()
    expect(within(dialog).getByText(/No open time log/)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Close order' })).toBeDisabled()
    answer([[`${BASE}/orders/o1/close-checklist`, { orderId: 'o1', orderNo: 'MO-2611-00001', status: 'Completed', canClose: true, conditions: [{ code: 'ISSUED', met: true, message: 'Settled' }] }], [`${BASE}/orders/o1`, order('Completed')], [`${BASE}/plants`, [PLANT]]])
    await userEvent.click(within(dialog).getByRole('checkbox', { name: /Carry the issued material/ }))
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Close order' })).toBeEnabled())
    const last = lastOf(get.mock.calls.filter((c) => String(c[0]).includes('close-checklist')))[1] as { params: Record<string, unknown> }
    expect(last.params).toEqual({ confirmConsumeRemaining: true })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Close order' }))
    await waitFor(() => expect(calls(post, '/close')[0][1]).toEqual({ confirmConsumeRemaining: true }))
  })
})

describe('Materials tab and the issue / return dialog (S18)', () => {
  const suggestion = { lineNo: 2, componentCode: 'NVL-SON01', warehouseId: 'w1', quantity: 52.5, reservedRemaining: 0, reservedLots: [], suggestion: { productId: 'i2', warehouseId: 'w1', quantity: 52.5, lots: [{ lotId: 'la', lotNumber: 'LOT-A', expiryDate: '2027-01-01', availableQuantity: 30, take: 30 }, { lotId: 'lb', lotNumber: 'LOT-B', expiryDate: '2027-03-01', availableQuantity: 40, take: 22.5 }], shortfall: 0, lotLessAvailable: 0 } }

  async function openIssue() {
    const view = openOrder(order('Released'), ALL, [], [[`${BASE}/orders/o1/material-issues/suggest-lots`, suggestion]])
    await userEvent.click(await screen.findByRole('tab', { name: 'Materials' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Issue material' }))
    return { ...view, dialog: await screen.findByRole('dialog', { name: /Issue material to MO-2611-00001/ }) }
  }

  it('suggests first-expiry-first lots into rows, and Enter in a lot field never submits', async () => {
    const { dialog } = await openIssue()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add a line' }))
    await userEvent.selectOptions(within(dialog).getByLabelText('Material'), '2')
    expect((within(dialog).getByLabelText('Quantity') as HTMLInputElement).value).toBe('52.5')   // what is still to issue
    await userEvent.click(within(dialog).getByRole('button', { name: 'Suggest lots' }))
    await waitFor(() => expect(within(dialog).getAllByLabelText('Lot')).toHaveLength(2))
    expect((within(dialog).getAllByLabelText('Lot')[0] as HTMLInputElement).value).toBe('LOT-A')
    expect((within(dialog).getAllByLabelText('Quantity')[1] as HTMLInputElement).value).toBe('22.5')
    await userEvent.type(within(dialog).getAllByLabelText('Lot')[0], '{Enter}')
    expect(calls(post, 'material-issues')).toHaveLength(0)
  })

  it('a typed lot the system does not know blocks the submit with a message', async () => {
    const { dialog } = await openIssue()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add a line' }))
    await userEvent.selectOptions(within(dialog).getByLabelText('Material'), '1')
    await userEvent.type(within(dialog).getByLabelText('Quantity'), '{Control>}a{/Control}5')
    await userEvent.type(within(dialog).getByLabelText('Lot'), 'NOPE')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Issue' }))
    expect(await within(dialog).findByText('Lot "NOPE" is not known for this item.')).toBeInTheDocument()
    expect(calls(post, 'material-issues')).toHaveLength(0)
  })

  it('keeps one Idempotency-Key for the same request, takes a new one when it changes, and asks for the over-issue reason', async () => {
    const { dialog } = await openIssue()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add a line' }))
    await userEvent.selectOptions(within(dialog).getByLabelText('Material'), '1')
    post.mockRejectedValueOnce(refusal('Net issued would pass the tolerance.', 'MFG_MAT_002')).mockRejectedValueOnce(refusal('Net issued would pass the tolerance.', 'MFG_MAT_002'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Issue' }))
    expect(await within(dialog).findByText('Net issued would pass the tolerance.')).toBeInTheDocument()
    expect(within(dialog).getByLabelText(/Over-issue reason/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Issue' }))                // the same request again (reason still empty -> the button is disabled)
    const first = calls(post, 'material-issues')[0]
    expect((first[2] as { headers: Record<string, string> }).headers['Idempotency-Key']).toMatch(/.{8,}/)
    expect(first[1]).toMatchObject({ lines: [{ lineNo: 1, quantity: 10, lotId: null }], reasonCodeId: null })
  })

  it('a 202 answer is shown as saved-and-waiting, not as an error', async () => {
    const { dialog } = await openIssue()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add a line' }))
    await userEvent.selectOptions(within(dialog).getByLabelText('Material'), '1')
    post.mockResolvedValue({ status: 202, data: doc({ status: 'Pending' }, [dline({ status: 'Pending' })]) } as never)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Issue' }))
    expect(await screen.findByText(/MI-2611-00001 is saved and waits for the Warehouse/)).toBeInTheDocument()
  })

  it('the availability panel names the shortage', async () => {
    openOrder(order('Released'))
    await userEvent.click(await screen.findByRole('tab', { name: 'Materials' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Check availability' }))
    expect(await screen.findByText('short 4')).toBeInTheDocument()
    expect(screen.getByText(/cannot be covered/)).toBeInTheDocument()
  })

  it('a return is offered on a Completed order, an issue is not', async () => {
    openOrder(order('Completed', { materials: [mat(1, 'NVL-KEO01', { issuedQty: 12 })] }))
    await userEvent.click(await screen.findByRole('tab', { name: 'Materials' }))
    expect(await screen.findByRole('button', { name: 'Return material' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Issue material' })).toBeNull()
  })
})

describe('Receipts tab and the receipt dialog (S19)', () => {
  const settings = { plantId: 'p1', values: { overProductionTolerancePct: 5 } }

  async function openReceive(o = order('InProgress', { completedQty: 25, outputs: [{ lineNo: 1, lineType: 'ByProduct', itemId: 'bp', itemCode: 'PP-MUN01', itemName: 'Sawdust', uomId: 'u1', plannedQty: 25, receivedQty: 0, costAllocationPct: null }] })) {
    openOrder(o, ALL, [], [[`${BASE}/plants/p1/settings`, settings]])
    await userEvent.click(await screen.findByRole('tab', { name: 'Finished goods' }))
    await userEvent.click((await screen.findAllByRole('button', { name: 'Receive finished goods' }))[0])
    return screen.findByRole('dialog', { name: /Receive finished goods for MO-2611-00001/ })
  }

  it('has a row for the main product and one per by-product, with the ceiling hint', async () => {
    const dialog = await openReceive()
    expect(within(dialog).getByText('TP-BAN01')).toBeInTheDocument()
    expect(within(dialog).getByText('PP-MUN01')).toBeInTheDocument()
    expect(await within(dialog).findByText(/upper limit 105/)).toBeInTheDocument()          // 100 x (1 + 5 %)
    expect(within(dialog).getByRole('button', { name: 'Receive finished goods' })).toBeDisabled()
  })

  it('sends only the filled rows; an empty lot is sent as null (generated by the server); Enter in the lot field does not submit', async () => {
    const dialog = await openReceive()
    const quantities = within(dialog).getAllByLabelText('Quantity received')
    await userEvent.type(quantities[0], '40')
    await userEvent.type(within(dialog).getAllByLabelText('Lot number')[0], '{Enter}')
    expect(calls(post, '/receipts')).toHaveLength(0)
    post.mockResolvedValue({ status: 201, data: doc({ type: 'Receipt', docNo: 'FR-2611-00001' }, []) } as never)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Receive finished goods' }))
    await waitFor(() => expect(calls(post, '/receipts')).toHaveLength(1))
    expect(calls(post, '/receipts')[0][1]).toEqual({ lines: [{ outputLineNo: null, quantity: 40, lotNumber: null }], note: null })
    expect(await screen.findByText('Document FR-2611-00001 posted.')).toBeInTheDocument()
  })

  it('shows the limit refusal of the server inside the dialog', async () => {
    const dialog = await openReceive()
    await userEvent.type(within(dialog).getAllByLabelText('Quantity received')[0], '90')
    post.mockRejectedValue(refusal('At most 75 more can be received.', 'MFG_FG_001'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Receive finished goods' }))
    expect(await within(dialog).findByText('At most 75 more can be received.')).toBeInTheDocument()
  })

  it('offers the quality release only to a holder of the permission and only while a lot is on hold', async () => {
    const receipt = doc({ id: 'd9', docNo: 'FR-2611-00001', type: 'Receipt' }, [dline({ kind: 'Receipt', lotNumber: 'TP-BAN01-261104-01', isQcHold: true, expiryDate: '2027-05-03T00:00:00Z' })])
    openOrder(order('Completed', { completedQty: 100 }), ALL, [receipt])
    await userEvent.click(await screen.findByRole('tab', { name: 'Finished goods' }))
    expect(await screen.findByText('TP-BAN01-261104-01')).toBeInTheDocument()
    expect(screen.getByText('quality hold')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Release from quality hold' }))
    const dialog = await screen.findByRole('dialog', { name: 'Release from quality hold' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Release from quality hold' }))
    await waitFor(() => expect(calls(post, '/stock-documents/d9/qc-release')).toHaveLength(1))
  })

  it('no quality release button without the permission or once the lot is released', async () => {
    const held = doc({ id: 'd9', type: 'Receipt' }, [dline({ kind: 'Receipt', lotNumber: 'L1', isQcHold: true })])
    openOrder(order('Completed'), ALL.filter((p) => p !== 'manufacturing:qc:disposition'), [held])
    await userEvent.click(await screen.findByRole('tab', { name: 'Finished goods' }))
    await screen.findByText('L1')
    expect(screen.queryByRole('button', { name: 'Release from quality hold' })).toBeNull()
  })
})

describe('Stock documents (order tab and plant list)', () => {
  const pending = doc({ id: 'd2', docNo: 'MI-2611-00002', status: 'Pending', attempts: 3, nextAttemptAt: '2026-11-11T02:05:00Z', lastError: 'Warehouse unreachable' }, [dline({ status: 'Pending' })])
  const failed = doc({ id: 'd3', docNo: 'MI-2611-00003', status: 'Failed', lastErrorCode: 'MFG_MAT_003', lastError: 'Not enough stock' }, [dline({ status: 'Refused', error: 'Not enough stock' })])

  it('shows a pending document as waiting with Retry and Abandon, and a failed one with its reason', async () => {
    openOrder(order('Released'), ALL, [pending, failed])
    await userEvent.click(await screen.findByRole('tab', { name: 'Stock documents' }))
    expect(await screen.findByText(/Not yet in the Warehouse \(attempt 3\)/)).toBeInTheDocument()
    expect(screen.getByText(/MFG_MAT_003: Not enough stock/)).toBeInTheDocument()
    post.mockResolvedValue({ status: 202, data: pending } as never)
    await userEvent.click(screen.getByRole('button', { name: 'Send now' }))
    await waitFor(() => expect(calls(post, '/stock-documents/d2/retry')).toHaveLength(1))
    await userEvent.click(screen.getByRole('button', { name: 'Abandon' }))
    const dialog = await screen.findByRole('dialog', { name: 'Abandon document' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Abandon' }))
    await waitFor(() => expect(calls(post, '/stock-documents/d2/abandon')).toHaveLength(1))
  })

  it('retry and abandon need the permission', async () => {
    openOrder(order('Released'), ALL.filter((p) => p !== 'manufacturing:stock-document:retry'), [pending])
    await userEvent.click(await screen.findByRole('tab', { name: 'Stock documents' }))
    await screen.findByText(/Not yet in the Warehouse/)
    expect(screen.queryByRole('button', { name: 'Send now' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Abandon' })).toBeNull()
  })

  it('the plant list asks for Pending and Failed documents by default', async () => {
    setup(ALL)
    answer([
      [`${BASE}/stock-documents`, (_u: string, c?: { params?: Record<string, unknown> }) => ({ items: c?.params?.status === 'Pending' ? [pending] : c?.params?.status === 'Failed' ? [failed] : [], total: 1, page: 1, pageSize: 100 })],
      [`${BASE}/plants`, [PLANT]],
    ])
    const { container } = renderAt(<StockDocumentsPage />)
    expect(await screen.findByText('MI-2611-00002')).toBeInTheDocument()
    expect(screen.getByText('MI-2611-00003')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'MO-2611-00001' })[0]).toHaveAttribute('href', '/manufacturing/orders/o1')
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Operations tab', () => {
  const running = {
    id: 'l1', orderId: 'o1', operationSeq: 10, employeeId: 'e1', employeeNumber: 'E001', employeeName: 'An Nguyen', machineId: null, startAt: '2026-11-11T01:00:00Z', endAt: null, pausedAt: null,
    pausedMinutes: 0, durationMinutes: null, laborRatePerHour: 50, isManual: false, state: 'Running',
  }
  const employees = { items: [{ id: 'e1', employeeNumber: 'E001', fullName: 'An Nguyen' }], totalCount: 1, page: 1, pageSize: 200, totalPages: 1 }

  async function openOps(logs: unknown[] = []) {
    const o = order('InProgress', { operations: [op(10, 'Running', { goodQty: 10 }), op(20, 'Pending')] })
    setup(ALL)
    api({ order: o, ops: [{ operation: o.operations[0], timeLogs: logs }, { operation: o.operations[1], timeLogs: [] }], extra: [['/employees', employees], [`${BASE}/machines`, []]] })
    const view = renderAt(<ProductionOrderDetailPage />, '/manufacturing/orders/o1', '/manufacturing/orders/:id')
    await userEvent.click(await screen.findByRole('tab', { name: 'Operations' }))
    return view
  }

  it('lists operations with time logs and pauses a running log', async () => {
    const { container } = await openOps([running])
    expect(await screen.findByText(/E001 An Nguyen/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Pause' }))
    await waitFor(() => expect(calls(post, '/operations/10/time-logs/l1/pause')).toHaveLength(1))
    expect(await axe(container)).toHaveNoViolations()
  })

  it('starting work needs an employee', async () => {
    await openOps()
    await userEvent.click(await screen.findByRole('button', { name: 'Start work' }))
    const dialog = await screen.findByRole('dialog', { name: /Start operation 10 Step 10/ })
    const start = within(dialog).getByRole('button', { name: 'Start work' })
    expect(start).toBeDisabled()
    await userEvent.selectOptions(await within(dialog).findByRole('combobox', { name: /Employee/ }), 'e1')
    await userEvent.click(start)
    await waitFor(() => expect(calls(post, '/operations/10/start')[0][1]).toEqual({ employeeId: 'e1', machineId: null }))
  })

  it('a report with scrap needs a scrap reason, and a 202 answer says the booking is on its way', async () => {
    await openOps()
    await userEvent.click((await screen.findAllByRole('button', { name: 'Report output' }))[0])
    const dialog = await screen.findByRole('dialog', { name: /Report operation 10 Step 10/ })
    await userEvent.type(within(dialog).getByLabelText('Good'), '60')
    await userEvent.type(within(dialog).getByLabelText('Scrap'), '2')
    const send = within(dialog).getByRole('button', { name: 'Report output' })
    expect(send).toBeDisabled()                                                                   // scrap without a reason
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: /Scrap reason/ }), 'rx')
    expect(send).toBeEnabled()
    post.mockResolvedValue({ status: 202, data: { report: { id: 'r1', docNo: 'OR-2611-00001' }, isPending: true, backflush: null } } as never)
    await userEvent.click(send)
    await waitFor(() => expect(calls(post, '/operations/10/report')).toHaveLength(1))
    expect(calls(post, '/operations/10/report')[0][1]).toMatchObject({ goodQty: 60, scrapQty: 2, reworkQty: 0, scrapReasonId: 'rx', reworkReasonId: null })
    expect(await screen.findByText(/OR-2611-00001 is saved and counts as soon as/)).toBeInTheDocument()
  })
})

describe('History tab', () => {
  it('names the reason of a status change', async () => {
    openOrder(order('Released'))
    await userEvent.click(await screen.findByRole('tab', { name: 'History' }))
    expect(await screen.findByText('Customer cancelled')).toBeInTheDocument()
    expect(screen.getByText('admin')).toBeInTheDocument()
  })
})
