import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { axe } from '@/test/axe'

vi.mock('@/api/client', () => {
  const none = () => Promise.resolve({ data: {} })
  const client = { get: vi.fn(none), post: vi.fn(none), put: vi.fn(none), patch: vi.fn(none), delete: vi.fn(none) }
  return { apiClient: client, gatewayClient: client }
})

import { PlantsPage } from './PlantsPage'
import { SettingsPage } from './SettingsPage'
import { CalendarsPage } from './CalendarsPage'
import { WorkCentersPage } from './WorkCentersPage'
import { CatalogsPage } from './CatalogsPage'
import { ItemProfilesPage } from './ItemProfilesPage'
import { BASE, PLANT, answer, del, get, put, post, refusal, renderAt, setup } from './testKit'

globalThis.ResizeObserver ??= class { observe() {} unobserve() {} disconnect() {} } as never

const SETTINGS = {
  overProductionTolerancePct: 5, underCompletionTolerancePct: 2, overIssueTolerancePct: 3, blockReleaseOnShortage: false, allowNegativeStockOnBackflush: false, requireSequentialOperations: true,
  autoCompleteOnFullReceipt: true, defaultIssueMethod: 'Manual', costingMethod: 'Actual', mrpHorizonDays: 90, maxBomLevels: 20, fgLotNumberFormat: '{ItemCode}-{yyMMdd}-{seq2}',
}
const CA1 = { id: 's1', plantId: 'p1', code: 'CA1', name: 'Shift 1', startTime: '06:00:00', endTime: '14:00:00', breakMinutes: 30, durationMinutes: 450, isActive: true }
const CA3 = { id: 's3', plantId: 'p1', code: 'CA3', name: 'Night', startTime: '22:00:00', endTime: '06:00:00', breakMinutes: 30, durationMinutes: 450, isActive: true }
const CAL = {
  id: 'c1', plantId: 'p1', code: 'CAL-5D', name: 'Five-day week', isActive: true,
  week: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'].map((d) => ({ dayOfWeek: d, shiftIds: d === 'Sunday' ? [] : ['s1'], workingMinutes: d === 'Sunday' ? 0 : 450 })),
  exceptions: [{ date: '2027-01-01', type: 'Holiday', description: 'New year', shiftIds: [], workingMinutes: 0 }],
}
const CENTER = { id: 'wc1', plantId: 'p1', code: 'WC-CUT', isActive: true, values: { name: 'Cutting', calendarId: 'c1', type: 'Machine', numberOfResources: 2, efficiencyPct: 100, utilizationPct: 90, laborRatePerHour: 60000, machineRatePerHour: 120000, queueTimeMinutes: 0, moveTimeMinutes: 0 } }

beforeEach(() => setup())

describe('Plants', () => {
  it('lists plants, offers Add to a manager and has no axe violations', async () => {
    answer([[`${BASE}/plants`, [PLANT, { ...PLANT, id: 'p2', code: 'PL02', name: 'Hanoi', isActive: false }]]])
    const { container } = renderAt(<PlantsPage />)
    expect(await screen.findByText('PL01')).toBeInTheDocument()
    expect(screen.getByText('PL02')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add plant' })).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('hides the manager actions without the permission', async () => {
    setup(['manufacturing:masterdata:view'])
    renderAt(<PlantsPage />)
    await screen.findByText('PL01')
    expect(screen.queryByRole('button', { name: 'Add plant' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
  })

  it('sends the chosen warehouses when a plant is created and shows a refusal of the server', async () => {
    answer([[`${BASE}/plants`, []], ['/warehouses', [{ id: 'w1', code: 'RM', name: 'Raw', address: null, isActive: true }, { id: 'w2', code: 'FG', name: 'Finished', address: null, isActive: true }, { id: 'w3', code: 'QC', name: 'Quarantine', address: null, isActive: true }]]])
    post.mockRejectedValueOnce(refusal('Warehouse FG is not active.'))
    renderAt(<PlantsPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Add plant' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/^Code/), 'PL09')
    await userEvent.type(within(dialog).getByLabelText(/^Name/), 'Test plant')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Raw material warehouse/), 'w1')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Finished goods warehouse/), 'w2')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Quarantine warehouse/), 'w3')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/plants`, expect.objectContaining({ code: 'PL09', rawMaterialWarehouseId: 'w1', finishedGoodsWarehouseId: 'w2', quarantineWarehouseId: 'w3' })))
    expect(await within(dialog).findByText('Warehouse FG is not active.')).toBeInTheDocument()
  })
})

describe('Production settings', () => {
  it('shows the parameters and the audit trail, and saves a change', async () => {
    answer([
      [`${BASE}/plants/p1/settings/audit`, [{ field: 'OverProductionTolerancePct', oldValue: '0', newValue: '5', changedBy: 'admin', changedAt: '2026-11-07T09:00:00Z' }]],
      [`${BASE}/plants/p1/settings`, { plantId: 'p1', values: SETTINGS }],
      [`${BASE}/plants`, [PLANT]],
    ])
    const { container } = renderAt(<SettingsPage />)
    const over = await screen.findByLabelText(/Over-production tolerance/)
    expect(over).toHaveValue(5)
    expect(await screen.findByText('0 → 5')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()

    await userEvent.clear(over)
    await userEvent.type(over, '7')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith(`${BASE}/plants/p1/settings`, expect.objectContaining({ overProductionTolerancePct: 7, costingMethod: 'Actual', maxBomLevels: 20 })))
    expect(await axe(container)).toHaveNoViolations()
  })

  it('shows the server refusal (costing method with open orders)', async () => {
    answer([[`${BASE}/plants/p1/settings/audit`, []], [`${BASE}/plants/p1/settings`, { plantId: 'p1', values: SETTINGS }], [`${BASE}/plants`, [PLANT]]])
    put.mockRejectedValueOnce(refusal('The costing method cannot change while orders are open.', 'MFG_SET_001'))
    renderAt(<SettingsPage />)
    await userEvent.selectOptions(await screen.findByLabelText(/Costing method/), 'Standard')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('The costing method cannot change while orders are open.')).toBeInTheDocument()
  })

  it('is read only without the settings permission', async () => {
    setup(['manufacturing:masterdata:view'])
    answer([[`${BASE}/plants/p1/settings/audit`, []], [`${BASE}/plants/p1/settings`, { plantId: 'p1', values: SETTINGS }], [`${BASE}/plants`, [PLANT]]])
    renderAt(<SettingsPage />)
    expect(await screen.findByText(/read these parameters but not change/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull()
  })

  it('guides the user when there is no plant', async () => {
    answer([[`${BASE}/plants`, []]])
    renderAt(<SettingsPage />)
    expect(await screen.findByText('No plant yet')).toBeInTheDocument()
  })
})

describe('Shifts and calendars', () => {
  const month = Array.from({ length: 30 }, (_, i) => {
    const date = `2026-11-${String(i + 1).padStart(2, '0')}`
    const dow = new Date(date + 'T00:00:00').getDay()
    return { date, workingMinutes: dow === 0 ? 0 : dow === 6 ? 450 : 900, isException: false, exceptionType: null }
  })

  it('shows overnight shifts with their working minutes', async () => {
    answer([[`${BASE}/shifts`, [CA1, CA3]], [`${BASE}/calendars`, [CAL]], [`${BASE}/plants`, [PLANT]]])
    renderAt(<CalendarsPage />)
    await userEvent.click(await screen.findByRole('tab', { name: 'Shifts' }))
    expect(await screen.findByText(/22:00 - 06:00 \(overnight\)/)).toBeInTheDocument()
    expect(screen.getAllByText('450').length).toBeGreaterThanOrEqual(2)
  })

  it('shows the weekly pattern, the exceptions and the month view of the calendar', async () => {
    answer([[`${BASE}/calendars/c1/month`, month], [`${BASE}/shifts`, [CA1]], [`${BASE}/calendars`, [CAL]], [`${BASE}/plants`, [PLANT]]])
    const { container } = renderAt(<CalendarsPage />)
    expect(await screen.findByRole('heading', { name: 'CAL-5D - Five-day week' })).toBeInTheDocument()
    const monday = within(screen.getByRole('table', { name: 'Weekly pattern' })).getByRole('row', { name: /^Mon/ })
    expect(within(monday).getByRole('checkbox', { name: 'CA1' })).toBeChecked()
    expect(screen.getByText('2027-01-01')).toBeInTheDocument()
    // the month grid: working minutes of a weekday / Saturday / Sunday
    const grid = await screen.findByRole('table', { name: 'Month view (working minutes per day)' })
    await waitFor(() => expect(within(grid).getAllByText('900').length).toBeGreaterThan(10))
    expect(within(grid).getAllByText('450').length).toBeGreaterThan(0)
    expect(within(grid).getAllByText('0').length).toBeGreaterThan(0)
    expect(await axe(container)).toHaveNoViolations()
  })

  it('saves a changed weekday and shows an overlap refusal', async () => {
    answer([[`${BASE}/calendars/c1/month`, month], [`${BASE}/shifts`, [CA1, { ...CA1, id: 's2', code: 'CA2' }]], [`${BASE}/calendars`, [CAL]], [`${BASE}/plants`, [PLANT]]])
    put.mockRejectedValueOnce(refusal('Shifts CA1 and CA2 overlap.', 'MFG_CAL_002'))
    renderAt(<CalendarsPage />)
    const monday = within(await screen.findByRole('table', { name: 'Weekly pattern' })).getByRole('row', { name: /^Mon/ })
    await userEvent.click(within(monday).getByRole('checkbox', { name: 'CA2' }))
    await waitFor(() => expect(put).toHaveBeenCalledWith(`${BASE}/calendars/c1/days/Monday`, { shiftIds: ['s1', 's2'] }))
    expect(await screen.findByText('Shifts CA1 and CA2 overlap.')).toBeInTheDocument()
  })
})

describe('Work centers', () => {
  it('lists the work centers and charts the capacity of the chosen one', async () => {
    answer([
      [`${BASE}/work-centers/wc1/capacity`, [{ date: '2026-11-09', workingMinutes: 900, capacityMinutes: 1620 }, { date: '2026-11-14', workingMinutes: 450, capacityMinutes: 810 }]],
      [`${BASE}/work-centers`, [CENTER]], [`${BASE}/plants`, [PLANT]],
    ])
    const { container } = renderAt(<WorkCentersPage />)
    expect(await screen.findByText('WC-CUT')).toBeInTheDocument()
    expect(screen.getByText('90%')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Capacity' }))
    expect(await screen.findByText(/Total capacity in the range: 2,430/)).toBeInTheDocument()
    const table = screen.getByRole('table', { name: 'Capacity of WC-CUT' })
    expect(within(table).getByText('1620')).toBeInTheDocument()
    expect(await axe(container)).toHaveNoViolations()
  })

  it('says "deactivate instead" when a used work center cannot be deleted', async () => {
    answer([[`${BASE}/work-centers`, [CENTER]], [`${BASE}/plants`, [PLANT]]])
    del.mockRejectedValueOnce(refusal('The record is in use.', 'MFG_GEN_005'))
    renderAt(<WorkCentersPage />)
    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: /Delete|Confirm/ }))
    expect(await screen.findByText(/The record is in use\. If it is in use, deactivate it instead\./)).toBeInTheDocument()
  })
})

describe('Machines, standard operations, reasons and crews', () => {
  it('shows machines with their status and lets a manager change it', async () => {
    answer([[`${BASE}/machines`, [{ id: 'm1', plantId: 'p1', code: 'M-CUT-01', name: 'Saw', workCenterId: 'wc1', status: 'Active', idealCycleTimeSeconds: 60, serialNo: null, isActive: true }]], [`${BASE}/work-centers`, [CENTER]], [`${BASE}/plants`, [PLANT]]])
    renderAt(<CatalogsPage />)
    expect(await screen.findByText('M-CUT-01')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Status M-CUT-01'), 'Retired')
    await waitFor(() => expect(put).toHaveBeenCalledWith(`${BASE}/machines/m1/status`, { status: 'Retired' }))
  })

  it('lists reason codes by type and adds the default ones', async () => {
    answer([[`${BASE}/reason-codes`, [{ id: 'r1', type: 'Downtime', code: 'DT-01', name: 'Breakdown', category: 'Unplanned', isActive: true }]], [`${BASE}/work-centers`, []], [`${BASE}/plants`, [PLANT]]])
    post.mockResolvedValueOnce({ data: { added: 3 } })
    renderAt(<CatalogsPage />)
    await userEvent.click(await screen.findByRole('tab', { name: 'Reason codes' }))
    expect(await screen.findByText('DT-01')).toBeInTheDocument()
    expect(screen.getByText('Unplanned')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Add default reasons' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith(`${BASE}/reason-codes/seed-defaults`, undefined))
  })

  it('shows crew members and refuses an employee that is already in a crew', async () => {
    const crew = { id: 'cr1', plantId: 'p1', code: 'T1', name: 'Crew 1', workCenterId: 'wc1', leaderEmployeeId: null, isActive: true, members: [{ id: 'mem1', employeeId: 'e1', employeeNumber: 'E001', employeeName: 'An Nguyen', validFrom: '2026-01-01', validTo: null, laborRatePerHour: 70000 }] }
    answer([['/employees', { items: [{ id: 'e2', employeeNumber: 'E002', fullName: 'Binh Tran' }], totalCount: 1 }], [`${BASE}/crews`, [crew]], [`${BASE}/work-centers`, [CENTER]], [`${BASE}/plants`, [PLANT]]])
    post.mockRejectedValueOnce(refusal('Employee E002 is already in another crew in this period.', 'MFG_LBR_001'))
    renderAt(<CatalogsPage />)
    await userEvent.click(await screen.findByRole('tab', { name: 'Crews' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Members' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('E001 An Nguyen')).toBeInTheDocument()
    await userEvent.selectOptions(within(dialog).getByLabelText('Employee'), 'e2')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add' }))
    expect(await within(dialog).findByText('Employee E002 is already in another crew in this period.')).toBeInTheDocument()
  })
})

describe('Item profiles', () => {
  it('lists profiles with the low-level code and shows policy fields only when needed', async () => {
    answer([
      [`${BASE}/item-profiles/i1`, { itemId: 'i1', plantId: 'p1', itemCode: 'TP-BAN01', itemName: 'Table', isDefault: false, lowLevelCode: 0, standardCost: 0, values: { makeOrBuy: 'Make', fixedLeadTimeDays: 2, purchaseLeadTimeDays: 0, lotSizingPolicy: 'LotForLot', safetyStockQty: 10, standardLotSize: 1, requiresFqc: true, shelfLifeDays: 0 } }],
      [`${BASE}/item-profiles`, { items: [{ itemId: 'i1', itemCode: 'TP-BAN01', itemName: 'Table', makeOrBuy: 'Make', lotSizingPolicy: 'LotForLot', lowLevelCode: 0 }], total: 1, page: 1, pageSize: 20 }],
      [`${BASE}/plants`, [PLANT]],
    ])
    renderAt(<ItemProfilesPage />)
    expect(await screen.findByText('TP-BAN01')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).queryByLabelText(/Fixed lot quantity/)).toBeNull()
    await userEvent.selectOptions(await within(dialog).findByLabelText('Lot sizing policy'), 'FixedQty')
    expect(within(dialog).getByLabelText(/Fixed lot quantity/)).toBeInTheDocument()
    expect(within(dialog).getByText('Low-level code')).toBeInTheDocument()
  })
})

describe('Calendar requests', () => {
  it('asks the month of the calendar from the server', async () => {
    answer([[`${BASE}/calendars/c1/month`, []], [`${BASE}/shifts`, [CA1]], [`${BASE}/calendars`, [CAL]], [`${BASE}/plants`, [PLANT]]])
    renderAt(<CalendarsPage />)
    await screen.findByRole('heading', { name: 'CAL-5D - Five-day week' })
    await waitFor(() => expect(get.mock.calls.some(([url]) => String(url).includes('/calendars/c1/month'))).toBe(true))
  })
})
