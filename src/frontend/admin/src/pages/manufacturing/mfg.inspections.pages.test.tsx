import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'

vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { InspectionListPage, InspectionPage } from './InspectionPages'
import { QualityTab } from '@/components/manufacturing/OrderTabs'
import { StockDocumentsTable } from '@/components/manufacturing/StockDocumentsTable'
import { BASE, PLANT, answer, get, post, put, refusal, renderAt, setup } from './testKit'

const INSP = ['manufacturing:qc:view', 'manufacturing:qc:inspect', 'manufacturing:qc:reinspect']
const ch = (seq: number, name: string, extra: Record<string, unknown> = {}) => ({ id: `c${seq}`, seq, name, kind: 'Attribute', minValue: null, maxValue: null, unit: null, isCritical: false, ...extra })
const insp = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'i1', docNo: 'QI-2611-00001', type: 'Fqc', status, orderId: 'o1', orderNo: 'MO-2611-00001', operationSeq: null, itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', lotId: 'l1', lotNumber: 'TP-BAN01-261111-01',
  lotQty: 100, sampleQty: 5, planCode: 'FQC-BAN01', planVersion: 1, receiptDocumentId: 'd9', receiptLineId: 'rl1', operationReportId: null, inspectedBy: null, inspectedAt: null, ncrRequired: false,
  reinspectionOfId: null, reinspectedById: null, releasedAt: null, createdAt: '2026-11-11T02:00:00Z', ...extra,
})
const inspDoc = (status: string, extra: Record<string, unknown> = {}, results: unknown[] = []) => ({
  inspection: insp(status, extra), samplingMethod: 'Fixed', sampleValue: 5, allowedMinorDefects: 1, units: 5,
  characteristics: [ch(10, 'Length', { kind: 'Measurement', minValue: 748, maxValue: 752, unit: 'mm', isCritical: true }), ch(20, 'Colour', { isCritical: true })], results,
})

function inspApi(status = 'Pending', extra: Record<string, unknown> = {}, results: unknown[] = []) {
  answer([
    [`${BASE}/inspections/i1`, inspDoc(status, extra, results)],
    [`${BASE}/inspections`, { items: [insp('Pending'), insp('Failed', { id: 'i2', docNo: 'QI-2611-00002', ncrRequired: true })], total: 2, page: 1, pageSize: 25 }],
    [`${BASE}/plants`, [PLANT]],
  ])
}
const openInspection = (status = 'Pending', extra: Record<string, unknown> = {}, results: unknown[] = []) => {
  inspApi(status, extra, results)
  return renderAt(<InspectionPage />, '/manufacturing/inspections/i1', '/manufacturing/inspections/:id')
}

describe('Inspections (S22, P4-3)', () => {
  beforeEach(() => setup(INSP))

  it('the worklist shows the pending inspections by default with the status badge and the NCR flag', async () => {
    inspApi()
    renderAt(<InspectionListPage />)

    expect((await screen.findAllByRole('link', { name: 'QI-2611-00001' }))[0]).toHaveAttribute('href', '/manufacturing/inspections/i1')
    expect(screen.getByText('NCR required')).toBeInTheDocument()
    await waitFor(() => expect(get).toHaveBeenCalledWith(`${BASE}/inspections`, { params: expect.objectContaining({ plantId: 'p1', status: 'Pending' }) }))
  })

  it('shows one value box per sample unit of a measurement and a pass / fail choice for an attribute, and marks a value out of range with text', async () => {
    openInspection()

    expect(await screen.findByText(/Length/)).toBeInTheDocument()
    const boxes = screen.getAllByLabelText(/^Unit \d$/)
    expect(boxes).toHaveLength(5)
    expect(screen.getByRole('radiogroup', { name: 'Colour' })).toBeInTheDocument()
    await userEvent.type(boxes[0], '753')
    expect(await screen.findByText('Out of range')).toBeInTheDocument()
    await userEvent.clear(boxes[0])
    await userEvent.type(boxes[0], '750')
    expect(await screen.findByText('In range')).toBeInTheDocument()
  })

  it('saves a draft and submits the values after a confirm that explains what a pass does', async () => {
    openInspection()
    await screen.findByText(/Length/)
    for (const b of screen.getAllByLabelText(/^Unit \d$/)) await userEvent.type(b, '750')
    await userEvent.click(screen.getByLabelText('Pass'))

    await userEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith(`${BASE}/inspections/i1/results`, expect.objectContaining({ sampleQty: 5, results: expect.arrayContaining([expect.objectContaining({ characteristicId: 'c10', sampleNo: 5, value: 750 }), { characteristicId: 'c20', sampleNo: 1, passed: true }]) })))

    post.mockResolvedValueOnce({ data: inspDoc('Passed') })
    await userEvent.click(screen.getByRole('button', { name: 'Submit' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/moves to the finished-goods warehouse/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Submit' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/inspections/i1/submit`, expect.objectContaining({ sampleQty: 5 })))
  })

  it('shows the refusal of a submit with missing values and keeps the form', async () => {
    openInspection()
    await screen.findByText(/Length/)
    post.mockRejectedValueOnce(refusal('Chưa nhập đủ kết quả cho các tiêu chí kiểm tra.', 'MFG_QC_003'))
    await userEvent.click(screen.getByRole('button', { name: 'Submit' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Submit' }))

    expect(await screen.findByText(/Chưa nhập đủ kết quả/)).toBeInTheDocument()
    expect(screen.getAllByLabelText(/^Unit \d$/)).toHaveLength(5)
  })

  it('a failed inspection shows its consequence and can be re-inspected once by a user with the right', async () => {
    openInspection('Failed', { ncrRequired: true, inspectedBy: 'qa', inspectedAt: '2026-11-11T03:00:00Z' })

    expect(await screen.findByText(/non-conformance report is required/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Submit' })).toBeNull()
    post.mockResolvedValueOnce({ data: inspDoc('Pending', { id: 'i3', docNo: 'QI-2611-00003', reinspectionOfId: 'i1' }) })
    await userEvent.click(screen.getByRole('button', { name: 'Re-inspect' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/inspections/i1/reinspect`, undefined))
  })

  it('no re-inspect button without the permission or once it was re-inspected', async () => {
    setup(['manufacturing:qc:view', 'manufacturing:qc:inspect'])
    openInspection('Failed', { ncrRequired: true })
    await screen.findByText(/non-conformance report is required/)
    expect(screen.queryByRole('button', { name: 'Re-inspect' })).toBeNull()
  })

  it('a pending inspection is read only for a user who cannot inspect', async () => {
    setup(['manufacturing:qc:view'])
    openInspection()

    await screen.findByText(/Length/)
    expect(screen.getByText(/not enter results/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Submit' })).toBeNull()
    expect(screen.getAllByLabelText(/^Unit \d$/)[0]).toBeDisabled()
  })

  it('the order Quality tab lists the inspections and says how many still decide something', () => {
    const list = [insp('Pending'), insp('Failed', { id: 'i2', docNo: 'QI-2611-00002', ncrRequired: true }), insp('Passed', { id: 'i4', docNo: 'QI-2611-00004' })]
    renderAt(<QualityTab inspections={list as never} />)

    expect(screen.getByText(/2 inspections still decide something/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'QI-2611-00002' })).toBeInTheDocument()
  })

  const receipt = (held = true) => ({
    id: 'd9', docNo: 'FR-2611-00001', plantId: 'p1', orderId: 'o1', orderNo: 'MO-2611-00001', type: 'Receipt', status: 'Posted', mode: 'Forward', postingDate: '2026-11-11', attempts: 0, createdAt: '2026-11-11T02:00:00Z', isReversed: false,
    lines: [{ lineNo: 1, itemId: 'top', itemCode: 'TP-BAN01', warehouseId: 'w3', qty: 100, isFromReservation: false, movementKey: 'FR#1', status: 'Posted', kind: 'Receipt', lotNumber: 'TP-BAN01-261111-01', isQcHold: held, qcReleasedAt: null, outputLineNo: null }],
  })

  it('a receipt line with an inspection links to it and has no interim release button; without one the button stays', () => {
    const withInspection = renderAt(<StockDocumentsTable documents={[receipt()] as never} canRetry={false} canQcRelease inspections={[insp('Pending')] as never} />)
    expect(screen.getByRole('link', { name: /QI-2611-00001/ })).toHaveAttribute('href', '/manufacturing/inspections/i1')
    expect(screen.queryByRole('button', { name: 'Release from quality hold' })).toBeNull()
    withInspection.unmount()

    renderAt(<StockDocumentsTable documents={[receipt()] as never} canRetry={false} canQcRelease inspections={[insp('Cancelled')] as never} />)
    expect(screen.getByRole('button', { name: 'Release from quality hold' })).toBeInTheDocument()
    expect(screen.getByText(/No inspection plan/)).toBeInTheDocument()
  })

  it('has no accessibility violations (worklist, document)', async () => {
    inspApi()
    const list = renderAt(<InspectionListPage />)
    await screen.findAllByRole('link', { name: 'QI-2611-00001' })
    expect(await axe(list.container)).toHaveNoViolations()
    list.unmount()

    const doc = openInspection()
    await screen.findByText(/Length/)
    expect(await axe(doc.container)).toHaveNoViolations()
  })
})
