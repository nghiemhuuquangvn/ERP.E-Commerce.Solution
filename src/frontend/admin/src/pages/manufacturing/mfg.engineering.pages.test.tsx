import { describe, it, expect, vi, beforeEach } from 'vitest'
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'

vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { BomEditorPage, BomListPage, WhereUsedPage } from './BomPages'
import { RoutingEditorPage, RoutingListPage, compareOperations } from './RoutingPages'
import { CsvImportDialog } from '@/components/manufacturing/shared'
import { reorderSeqs } from '@/components/manufacturing/reorder'
import { BASE, PLANT, answer, get, post, refusal, renderAt, setup } from './testKit'

const UNITS = { items: [{ id: 'u1', code: 'PCS' }, { id: 'u2', code: 'L' }], totalCount: 2 }
const line = (no: number, code: string, extra: Record<string, unknown> = {}) => ({
  lineNo: no, lineType: 'Component', componentItemId: `i${no}`, componentCode: code, componentName: `Name ${code}`, quantity: 1, uomId: 'u1', scrapPct: 0, isFixedQuantity: false, isPhantom: false,
  issueMethod: 'Manual', operationSeq: 10, alternativeGroup: null, alternativePriority: null, costAllocationPct: null, notes: null, ...extra,
})
const bom = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'b1', plantId: 'p1', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, baseQuantity: 1, yieldPct: 100, status, effectiveFrom: '2026-01-01', effectiveTo: null, description: null,
  approvedBy: status === 'Draft' ? null : 'admin', approvedAt: status === 'Draft' ? null : '2026-11-07T09:00:00Z', warnings: [],
  lines: [line(10, 'BTP-MAT01', { quantity: 1 }), line(20, 'NVL-VIT01', { quantity: 16, issueMethod: 'Backflush' }), line(60, 'KIT-PKG01', { isPhantom: true, quantity: 1, uomId: 'u1' })], ...extra,
})
const node = (level: number, code: string, qty: number, extra: Record<string, unknown> = {}, children: unknown[] = []) => ({
  level, itemId: code, itemCode: code, itemName: `Name ${code}`, quantity: qty, isPhantom: false, isSubstitute: false, alternativeGroup: null, operationSeq: null, bomVersion: null, children, ...extra,
})
const TREE = node(0, 'TP-BAN01', 1, { bomVersion: 1 }, [
  node(1, 'BTP-MAT01', 1, {}, [node(2, 'NVL-GO01', 0.022)]),
  node(1, 'KIT-PKG01', 1, { isPhantom: true }, [node(2, 'NVL-THUNG01', 1), node(2, 'NVL-XOP01', 2)]),
  node(1, 'NVL-KEO01', 0.1, { alternativeGroup: 'G1' }),
  node(1, 'NVL-KEO02', 0.12, { alternativeGroup: 'G1', isSubstitute: true }),
])

function bomApi(status = 'Approved', extra: [string, unknown][] = []) {
  answer([
    ...extra,
    [`${BASE}/boms/indented`, TREE],
    [`${BASE}/boms/explode`, { bomId: 'b1', itemId: 'top', version: 1, orderQuantity: 100, requirements: [{ componentItemId: 'i1', componentCode: 'NVL-SON01', componentName: 'Paint', quantity: 52.5, operationSeq: 20, issueMethod: 'Manual', sourceLineNo: 40 }], outputs: [] }],
    [`${BASE}/boms/b1`, bom(status)],
    [`${BASE}/boms/b2`, { ...bom('Draft'), id: 'b2', version: 2 }],
    [`${BASE}/boms`, [{ id: 'b1', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status, effectiveFrom: '2026-01-01', effectiveTo: null, lineCount: 3 }]],
    ['/units-of-measure', UNITS],
    [`${BASE}/plants`, [PLANT]],
  ])
}
const openBom = (status = 'Approved') => { bomApi(status); return renderAt(<BomEditorPage />, '/manufacturing/boms/b1', '/manufacturing/boms/:id') }

beforeEach(() => setup())

describe('BOM list', () => {
  it('lists versions with their status and validity', async () => {
    bomApi()
    const { container } = renderAt(<BomListPage />)
    expect(await screen.findByRole('link', { name: 'TP-BAN01' })).toBeInTheDocument()
    expect(screen.getByText('v1')).toBeInTheDocument()
    expect(screen.getByText('2026-01-01 → …')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New BOM' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('BOM editor', () => {
  it('an approved version cannot be edited: copy and obsolete are offered, add line and approve are not', async () => {
    const { container } = openBom('Approved')
    expect(await screen.findByText('BTP-MAT01')).toBeInTheDocument()
    expect(screen.getByText('Phantom')).toBeInTheDocument()
    expect(screen.getByText(/can no longer|cannot be edited/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Copy as new version' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Make obsolete' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add line' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('a draft shows Add line and Approve to a user who may approve, and hides Approve from one who may not', async () => {
    openBom('Draft')
    expect(await screen.findByRole('button', { name: 'Add line' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument()
  })

  it('hides Approve without the approve permission', async () => {
    setup(['manufacturing:bom:view', 'manufacturing:bom:manage', 'manufacturing:masterdata:view'])
    openBom('Draft')
    await screen.findByRole('button', { name: 'Add line' })
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull()
  })

  it('asks first, approves, and shows the routing warnings the server returns', async () => {
    openBom('Draft')
    post.mockResolvedValueOnce({ data: { ...bom('Approved'), warnings: ['Operation 40 (BOM line 40: NVL-SON01) is not in routing version 1.'] } })
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('dialog')
    expect(post).not.toHaveBeenCalled()
    await userEvent.click(within(dialog).getByRole('button', { name: /Approve|Confirm/ }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/boms/b1/approve`, undefined))
    expect(await screen.findByText('Approved, with warnings')).toBeInTheDocument()
    expect(screen.getByText(/Operation 40 \(BOM line 40/)).toBeInTheDocument()
  })

  it('shows the cycle path when the approval is refused', async () => {
    openBom('Draft')
    post.mockRejectedValueOnce(refusal('BOM cycle detected: BTP-MAT01 > TP-BAN01 > BTP-MAT01.', 'MFG_BOM_004'))
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: /Approve|Confirm/ }))
    expect(await screen.findByText('BOM cycle detected: BTP-MAT01 > TP-BAN01 > BTP-MAT01.')).toBeInTheDocument()
  })

  it('copy makes the next version and opens it', async () => {
    openBom('Approved')
    post.mockResolvedValueOnce({ data: { ...bom('Draft'), id: 'b2', version: 2 } })
    await userEvent.click(await screen.findByRole('button', { name: 'Copy as new version' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: /Copy|Confirm/ }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/boms/b1/copy`, { effectiveFrom: undefined }))
  })

  it('the tree shows phantoms open, substitutes marked, and collapses a branch', async () => {
    const { container } = openBom('Approved')
    await userEvent.click(await screen.findByRole('tab', { name: 'Tree' }))
    const tree = await screen.findByRole('tree', { name: 'Tree' })
    expect(within(tree).getByText('NVL-THUNG01')).toBeInTheDocument()   // inside the phantom
    expect(within(tree).getByText('NVL-XOP01')).toBeInTheDocument()
    expect(within(tree).getAllByText('Phantom').length).toBe(1)
    expect(within(tree).getByText(/G1 · substitute/)).toBeInTheDocument()
    expect(within(tree).getByText('0.022')).toBeInTheDocument()
    await userEvent.click(within(tree).getByRole('button', { name: 'Collapse KIT-PKG01' }))
    expect(within(tree).queryByText('NVL-THUNG01')).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Collapse all' }))
    expect(within(tree).queryByText('BTP-MAT01')).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Expand all' }))
    expect(within(tree).getByText('BTP-MAT01')).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('the requirements tab shows the materials of an order', async () => {
    openBom('Approved')
    await userEvent.click(await screen.findByRole('tab', { name: 'Requirements' }))
    expect(await screen.findByText('NVL-SON01')).toBeInTheDocument()
    expect(screen.getByText('52.5')).toBeInTheDocument()
  })

  it('compares two versions: one changed quantity and one added line', async () => {
    bomApi('Approved', [[`${BASE}/boms/compare`, [
      { kind: 'Changed', componentItemId: 'i2', componentCode: 'NVL-VIT01', operationSeq: 10, leftQuantity: 16, rightQuantity: 18, detail: 'quantity 16 -> 18' },
      { kind: 'Added', componentItemId: 'i9', componentCode: 'NVL-KEO03', operationSeq: 10, leftQuantity: null, rightQuantity: 0.05, detail: null },
    ]], [`${BASE}/boms?`, [
      { id: 'b1', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status: 'Approved', effectiveFrom: '2026-01-01', effectiveTo: '2026-11-30', lineCount: 3 },
      { id: 'b2', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 2, status: 'Draft', effectiveFrom: '2026-12-01', effectiveTo: null, lineCount: 4 },
    ]]])
    get.mockImplementation(((url: string) => Promise.resolve({
      data: url.includes('/boms/compare') ? [
        { kind: 'Changed', componentItemId: 'i2', componentCode: 'NVL-VIT01', operationSeq: 10, leftQuantity: 16, rightQuantity: 18, detail: 'quantity 16 -> 18' },
        { kind: 'Added', componentItemId: 'i9', componentCode: 'NVL-KEO03', operationSeq: 10, leftQuantity: null, rightQuantity: 0.05, detail: null },
      ] : url.endsWith('/boms/b1') ? bom('Approved') : url.includes('/units-of-measure') ? UNITS : url.includes('/plants') ? [PLANT] : [
        { id: 'b1', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status: 'Approved', effectiveFrom: '2026-01-01', effectiveTo: '2026-11-30', lineCount: 3 },
        { id: 'b2', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 2, status: 'Draft', effectiveFrom: '2026-12-01', effectiveTo: null, lineCount: 4 },
      ],
    })) as never)
    renderAt(<BomEditorPage />, '/manufacturing/boms/b1', '/manufacturing/boms/:id')
    await userEvent.click(await screen.findByRole('tab', { name: 'Versions' }))
    expect(await screen.findByText('2 difference(s) between v1 and v2')).toBeInTheDocument()
    expect(screen.getByText('Changed')).toBeInTheDocument()
    expect(screen.getByText('Added')).toBeInTheDocument()
    expect(screen.getByText('NVL-KEO03')).toBeInTheDocument()
    expect(screen.getByText('quantity 16 -> 18')).toBeInTheDocument()
  })
})

describe('Where used', () => {
  it('lists the parents by level and links to their BOM', async () => {
    answer([
      [`${BASE}/boms/where-used`, [
        { level: 1, parentItemId: 'mat', parentCode: 'BTP-MAT01', parentName: 'Top', bomVersion: 1, componentItemId: 'go', quantityPer: 0.02, uomId: 'u1' },
        { level: 2, parentItemId: 'top', parentCode: 'TP-BAN01', parentName: 'Table', bomVersion: 1, componentItemId: 'mat', quantityPer: 1, uomId: 'u1' },
      ]],
      [`${BASE}/boms`, [{ id: 'b9', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status: 'Approved', effectiveFrom: '2026-01-01', effectiveTo: null, lineCount: 3 }]],
      [`${BASE}/plants`, [PLANT]],
    ])
    renderAt(<WhereUsedPage />, '/manufacturing/where-used?itemId=go', '/manufacturing/where-used')
    expect(await screen.findByText('BTP-MAT01')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'TP-BAN01' })).toHaveAttribute('href', '/manufacturing/boms/b9')
    expect(screen.getByText('0.02')).toBeInTheDocument()
  })
})

// ── Routing ───────────────────────────────────────────────────────────────────────────────────────────────
const op = (seq: number, name: string, wc: string, setup: number, run: number) => ({
  operationSeq: seq, standardOperationId: null, name, workCenterId: wc, workCenterCode: wc, setupTimeMinutes: setup, runTimeMinutes: run, runTimeBasis: 'PerUnit', batchSize: null,
  queueTimeMinutes: 0, moveTimeMinutes: 0, isSubcontract: false, instructions: null,
})
const routing = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'r1', plantId: 'p1', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status, effectiveFrom: '2026-01-01', effectiveTo: null, description: null,
  approvedBy: status === 'Draft' ? null : 'admin', approvedAt: status === 'Draft' ? null : '2026-11-07T09:00:00Z', warnings: [],
  operations: [op(10, 'Assembly', 'WC-ASM', 15, 10), op(20, 'Painting', 'WC-PNT', 30, 6), op(30, 'Packing', 'WC-PKG', 0, 2)], ...extra,
})
const LEAD = {
  totalMinutes: 2003, startDate: '2026-11-11', startTime: '2026-11-11T08:03:00', start: { date: '2026-11-11', workingMinute: 197 },
  operations: [
    { operationSeq: 10, name: 'Assembly', workCenterCode: 'WC-ASM', standardMinutes: 1015, calendarMinutes: 1015, queueMinutes: 0, moveMinutes: 0, start: { date: '2026-11-11', workingMinute: 697 }, end: { date: '2026-11-12', workingMinute: 812 }, startTime: '2026-11-11T08:03:00', endTime: '2026-11-12T19:12:00' },
    { operationSeq: 20, name: 'Painting', workCenterCode: 'WC-PNT', standardMinutes: 630, calendarMinutes: 788, queueMinutes: 0, moveMinutes: 0, start: { date: '2026-11-12', workingMinute: 812 }, end: { date: '2026-11-13', workingMinute: 700 }, startTime: '2026-11-12T19:12:00', endTime: '2026-11-13T17:40:00' },
    { operationSeq: 30, name: 'Packing', workCenterCode: 'WC-PKG', standardMinutes: 200, calendarMinutes: 200, queueMinutes: 0, moveMinutes: 0, start: { date: '2026-11-13', workingMinute: 700 }, end: { date: '2026-11-13', workingMinute: 900 }, startTime: '2026-11-13T17:40:00', endTime: '2026-11-13T21:30:00' },
  ],
}
function routingApi(status = 'Draft') {
  answer([
    [`${BASE}/routings/r1/lead-time`, LEAD],
    [`${BASE}/routings/r1`, routing(status)],
    [`${BASE}/routings`, [{ id: 'r1', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status, effectiveFrom: '2026-01-01', effectiveTo: null, operationCount: 3 }]],
    [`${BASE}/work-centers`, []], [`${BASE}/standard-operations`, []],
    [`${BASE}/plants`, [PLANT]],
  ])
}
const openRouting = (status = 'Draft') => { routingApi(status); return renderAt(<RoutingEditorPage />, '/manufacturing/routings/r1', '/manufacturing/routings/:id') }

describe('Routing list', () => {
  it('lists routings with their operation count', async () => {
    routingApi('Approved')
    const { container } = renderAt(<RoutingListPage />)
    expect(await screen.findByRole('link', { name: 'TP-BAN01' })).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })
})

describe('Routing editor', () => {
  it('dropping operation 30 on the first row renumbers the operations in the new order', async () => {
    openRouting('Draft')
    post.mockResolvedValueOnce({ data: routing('Draft', { operations: [op(10, 'Packing', 'WC-PKG', 0, 2), op(20, 'Assembly', 'WC-ASM', 15, 10), op(30, 'Painting', 'WC-PNT', 30, 6)] }) })
    const last = await screen.findByTestId('op-row-30')
    const first = screen.getByTestId('op-row-10')
    expect(last).toHaveAttribute('draggable', 'true')
    fireEvent.dragStart(last, { dataTransfer: {} })
    fireEvent.dragOver(first, { dataTransfer: {} })
    fireEvent.drop(first, { dataTransfer: {} })
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/routings/r1/renumber`, { orderedSeqs: [30, 10, 20] }))
    expect(await screen.findByText('Operations renumbered')).toBeInTheDocument()
  })

  it('the arrows do the same without dragging, and the ends cannot move out', async () => {
    openRouting('Draft')
    post.mockResolvedValueOnce({ data: routing('Draft') })
    await screen.findByTestId('op-row-10')
    expect(screen.getByRole('button', { name: 'Move operation 10 up' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Move operation 30 down' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Move operation 20 down' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/routings/r1/renumber`, { orderedSeqs: [10, 30, 20] }))
  })

  it('an approved routing cannot be dragged or edited', async () => {
    const { container } = openRouting('Approved')
    const row = await screen.findByTestId('op-row-10')
    expect(row).toHaveAttribute('draggable', 'false')
    expect(screen.queryByRole('button', { name: /Move operation/ })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Add operation' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Copy as new version' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('shows the server refusal of a renumbering', async () => {
    openRouting('Draft')
    post.mockRejectedValueOnce(refusal('The new order must list every operation of the routing exactly once.'))
    await userEvent.click(await screen.findByRole('button', { name: 'Move operation 20 down' }))
    expect(await screen.findByText('The new order must list every operation of the routing exactly once.')).toBeInTheDocument()
  })

  it('the lead time tab shows 2003 minutes starting on 2026-11-11 with the minutes of each operation', async () => {
    openRouting('Approved')
    await userEvent.click(await screen.findByRole('tab', { name: 'Lead time' }))
    expect(await screen.findByText('2,003')).toBeInTheDocument()
    expect(screen.getByText('2026-11-11')).toBeInTheDocument()
    expect(screen.getByText('788')).toBeInTheDocument()   // painting: 630 standard at 80 % efficiency
    expect(screen.getByText('630')).toBeInTheDocument()
    expect(get.mock.calls.some(([url, cfg]) => String(url).endsWith('/routings/r1/lead-time') && (cfg as { params: { quantity: number } }).params.quantity === 100)).toBe(true)
  })

  it('approving shows the BOM lines the routing does not cover', async () => {
    openRouting('Draft')
    post.mockResolvedValueOnce({ data: { ...routing('Approved'), warnings: ['BOM version 1: Operation 40 is not in routing version 1.'] } })
    await userEvent.click(await screen.findByRole('button', { name: 'Approve' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: /Approve|Confirm/ }))
    expect(await screen.findByText(/some BOM lines name an operation this routing does not have/)).toBeInTheDocument()
    expect(screen.getByText(/BOM version 1: Operation 40/)).toBeInTheDocument()
  })
})

describe('Order helpers', () => {
  it('moves one operation and leaves the others in order', () => {
    expect(reorderSeqs([10, 20, 30], 2, 0)).toEqual([30, 10, 20])
    expect(reorderSeqs([10, 20, 30], 0, 1)).toEqual([20, 10, 30])
    expect(reorderSeqs([10, 20, 30], 1, 1)).toBeNull()
    expect(reorderSeqs([10, 20, 30], 0, 5)).toBeNull()
  })

  it('compares two versions of a routing by operation number', () => {
    const left = [op(10, 'Assembly', 'WC-ASM', 15, 10), op(20, 'Painting', 'WC-PNT', 30, 6), op(30, 'Packing', 'WC-PKG', 0, 2)]
    const right = [op(10, 'Assembly', 'WC-ASM', 15, 12), op(20, 'Painting', 'WC-PNT', 30, 6), op(40, 'Inspection', 'WC-PKG', 0, 1)]
    expect(compareOperations(left as never, right as never)).toEqual([
      { kind: 'Changed', seq: 10, name: 'Assembly', detail: 'run 10 → 12' },
      { kind: 'Removed', seq: 30, name: 'Packing', detail: '' },
      { kind: 'Added', seq: 40, name: 'Inspection', detail: '' },
    ])
  })
})

// ── CSV import dialog ─────────────────────────────────────────────────────────────────────────────────────
const csvFile = () => Object.assign(new File(['a,b'], 'bom.csv', { type: 'text/csv' }), { text: async () => 'a,b' })

describe('CSV import dialog', () => {
  const open = (onImported = vi.fn()) => renderAt(<CsvImportDialog open kind="bom" plantId="p1" itemId="top" onClose={() => {}} onImported={onImported} />)

  it('lists every problem with its line and does not allow the import while there is one', async () => {
    post.mockResolvedValueOnce({ data: { dryRun: true, rowCount: 10, createdId: null, version: null, errors: [{ line: 5, key: 'NO-SUCH', message: 'No item with SKU NO-SUCH.' }, { line: 9, key: 'NVL-SON01', message: 'Unit mismatch.' }] } })
    open()
    await userEvent.upload(screen.getByLabelText('CSV file'), csvFile())
    expect(screen.getByRole('button', { name: 'Import' })).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Check only' }))
    expect((await screen.findAllByText('2 problem(s) found')).length).toBeGreaterThan(0)
    expect(screen.getByText('No item with SKU NO-SUCH.')).toBeInTheDocument()
    const rows = within(screen.getByRole('table')).getAllByRole('row')
    expect(within(rows[1]).getByText('5')).toBeInTheDocument()
    expect(within(rows[2]).getByText('9')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Import' })).toBeDisabled()
    expect(post).toHaveBeenCalledWith(`${BASE}/boms/import`, 'a,b', expect.objectContaining({ params: expect.objectContaining({ plantId: 'p1', itemId: 'top', dryRun: true }) }))
  })

  it('a clean check enables the import, which creates the draft and reports it', async () => {
    const onImported = vi.fn()
    post.mockResolvedValueOnce({ data: { dryRun: true, rowCount: 3, createdId: null, version: null, errors: [] } })
    post.mockResolvedValueOnce({ data: { dryRun: false, rowCount: 3, createdId: 'new-bom', version: 2, errors: [] } })
    open(onImported)
    await userEvent.upload(screen.getByLabelText('CSV file'), csvFile())
    await userEvent.click(screen.getByRole('button', { name: 'Check only' }))
    expect(await screen.findByText(/The file is clean \(3 lines\)/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Import' }))
    await waitFor(() => expect(onImported).toHaveBeenCalledWith('new-bom'))
    expect(post).toHaveBeenLastCalledWith(`${BASE}/boms/import`, 'a,b', expect.objectContaining({ params: expect.objectContaining({ dryRun: false }) }))
  })

  it('shows the server message of a wrong header', async () => {
    post.mockRejectedValueOnce(refusal('The header lacks the required column(s): uom.'))
    open()
    await userEvent.upload(screen.getByLabelText('CSV file'), csvFile())
    await userEvent.click(screen.getByRole('button', { name: 'Check only' }))
    expect(await screen.findByText('The header lacks the required column(s): uom.')).toBeInTheDocument()
  })
})
