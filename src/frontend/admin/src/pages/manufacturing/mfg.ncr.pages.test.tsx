import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'

vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { NcrListPage, NcrPage } from './NcrPages'
import { QualityTab } from '@/components/manufacturing/OrderTabs'
import { RaiseNcrDialog } from '@/components/manufacturing/RaiseNcrDialog'
import { IssueReturnDialog } from '@/components/manufacturing/IssueReturnDialog'
import { BASE, PLANT, answer, get, post, put, refusal, renderAt, setup } from './testKit'

const QC = ['manufacturing:qc:view', 'manufacturing:qc:disposition', 'manufacturing:qc:approve-deviation', 'manufacturing:qc:inspect']
const ncr = (extra: Record<string, unknown> = {}) => ({
  id: 'n1', ncrNo: 'NCR-2611-00001', plantId: 'p1', sourceType: 'Inspection', inspectionId: 'i1', operationReportId: null, orderId: 'o1', orderNo: 'MO-2611-00001', operationSeq: null, itemId: 'top',
  itemCode: 'TP-BAN01', itemName: 'Table', lotId: 'l1', lotNumber: 'TP-BAN01-261111-01', nonconformingQty: 100, description: 'Inspection QI-2611-00001 failed', status: 'Open', isInProcess: false,
  isRecordOnly: false, createdAt: '2026-11-11T02:00:00Z', createdBy: 'qa', closedAt: null, ...extra,
})
const line = (lineNo: number, type: string, qty: number, extra: Record<string, unknown> = {}) => ({
  lineNo, type, qty, reasonCodeId: type === 'Scrap' ? 'sc1' : null, note: null, approvedBy: type === 'UseAsIs' ? 'qm' : null, approvedAt: null, reworkOrderId: null, reworkOrderNo: null, reworkOrderStatus: null,
  movementKey: null, resultLotId: null, resultLotNumber: null, isExecuted: false, isStarted: false, executedAt: null, executedBy: null, ...extra,
})
const doc = (n: Record<string, unknown> = {}, lines: unknown[] = []) => ({ ncr: ncr(n), dispositions: lines })
const REASONS = [{ id: 'sc1', type: 'Scrap', code: 'SCRAP-01', name: 'Material fault', isActive: true }, { id: 'rw1', type: 'Rework', code: 'RWK-01', name: 'Repaint', isActive: true }]

let current = doc()
function ncrApi(d = doc()) {
  current = d
  answer([
    [`${BASE}/ncrs/n1`, () => current],
    [`${BASE}/ncrs`, { items: [ncr(), ncr({ id: 'n2', ncrNo: 'NCR-2611-00002', status: 'InDisposition' })], total: 2, page: 1, pageSize: 25 }],
    [`${BASE}/reason-codes`, REASONS],
    [`${BASE}/plants`, [PLANT]],
  ])
}
const openNcr = (d = doc()) => {
  ncrApi(d)
  return renderAt(<NcrPage />, '/manufacturing/ncrs/n1', '/manufacturing/ncrs/:id')
}

describe('Non-conformance reports (S23, P4-4)', () => {
  beforeEach(() => setup(QC))

  it('the worklist shows the open reports by default with their status', async () => {
    ncrApi()
    renderAt(<NcrListPage />)

    expect((await screen.findAllByRole('link', { name: 'NCR-2611-00001' }))[0]).toHaveAttribute('href', '/manufacturing/ncrs/n1')
    expect(screen.getAllByText('In disposition').length).toBeGreaterThan(0)
    await waitFor(() => expect(get).toHaveBeenCalledWith(`${BASE}/ncrs`, { params: expect.objectContaining({ plantId: 'p1', openOnly: true }) }))
  })

  it('shows what is missing while the lines do not add up and saves the lines once they do', async () => {
    openNcr()
    await screen.findByText('NCR-2611-00001')
    const add = screen.getByRole('button', { name: /Add a line/ })

    await userEvent.click(add)             // the first line starts with the whole quantity
    await userEvent.clear(screen.getByLabelText('Quantity'))
    await userEvent.type(screen.getByLabelText('Quantity'), '60')
    expect(screen.getByText('Entered 60 of 100: 40 still to assign.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()

    await userEvent.click(add)             // the new line is filled with the missing 40
    expect(await screen.findByText('The lines add up to 100.')).toBeInTheDocument()
    const types = screen.getAllByLabelText('Action')
    await userEvent.selectOptions(types[0], 'Scrap')
    await userEvent.selectOptions(screen.getByLabelText(/Scrap reason/), 'sc1')
    put.mockResolvedValueOnce({ data: doc({ status: 'InDisposition' }, [line(1, 'Scrap', 60), line(2, 'Rework', 40)]) })
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(put).toHaveBeenCalledWith(`${BASE}/ncrs/n1/dispositions`, { lines: [{ type: 'Scrap', qty: 60, reasonCodeId: 'sc1', note: null }, { type: 'Rework', qty: 40, reasonCodeId: null, note: null }] }))
  })

  it('a Use as is line cannot be saved by a user without the deviation approval right', async () => {
    setup(['manufacturing:qc:view', 'manufacturing:qc:disposition'])
    openNcr()
    await screen.findByText('NCR-2611-00001')

    await userEvent.click(screen.getByRole('button', { name: /Add a line/ }))
    await userEvent.selectOptions(screen.getByLabelText('Action'), 'UseAsIs')
    await userEvent.type(screen.getByLabelText('Quantity'), '100')

    expect(screen.getByText(/needs the right to approve deviations/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('a saved line is executed after a confirm that says what it does and a rework line then links to its order', async () => {
    openNcr(doc({ status: 'InDisposition' }, [line(1, 'Rework', 10), line(2, 'Scrap', 5), line(3, 'UseAsIs', 85)]))
    await screen.findByText('NCR-2611-00001')
    const buttons = screen.getAllByRole('button', { name: 'Execute' })
    expect(buttons).toHaveLength(3)

    const after = doc({ status: 'InDisposition' }, [line(1, 'Rework', 10, { reworkOrderId: 'r1', reworkOrderNo: 'MO-2611-00009', isStarted: true }), line(2, 'Scrap', 5), line(3, 'UseAsIs', 85)])
    post.mockImplementationOnce((() => { current = after; return Promise.resolve({ data: { ncr: after, reworkOrderId: 'r1', reworkOrderNo: 'MO-2611-00009' } }) }) as never)
    await userEvent.click(buttons[0])
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/Create a rework order for 10 of lot TP-BAN01-261111-01/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Execute' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/ncrs/n1/dispositions/1/execute`, {}))
    expect(await screen.findByRole('link', { name: 'Rework order MO-2611-00009' })).toHaveAttribute('href', '/manufacturing/orders/r1')
    expect(screen.getByText(/lines can no longer be replaced/)).toBeInTheDocument()
  })

  it('the scrap confirm warns that it cannot be undone and a refusal of the Warehouse stays on the page', async () => {
    openNcr(doc({ status: 'InDisposition' }, [line(1, 'Scrap', 100)]))
    await screen.findByText('NCR-2611-00001')
    post.mockRejectedValueOnce(refusal('Không lấy được dữ liệu từ dịch vụ Warehouse.', 'MFG_INT_001'))

    await userEvent.click(screen.getByRole('button', { name: 'Execute' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/cannot be undone here/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Execute' }))

    expect(await screen.findByText(/Không lấy được dữ liệu/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Execute' })).toBeInTheDocument()
  })

  it('output held by an in-process inspection offers only Scrap and Use as is', async () => {
    openNcr(doc({ sourceType: 'Inspection', isInProcess: true, lotId: null, lotNumber: null, operationSeq: 10, nonconformingQty: 40 }))
    await screen.findByText('NCR-2611-00001')

    expect(screen.getByText(/work in process/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Add a line/ }))
    const options = within(screen.getByLabelText('Action')).getAllByRole('option').map((o) => o.textContent)
    expect(options).toEqual(['Scrap', 'Use as is'])
  })

  it('a closed report is read only and says when it was closed', async () => {
    openNcr(doc({ status: 'Closed', closedAt: '2026-11-12T03:00:00Z' }, [line(1, 'Scrap', 100, { isExecuted: true })]))

    expect(await screen.findByText(/Every line is executed/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Execute' })).toBeNull()
    expect(screen.getByText('Executed')).toBeInTheDocument()
  })

  it('a user who can only view cannot change or execute anything', async () => {
    setup(['manufacturing:qc:view'])
    openNcr(doc({ status: 'InDisposition' }, [line(1, 'Scrap', 100)]))

    expect(await screen.findByText(/cannot decide|not decide what to do/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Execute' })).toBeNull()
    expect(screen.getByLabelText('Quantity')).toBeDisabled()
  })

  it('the order Quality tab lists the reports of the order and links the NCR of a failed inspection', () => {
    const inspection = { id: 'i1', docNo: 'QI-2611-00001', type: 'Fqc', status: 'Failed', orderId: 'o1', orderNo: 'MO', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', lotNumber: 'L1', lotQty: 100, sampleQty: 5, ncrRequired: true, ncrId: 'n1', ncrNo: 'NCR-2611-00001', reinspectedById: null }
    renderAt(<QualityTab inspections={[inspection] as never} ncrs={[ncr()] as never} />)

    const links = screen.getAllByRole('link', { name: 'NCR-2611-00001' })
    expect(links).toHaveLength(2)
    expect(links[0]).toHaveAttribute('href', '/manufacturing/ncrs/n1')
    expect(screen.getByRole('table', { name: 'Non-conformance reports of the order' })).toBeInTheDocument()
  })

  it('raises an NCR from a report: the quantity cannot pass what the report counted', async () => {
    const report = { id: 'r1', docNo: 'OR-2611-00001', operationSeq: 10, goodQty: 60, scrapQty: 2, reworkQty: 0 }
    const done = vi.fn()
    post.mockResolvedValueOnce({ data: doc({ sourceType: 'OperationReport', isRecordOnly: true, nonconformingQty: 5 }) })
    renderAt(<RaiseNcrDialog orderId="o1" report={report as never} onClose={() => undefined} onDone={done} />)

    const raise = screen.getByRole('button', { name: 'Raise NCR' })
    expect(raise).toBeDisabled()
    await userEvent.type(screen.getByLabelText(/Non-conforming quantity/), '70')
    await userEvent.type(screen.getByLabelText(/Description/), 'Scratched panels')
    expect(raise).toBeDisabled()
    await userEvent.clear(screen.getByLabelText(/Non-conforming quantity/))
    await userEvent.type(screen.getByLabelText(/Non-conforming quantity/), '5')
    await userEvent.click(raise)

    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/ncrs`, { orderId: 'o1', reportId: 'r1', quantity: 5, description: 'Scratched panels' }))
    await waitFor(() => expect(done).toHaveBeenCalled())
  })

  it('the issue dialog of a rework order has the held lot fixed and no lot suggestion', async () => {
    const order = {
      id: 'r9', orderNo: 'MO-2611-00009', status: 'Released', materials: [{
        lineNo: 10, componentItemId: 'top', componentCode: 'TP-BAN01', componentName: 'Table', requiredQty: 10, issuedQty: 0, returnedQty: 0, consumedQty: 0, reservedQty: 0, issueMethod: 'Manual',
        sourceLotId: 'l1', sourceLotNumber: 'TP-BAN01-261111-01',
      }],
    }
    post.mockResolvedValueOnce({ data: { document: { docNo: 'MI-2611-00005' }, pending: false } })
    renderAt(<IssueReturnDialog order={order as never} mode="issue" initialLineNo={10} onClose={() => undefined} onDone={() => undefined} />)

    const lot = await screen.findByLabelText('Lot')
    expect(lot).toHaveValue('TP-BAN01-261111-01')
    expect(lot).toHaveAttribute('readonly')
    expect(screen.queryByRole('button', { name: /Suggest/ })).toBeNull()
    await userEvent.click(screen.getByRole('button', { name: 'Issue' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/orders/r9/material-issues`, expect.objectContaining({ lines: [{ lineNo: 10, quantity: 10, lotId: 'l1' }] }), expect.anything()))
  })

  it('has no accessibility violations (worklist, document)', async () => {
    ncrApi()
    const list = renderAt(<NcrListPage />)
    await screen.findAllByRole('link', { name: 'NCR-2611-00001' })
    expect(await axe(list.container)).toHaveNoViolations()
    list.unmount()

    const d = openNcr(doc({ status: 'InDisposition' }, [line(1, 'Scrap', 100)]))
    await screen.findByText('NCR-2611-00001')
    expect(await axe(d.container)).toHaveNoViolations()
  })
})
