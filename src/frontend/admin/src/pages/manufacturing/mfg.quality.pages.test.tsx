import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'

vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { InspectionPlanEditorPage, InspectionPlanListPage } from './InspectionPlanPages'
import { BASE, PLANT, answer, del, get, post, put, refusal, renderAt, setup } from './testKit'

const QC = ['manufacturing:qc:plan-view', 'manufacturing:qc:plan-manage', 'manufacturing:qc:plan-approve']

const ch = (seq: number, name: string, extra: Record<string, unknown> = {}) => ({ id: `c${seq}`, seq, name, kind: 'Attribute', minValue: null, maxValue: null, unit: null, isCritical: false, ...extra })
const plan = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'q1', plantId: 'p1', code: 'FQC-BAN01', type: 'Fqc', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version: 1, status, samplingMethod: 'Fixed', sampleValue: 5, allowedMinorDefects: 1,
  effectiveFrom: '2026-01-01', effectiveTo: null, description: null, approvedBy: status === 'Draft' ? null : 'admin', approvedAt: status === 'Draft' ? null : '2026-11-07T09:00:00Z',
  characteristics: [ch(10, 'Length', { kind: 'Measurement', minValue: 748, maxValue: 752, unit: 'mm', isCritical: true }), ch(20, 'Colour', { isCritical: true }), ch(30, 'Scratches')], ...extra,
})
const listItem = (id: string, version: number, status: string, extra: Record<string, unknown> = {}) => ({
  id, code: 'FQC-BAN01', type: 'Fqc', itemId: 'top', itemCode: 'TP-BAN01', itemName: 'Table', version, status, samplingMethod: 'Fixed', sampleValue: 5, effectiveFrom: '2026-01-01', effectiveTo: null, characteristicCount: 3, ...extra,
})

function planApi(status = 'Approved') {
  answer([
    [`${BASE}/inspection-plans/q1`, plan(status)],
    [`${BASE}/inspection-plans/q2`, plan('Draft', { id: 'q2', version: 2 })],
    [`${BASE}/inspection-plans`, [listItem('q1', 1, status), listItem('q0', 2, 'Draft')]],
    [`${BASE}/plants`, [PLANT]],
  ])
}

const openPlan = (status: string) => { planApi(status); return renderAt(<InspectionPlanEditorPage />, '/manufacturing/inspection-plans/q1', '/manufacturing/inspection-plans/:id') }

describe('Inspection plans (S21, P4-3)', () => {
  beforeEach(() => setup(QC))

  it('lists the plans of the plant with type, version, status and sample, and filters by type', async () => {
    planApi()
    renderAt(<InspectionPlanListPage />)

    const link = (await screen.findAllByRole('link', { name: 'FQC-BAN01' }))[0]
    expect(link).toHaveAttribute('href', '/manufacturing/inspection-plans/q1')
    expect(screen.getAllByText('Final (FQC)').length).toBeGreaterThan(0)
    expect(screen.getAllByText('5 units').length).toBeGreaterThan(0)

    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Type' }), 'Ipqc')
    await waitFor(() => expect(get).toHaveBeenCalledWith(`${BASE}/inspection-plans`, { params: { plantId: 'p1', type: 'Ipqc' } }))
  })

  it('a user who cannot manage plans has no New button', async () => {
    setup(['manufacturing:qc:plan-view'])
    planApi()
    renderAt(<InspectionPlanListPage />)

    await screen.findAllByRole('link', { name: 'FQC-BAN01' })
    expect(screen.queryByRole('button', { name: 'New inspection plan' })).toBeNull()
  })

  it('a draft shows its characteristics with the range and critical flag and can be approved after a confirm', async () => {
    openPlan('Draft')

    expect(await screen.findByText('Length')).toBeInTheDocument()
    expect(screen.getByText('748 – 752 mm')).toBeInTheDocument()
    expect(screen.getAllByText('Critical').length).toBeGreaterThan(1)
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/Approve plan FQC-BAN01 version 1/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: /confirm|yes|approve/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/inspection-plans/q1/approve`, undefined))
  })

  it('an approved plan is read only: no edit buttons, a note, and it can be made obsolete or copied', async () => {
    openPlan('Approved')

    await screen.findByText('Length')
    expect(screen.queryByRole('button', { name: 'Add characteristic' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Delete' })).toBeNull()
    expect(screen.getByText(/cannot be changed/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Make obsolete' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New version' })).toBeInTheDocument()
  })

  it('adds a measurement characteristic with a range and shows the server refusal for Min above Max', async () => {
    openPlan('Draft')
    await screen.findByText('Length')
    await userEvent.click(screen.getByRole('button', { name: 'Add characteristic' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Name/), 'Width')
    await userEvent.type(within(dialog).getByLabelText('Minimum'), '752')
    await userEvent.type(within(dialog).getByLabelText('Maximum'), '748')
    post.mockRejectedValueOnce(refusal('The minimum of a measurement cannot be greater than its maximum.'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText(/cannot be greater than its maximum/)).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith(`${BASE}/inspection-plans/q1/characteristics`, expect.objectContaining({ name: 'Width', kind: 'Measurement', minValue: 752, maxValue: 748, isCritical: false }))
  })

  it('removing a characteristic of a draft calls the API; a copy goes to the new version', async () => {
    openPlan('Draft')
    await screen.findByText('Length')
    const row = screen.getByText('Scratches').closest('tr') as HTMLElement
    await userEvent.click(within(row).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(del).toHaveBeenCalledWith(`${BASE}/inspection-plans/q1/characteristics/30`))

    post.mockResolvedValueOnce({ data: plan('Draft', { id: 'q2', version: 2 }) })
    await userEvent.click(screen.getByRole('button', { name: 'New version' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: /confirm|yes|new version/i }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/inspection-plans/q1/copy`, { effectiveFrom: undefined }))
  })

  it('saves the header of a draft with the sample', async () => {
    openPlan('Draft')
    await screen.findByText('Length')
    const units = screen.getByLabelText('Units')
    await userEvent.clear(units)
    await userEvent.type(units, '8')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(put).toHaveBeenCalledWith(`${BASE}/inspection-plans/q1`, expect.objectContaining({ samplingMethod: 'Fixed', sampleValue: 8, allowedMinorDefects: 1 })))
  })

  it('the versions tab lists the other versions of the series', async () => {
    openPlan('Approved')
    await screen.findByText('Length')
    await userEvent.click(screen.getByRole('tab', { name: 'Versions' }))

    expect(await screen.findByRole('link', { name: /v2/ })).toHaveAttribute('href', '/manufacturing/inspection-plans/q0')
  })

  it('has no accessibility violations (list, editor)', async () => {
    planApi('Draft')
    const list = renderAt(<InspectionPlanListPage />)
    await screen.findAllByRole('link', { name: 'FQC-BAN01' })
    expect(await axe(list.container)).toHaveNoViolations()
    list.unmount()

    const editor = openPlan('Draft')
    await screen.findByText('Length')
    expect(await axe(editor.container)).toHaveNoViolations()
  })
})
