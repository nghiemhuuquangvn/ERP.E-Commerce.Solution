import { gatewayClient as client } from '../client'

// ManufacturingService (MFG) through the Gateway: `/mfg/api/v1/manufacturing/...` is already gateway-rooted, so it goes through `gatewayClient`
// (no baseURL). Enums travel as strings both ways (the API has a string-enum converter); dates are `yyyy-MM-dd`, times `HH:mm:ss`.
const BASE = '/mfg/api/v1/manufacturing'

export type VersionStatus = 'Draft' | 'Approved' | 'Obsolete'
export type MakeOrBuy = 'Make' | 'Buy' | 'Both'
export type LotSizingPolicy = 'LotForLot' | 'FixedQty' | 'MinMultiple' | 'PeriodOrder'
export type IssueMethod = 'Manual' | 'Backflush'
export type CostingMethod = 'Actual' | 'Standard'
export type WorkCenterType = 'Machine' | 'Labor' | 'Subcontract'
export type MachineStatus = 'Active' | 'UnderMaintenance' | 'Retired'
export type RunTimeBasis = 'PerUnit' | 'PerBatch'
export type CalendarExceptionType = 'Holiday' | 'ExtraWorkingDay' | 'ModifiedShifts'
export type BomLineType = 'Component' | 'ByProduct' | 'CoProduct'
export type ReasonType = string
export type DowntimeCategory = 'Planned' | 'Unplanned'
export const DAYS = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'] as const

// ── Plants, settings, access, item profiles ──────────────────────────────────────────────────────────────
export interface Plant {
  id: string; code: string; name: string; timeZoneId: string
  rawMaterialWarehouseId: string; finishedGoodsWarehouseId: string; quarantineWarehouseId: string; scrapWarehouseId?: string | null
  defaultCalendarId?: string | null; isActive: boolean
}
export interface PlantBody {
  name: string; timeZoneId?: string | null
  rawMaterialWarehouseId: string; finishedGoodsWarehouseId: string; quarantineWarehouseId: string; scrapWarehouseId?: string | null
  defaultCalendarId?: string | null
}
export interface ProductionSettingValues {
  overProductionTolerancePct: number; underCompletionTolerancePct: number; overIssueTolerancePct: number
  blockReleaseOnShortage: boolean; allowNegativeStockOnBackflush: boolean; requireSequentialOperations: boolean; autoCompleteOnFullReceipt: boolean
  defaultIssueMethod: IssueMethod; costingMethod: CostingMethod; mrpHorizonDays: number; maxBomLevels: number; fgLotNumberFormat: string
}
export interface SettingsDto { plantId: string; values: ProductionSettingValues }
export interface SettingAudit { field: string; oldValue: string; newValue: string; changedBy: string; changedAt: string }
export interface PlantAccess { plantId: string; userId: string }

export interface ItemProfileValues {
  makeOrBuy: MakeOrBuy; fixedLeadTimeDays: number; purchaseLeadTimeDays: number; lotSizingPolicy: LotSizingPolicy
  fixedLotQty?: number | null; minOrderQty?: number | null; orderMultiple?: number | null; periodDays?: number | null
  safetyStockQty: number; standardLotSize: number; defaultIssueMethod?: IssueMethod | null
  requiresFqc: boolean; fqcInspectionPlanId?: string | null; shelfLifeDays: number
}
export interface ItemProfile { itemId: string; plantId: string; itemCode: string; itemName: string; isDefault: boolean; values: ItemProfileValues; lowLevelCode: number; standardCost: number }
export interface ItemProfileListItem { itemId: string; itemCode: string; itemName: string; makeOrBuy: MakeOrBuy; lotSizingPolicy: LotSizingPolicy; lowLevelCode: number }
export interface PagedProfiles { items: ItemProfileListItem[]; total: number; page: number; pageSize: number }

// ── Calendars ────────────────────────────────────────────────────────────────────────────────────────────
export interface Shift { id: string; plantId: string; code: string; name: string; startTime: string; endTime: string; breakMinutes: number; durationMinutes: number; isActive: boolean }
export interface ShiftBody { name: string; startTime: string; endTime: string; breakMinutes: number }
export interface CalendarDay { dayOfWeek: string | number; shiftIds: string[]; workingMinutes: number }
export interface CalendarException { date: string; type: CalendarExceptionType; description?: string | null; shiftIds: string[]; workingMinutes: number }
export interface WorkCalendar { id: string; plantId: string; code: string; name: string; isActive: boolean; week: CalendarDay[]; exceptions: CalendarException[] }
export interface MonthDay { date: string; workingMinutes: number; isException: boolean; exceptionType?: CalendarExceptionType | null }
export interface WorkingTimeAnswer { date: string; workingMinute: number; clockTime?: string }

// ── Work centers, machines, standard operations, reasons, crews ──────────────────────────────────────────
export interface WorkCenterValues {
  name: string; departmentId?: string | null; calendarId: string; type: WorkCenterType; numberOfResources: number
  efficiencyPct: number; utilizationPct: number; laborRatePerHour: number; machineRatePerHour: number
  queueTimeMinutes: number; moveTimeMinutes: number; vendorId?: string | null
}
export interface WorkCenter { id: string; plantId: string; code: string; isActive: boolean; values: WorkCenterValues }
export interface DayCapacity { date: string; workingMinutes: number; capacityMinutes: number }
export interface Machine { id: string; plantId: string; code: string; name: string; workCenterId: string; status: MachineStatus; idealCycleTimeSeconds: number; serialNo?: string | null; isActive: boolean }
export interface StandardOperationBody { name: string; defaultWorkCenterId: string; setupTimeMinutes: number; runTimeMinutes: number; runTimeBasis: RunTimeBasis; batchSize?: number | null; instructions?: string | null }
export interface StandardOperation extends StandardOperationBody { id: string; plantId: string; code: string; isActive: boolean }
export interface ReasonCode { id: string; type: ReasonType; code: string; name: string; category?: DowntimeCategory | null; isActive: boolean }
export interface CrewMember { id: string; employeeId: string; employeeNumber?: string | null; employeeName?: string | null; validFrom: string; validTo?: string | null; laborRatePerHour?: number | null }
export interface Crew { id: string; plantId: string; code: string; name: string; workCenterId?: string | null; leaderEmployeeId?: string | null; isActive: boolean; members: CrewMember[] }

// ── BOM ──────────────────────────────────────────────────────────────────────────────────────────────────
export interface BomLine {
  lineNo: number; lineType: BomLineType; componentItemId: string; componentCode: string; componentName: string; quantity: number; uomId: string
  scrapPct: number; isFixedQuantity: boolean; isPhantom: boolean; issueMethod: IssueMethod; operationSeq?: number | null
  alternativeGroup?: string | null; alternativePriority?: number | null; costAllocationPct?: number | null; notes?: string | null
}
export interface Bom {
  id: string; plantId: string; itemId: string; itemCode: string; itemName: string; version: number; baseQuantity: number; yieldPct: number; status: VersionStatus
  effectiveFrom: string; effectiveTo?: string | null; description?: string | null; approvedBy?: string | null; approvedAt?: string | null; lines: BomLine[]; warnings: string[]
}
export interface BomListItem { id: string; itemId: string; itemCode: string; itemName: string; version: number; status: VersionStatus; effectiveFrom: string; effectiveTo?: string | null; lineCount: number }
export interface BomLineBody {
  lineType: BomLineType; componentItemId: string; quantity: number; uomId: string; scrapPct: number; isFixedQuantity: boolean; isPhantom: boolean; issueMethod: IssueMethod
  operationSeq?: number | null; alternativeGroup?: string | null; alternativePriority?: number | null; costAllocationPct?: number | null; notes?: string | null
}
export interface RequirementLine { componentItemId: string; componentCode: string; componentName: string; quantity: number; operationSeq?: number | null; issueMethod: IssueMethod; alternativeGroup?: string | null; sourceLineNo: number }
export interface OutputLine { itemId: string; itemCode: string; itemName: string; lineType: BomLineType; plannedQuantity: number; costAllocationPct?: number | null; sourceLineNo: number }
export interface Explosion { bomId: string; itemId: string; version: number; orderQuantity: number; requirements: RequirementLine[]; outputs: OutputLine[] }
export interface IndentedNode {
  level: number; itemId: string; itemCode: string; itemName: string; quantity: number; isPhantom: boolean; isSubstitute: boolean
  alternativeGroup?: string | null; operationSeq?: number | null; bomVersion?: number | null; children: IndentedNode[]
}
export interface WhereUsedRow { level: number; parentItemId: string; parentCode: string; parentName: string; bomVersion: number; componentItemId: string; quantityPer: number; uomId: string }
export interface BomDifference { kind: 'Added' | 'Removed' | 'Changed'; componentItemId: string; componentCode: string; operationSeq?: number | null; leftQuantity?: number | null; rightQuantity?: number | null; detail?: string | null }
export interface LowLevelCode { itemId: string; itemCode: string; lowLevelCode: number }

// ── Routing ──────────────────────────────────────────────────────────────────────────────────────────────
export interface RoutingOperation {
  operationSeq: number; standardOperationId?: string | null; name: string; workCenterId: string; workCenterCode: string; setupTimeMinutes: number
  runTimeMinutes: number; runTimeBasis: RunTimeBasis; batchSize?: number | null; queueTimeMinutes: number; moveTimeMinutes: number; isSubcontract: boolean; instructions?: string | null
}
export interface Routing {
  id: string; plantId: string; itemId: string; itemCode: string; itemName: string; version: number; status: VersionStatus; effectiveFrom: string; effectiveTo?: string | null
  description?: string | null; approvedBy?: string | null; approvedAt?: string | null; operations: RoutingOperation[]; warnings: string[]
}
export interface RoutingListItem { id: string; itemId: string; itemCode: string; itemName: string; version: number; status: VersionStatus; effectiveFrom: string; effectiveTo?: string | null; operationCount: number }
export interface RoutingOperationBody {
  operationSeq?: number | null; standardOperationId?: string | null; name?: string | null; workCenterId?: string | null; setupTimeMinutes?: number | null; runTimeMinutes?: number | null
  runTimeBasis?: RunTimeBasis | null; batchSize?: number | null; queueTimeMinutes?: number | null; moveTimeMinutes?: number | null; isSubcontract: boolean; instructions?: string | null
}
export interface WorkPoint { date: string; workingMinute: number }
export interface ScheduledOperation { operationSeq: number; name: string; workCenterCode: string; standardMinutes: number; calendarMinutes: number; queueMinutes: number; moveMinutes: number; start: WorkPoint; end: WorkPoint; startTime: string; endTime: string }
export interface RoutingLeadTime { operations: ScheduledOperation[]; totalMinutes: number; start: WorkPoint; startDate: string; startTime: string }

// ── Production orders (P3-1..P3-5) ───────────────────────────────────────────────────────────────────────
export type OrderStatus = 'Draft' | 'Released' | 'InProgress' | 'OnHold' | 'Completed' | 'Closed' | 'Cancelled'
export type OperationStatus = 'Pending' | 'Ready' | 'Running' | 'Paused' | 'Completed'
export type StockDocumentType = 'Issue' | 'Return' | 'Backflush' | 'Receipt' | 'Reversal'
/** What a line of a Reversal document does in the Warehouse (P4-2): stock comes back, a received lot is scrapped, lot-less received stock is issued. */
export type ReversalAction = 'Return' | 'Scrap' | 'Issue'
export type StockDocumentStatus = 'Pending' | 'Posted' | 'Failed'
export type StockLineStatus = 'Pending' | 'Posted' | 'Refused' | 'Compensated'
export type StockLineKind = 'Material' | 'Receipt'
export type OperationReportStatus = 'Pending' | 'Posted' | 'Failed'
/** The reason types the order screens ask for (the API enum has more: Downtime, UnplannedMaterial). */
export type OrderReasonType = 'Scrap' | 'Rework' | 'Cancel' | 'Hold' | 'ShortClose' | 'OverIssue'

export interface OrderMaterial {
  lineNo: number; componentItemId: string; componentCode: string; componentName: string; uomId: string; operationSeq?: number | null; requiredQty: number
  issuedQty: number; returnedQty: number; consumedQty: number; reservedQty: number; scrapPct: number; issueMethod: IssueMethod; warehouseId: string
  isSubstitute: boolean; alternativeGroup?: string | null; isUnplanned: boolean
}
export interface OrderOperation {
  operationSeq: number; name: string; workCenterId: string; workCenterCode: string; setupTimeMinutes: number; runTimeMinutes: number; runTimeBasis: RunTimeBasis
  batchSize?: number | null; queueTimeMinutes: number; moveTimeMinutes: number; isSubcontract: boolean; status: OperationStatus; standardMinutes: number; calendarMinutes: number
  plannedStart?: string | null; plannedEnd?: string | null; actualStart?: string | null; actualEnd?: string | null; goodQty: number; scrapQty: number; reworkQty: number
}
export interface OrderOutput { lineNo: number; lineType: BomLineType; itemId: string; itemCode: string; itemName: string; uomId: string; plannedQty: number; receivedQty: number; costAllocationPct?: number | null }
export interface Order {
  id: string; orderNo: string; plantId: string; orderType: string; itemId: string; itemCode: string; itemName: string; quantity: number; uomId: string; bomId: string; routingId?: string | null
  status: OrderStatus; statusBeforeHold?: OrderStatus | null; priority: number; dueDate: string; plannedStart: string; plannedEnd: string; actualStart?: string | null; actualEnd?: string | null
  isLate: boolean; outputWarehouseId: string; sourceType: string; sourceRefId?: string | null; parentOrderId?: string | null; completedQty: number; scrappedQty: number; isRework: boolean
  notes?: string | null; reservationId?: string | null; reservationSyncPending: boolean; reservationSyncError?: string | null
  materials: OrderMaterial[]; operations: OrderOperation[]; outputs: OrderOutput[]; warnings: string[]; pendingStockDocuments: number
}
export interface OrderListItem {
  id: string; orderNo: string; itemId: string; itemCode: string; itemName: string; quantity: number; completedQty: number; status: OrderStatus; priority: number
  dueDate: string; plannedStart: string; plannedEnd: string; isLate: boolean
}
export interface OrderPage { items: OrderListItem[]; total: number; page: number; pageSize: number }
export interface OrderListFilter { plantId: string; status?: OrderStatus; isLate?: boolean; priority?: number; dueFrom?: string; dueTo?: string; search?: string; page: number; pageSize: number }
export interface OrderHistory { fromStatus: OrderStatus; toStatus: OrderStatus; reasonCodeId?: string | null; note?: string | null; changedBy: string; changedAt: string }
export interface MaterialAvailability { lineNo: number; componentItemId: string; componentCode: string; componentName: string; requiredQty: number; need: number; allocatable: number; shortage: number }
export interface OrderAvailability { orderId: string; hasShortage: boolean; materials: MaterialAvailability[] }
export interface OrderActionResult { order: Order; shortages: MaterialAvailability[]; reservationPending: boolean }
export interface OrderCreateBody {
  plantId: string; itemId: string; quantity: number; dueDate: string; priority?: number | null; bomId?: string | null; routingId?: string | null; outputWarehouseId?: string | null; notes?: string | null
}
export interface OrderUpdateBody { quantity: number; dueDate: string; priority?: number | null; bomId?: string | null; routingId?: string | null; outputWarehouseId?: string | null; notes?: string | null }

export interface StockDocumentLine {
  lineNo: number; orderMaterialId?: string | null; materialLineNo?: number | null; itemId: string; itemCode: string; warehouseId: string; lotId?: string | null; qty: number
  isFromReservation: boolean; movementKey: string; status: StockLineStatus; unitCost?: number | null; amount?: number | null; postedLotId?: string | null; error?: string | null
  kind: StockLineKind; outputLineNo?: number | null; lotNumber?: string | null; expiryDate?: string | null; isQcHold: boolean; qcReleasedAt?: string | null
  isNegativeStock: boolean; manufactureDate?: string | null
  reversalAction?: ReversalAction | null
  /** The lots the Warehouse really used for the line (kept since P4-2). */
  lots?: StockLineLot[] | null
}
export interface StockLineLot { lotId: string; lotNumber: string; qty: number; unitCost?: number | null; warehouseId?: string | null; parentLotId?: string | null }
export interface StockDocument {
  id: string; docNo: string; plantId: string; orderId: string; orderNo: string; type: StockDocumentType; status: StockDocumentStatus; mode: 'Forward' | 'Rollback'; postingDate: string
  reasonCodeId?: string | null; note?: string | null; attempts: number; nextAttemptAt?: string | null; lastError?: string | null; lastErrorCode?: string | null; postedAt?: string | null
  createdAt: string; createdBy?: string | null; lines: StockDocumentLine[]
  /** A Reversal document: the document it undoes. */
  reversalOfId?: string | null
  /** The document has been undone by a Posted reversal. */
  isReversed: boolean
}
export interface StockDocumentPage { items: StockDocument[]; total: number; page: number; pageSize: number }
export interface StockDocumentFilter { plantId: string; status?: StockDocumentStatus; type?: StockDocumentType; orderId?: string; search?: string; page: number; pageSize: number }
/** The answer of a document call: 201 Posted, 202 still Pending (the Warehouse is told again later). */
export interface DocumentAnswer { document: StockDocument; pending: boolean }
export interface MaterialLineBody { lineNo: number; quantity: number; lotId?: string | null }
export interface MaterialBody { lines: MaterialLineBody[]; reasonCodeId?: string | null; note?: string | null }
export interface ReceiptLineBody { outputLineNo?: number | null; quantity: number; lotNumber?: string | null }
export interface SuggestedLot { lotId: string; lotNumber: string; warehouseId?: string | null; expiryDate?: string | null; availableQuantity: number; take: number }
export interface LotSuggestion { productId: string; warehouseId?: string | null; quantity: number; lots: SuggestedLot[]; shortfall: number; lotLessAvailable: number }
export interface MaterialLotSuggestion {
  lineNo: number; componentCode: string; warehouseId: string; quantity: number; reservedRemaining: number
  reservedLots: { lotId: string; lotNumber?: string | null; remaining: number }[]; suggestion: LotSuggestion
}

export interface CloseCondition { code: string; met: boolean; message: string }
export interface CloseChecklist { orderId: string; orderNo: string; status: OrderStatus; canClose: boolean; conditions: CloseCondition[] }

export interface TimeLog {
  id: string; orderId: string; operationSeq: number; employeeId: string; employeeNumber: string; employeeName: string; machineId?: string | null; startAt: string; endAt?: string | null
  pausedAt?: string | null; pausedMinutes: number; durationMinutes?: number | null; laborRatePerHour?: number | null; isManual: boolean; state: 'Running' | 'Paused' | 'Stopped'
}
export interface OperationDetail { operation: OrderOperation; timeLogs: TimeLog[] }
export interface OperationReport {
  id: string; docNo: string; plantId: string; orderId: string; orderNo: string; operationSeq: number; reportedAt: string; postingDate: string; goodQty: number; scrapQty: number; reworkQty: number
  scrapReasonId?: string | null; reworkReasonId?: string | null; machineId?: string | null; reportedBy: string; status: OperationReportStatus; backflushDocumentId?: string | null
  failureCode?: string | null; failureMessage?: string | null; note?: string | null; postedAt?: string | null
  /** A reversal report: the report it undoes (P4-2). */
  reversalOfId?: string | null
  /** The report has been undone by a Posted reversal. */
  isReversed: boolean
}
/** 201 when the report counts, 202 (`isPending`) while its backflush document is on its way to the Warehouse. */
export interface OperationReportAnswer { report: OperationReport; isPending: boolean; backflush?: StockDocument | null }
export interface OperationReportBody {
  goodQty: number; scrapQty: number; reworkQty: number; scrapReasonId?: string | null; reworkReasonId?: string | null; machineId?: string | null; note?: string | null
}

// ── CSV import ───────────────────────────────────────────────────────────────────────────────────────────
export interface ImportRowError { line: number; key?: string | null; message: string }
export interface ImportResult { dryRun: boolean; rowCount: number; createdId?: string | null; version?: number | null; errors: ImportRowError[] }

const get = <T>(path: string, params?: object) => client.get<T>(`${BASE}${path}`, { params }).then((r) => r.data)
const post = <T>(path: string, body?: unknown) => client.post<T>(`${BASE}${path}`, body).then((r) => r.data)
const put = <T>(path: string, body?: unknown) => client.put<T>(`${BASE}${path}`, body).then((r) => r.data)
const del = <T = void>(path: string) => client.delete<T>(`${BASE}${path}`).then((r) => r.data)
const idem = (key?: string) => (key ? { headers: { 'Idempotency-Key': key } } : undefined)
/** A stock-document call: the status code tells Posted (201 / 200) from still Pending (202). */
const postDocument = (path: string, body?: unknown, key?: string): Promise<DocumentAnswer> =>
  client.post<StockDocument>(`${BASE}${path}`, body, idem(key)).then((r) => ({ document: r.data, pending: r.status === 202 }))
const csv = async (path: string, params: object, file: File) =>
  client.post<ImportResult>(`${BASE}${path}`, await file.text(), { params, headers: { 'Content-Type': 'text/csv' }, transformRequest: [(d: unknown) => d] }).then((r) => r.data)

export const mfgApi = {
  // plants
  listPlants: (activeOnly = false) => get<Plant[]>('/plants', { activeOnly }),
  createPlant: (body: PlantBody & { code: string }) => post<Plant>('/plants', body),
  updatePlant: (id: string, body: PlantBody) => put<Plant>(`/plants/${id}`, body),
  setPlantActive: (id: string, active: boolean) => post<Plant>(`/plants/${id}/${active ? 'activate' : 'deactivate'}`),
  getSettings: (plantId: string) => get<SettingsDto>(`/plants/${plantId}/settings`),
  saveSettings: (plantId: string, values: ProductionSettingValues) => put<SettingsDto>(`/plants/${plantId}/settings`, values),
  settingsAudit: (plantId: string) => get<SettingAudit[]>(`/plants/${plantId}/settings/audit`, { take: 100 }),
  listAccess: (plantId: string) => get<PlantAccess[]>(`/plants/${plantId}/access`),
  grantAccess: (plantId: string, userId: string) => post<PlantAccess>(`/plants/${plantId}/access`, { userId }),
  revokeAccess: (plantId: string, userId: string) => del(`/plants/${plantId}/access/${userId}`),
  // item profiles
  listProfiles: (plantId: string, search: string, page: number, pageSize = 20) => get<PagedProfiles>('/item-profiles', { plantId, search: search || undefined, page, pageSize }),
  getProfile: (itemId: string, plantId: string) => get<ItemProfile>(`/item-profiles/${itemId}`, { plantId }),
  saveProfile: (itemId: string, plantId: string, values: ItemProfileValues) => client.put<ItemProfile>(`${BASE}/item-profiles/${itemId}`, values, { params: { plantId } }).then((r) => r.data),
  // calendars
  listShifts: (plantId: string) => get<Shift[]>('/shifts', { plantId }),
  createShift: (plantId: string, code: string, body: ShiftBody) => post<Shift>('/shifts', { plantId, code, ...body }),
  updateShift: (id: string, body: ShiftBody) => put<Shift>(`/shifts/${id}`, body),
  setShiftActive: (id: string, active: boolean) => post<Shift>(`/shifts/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteShift: (id: string) => del(`/shifts/${id}`),
  listCalendars: (plantId: string) => get<WorkCalendar[]>('/calendars', { plantId }),
  getCalendar: (id: string) => get<WorkCalendar>(`/calendars/${id}`),
  createCalendar: (plantId: string, code: string, name: string) => post<WorkCalendar>('/calendars', { plantId, code, name }),
  renameCalendar: (id: string, name: string) => put<WorkCalendar>(`/calendars/${id}`, { name }),
  setCalendarDay: (id: string, day: string, shiftIds: string[]) => put<WorkCalendar>(`/calendars/${id}/days/${day}`, { shiftIds }),
  addException: (id: string, body: { date: string; type: CalendarExceptionType; description?: string | null; shiftIds?: string[] }) => post<WorkCalendar>(`/calendars/${id}/exceptions`, body),
  removeException: (id: string, date: string) => del<WorkCalendar>(`/calendars/${id}/exceptions/${date}`),
  setCalendarActive: (id: string, active: boolean) => post<WorkCalendar>(`/calendars/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteCalendar: (id: string) => del(`/calendars/${id}`),
  month: (id: string, year: number, month: number) => get<MonthDay[]>(`/calendars/${id}/month`, { year, month }),
  workingTime: (id: string, operation: string, date: string, workingMinute: number, amount: number) => get<WorkingTimeAnswer>(`/calendars/${id}/working-time`, { operation, date, workingMinute, amount }),
  // work centers & catalogs
  listWorkCenters: (plantId: string) => get<WorkCenter[]>('/work-centers', { plantId }),
  createWorkCenter: (plantId: string, code: string, values: WorkCenterValues) => post<WorkCenter>('/work-centers', { plantId, code, values }),
  updateWorkCenter: (id: string, values: WorkCenterValues) => put<WorkCenter>(`/work-centers/${id}`, values),
  setWorkCenterActive: (id: string, active: boolean) => post<WorkCenter>(`/work-centers/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteWorkCenter: (id: string) => del(`/work-centers/${id}`),
  capacity: (id: string, from: string, to: string) => get<DayCapacity[]>(`/work-centers/${id}/capacity`, { from, to }),
  listMachines: (plantId: string) => get<Machine[]>('/machines', { plantId }),
  createMachine: (plantId: string, body: { code: string; name: string; workCenterId: string; idealCycleTimeSeconds: number; serialNo?: string | null }) => post<Machine>('/machines', { plantId, ...body }),
  updateMachine: (id: string, body: { name: string; workCenterId: string; idealCycleTimeSeconds: number; serialNo?: string | null }) => put<Machine>(`/machines/${id}`, body),
  setMachineStatus: (id: string, status: MachineStatus) => put<Machine>(`/machines/${id}/status`, { status }),
  setMachineActive: (id: string, active: boolean) => post<Machine>(`/machines/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteMachine: (id: string) => del(`/machines/${id}`),
  listStandardOperations: (plantId: string) => get<StandardOperation[]>('/standard-operations', { plantId }),
  createStandardOperation: (plantId: string, code: string, body: StandardOperationBody) => post<StandardOperation>('/standard-operations', { plantId, code, body }),
  updateStandardOperation: (id: string, body: StandardOperationBody) => put<StandardOperation>(`/standard-operations/${id}`, body),
  setStandardOperationActive: (id: string, active: boolean) => post<StandardOperation>(`/standard-operations/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteStandardOperation: (id: string) => del(`/standard-operations/${id}`),
  listReasons: () => get<ReasonCode[]>('/reason-codes'),
  seedReasons: () => post<{ added: number }>('/reason-codes/seed-defaults'),
  createReason: (body: { type: ReasonType; code: string; name: string; category?: DowntimeCategory | null }) => post<ReasonCode>('/reason-codes', body),
  updateReason: (id: string, body: { name: string; category?: DowntimeCategory | null }) => put<ReasonCode>(`/reason-codes/${id}`, body),
  setReasonActive: (id: string, active: boolean) => post<ReasonCode>(`/reason-codes/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteReason: (id: string) => del(`/reason-codes/${id}`),
  listCrews: (plantId: string) => get<Crew[]>('/crews', { plantId }),
  createCrew: (plantId: string, body: { code: string; name: string; workCenterId?: string | null; leaderEmployeeId?: string | null }) => post<Crew>('/crews', { plantId, ...body }),
  updateCrew: (id: string, body: { name: string; workCenterId?: string | null; leaderEmployeeId?: string | null }) => put<Crew>(`/crews/${id}`, body),
  setCrewActive: (id: string, active: boolean) => post<Crew>(`/crews/${id}/${active ? 'activate' : 'deactivate'}`),
  deleteCrew: (id: string) => del(`/crews/${id}`),
  addCrewMember: (id: string, body: { employeeId: string; validFrom: string; validTo?: string | null; laborRatePerHour?: number | null }) => post<Crew>(`/crews/${id}/members`, body),
  removeCrewMember: (id: string, memberId: string) => del<Crew>(`/crews/${id}/members/${memberId}`),
  // BOM
  listBoms: (plantId: string, itemId?: string, status?: VersionStatus) => get<BomListItem[]>('/boms', { plantId, itemId, status }),
  getBom: (id: string) => get<Bom>(`/boms/${id}`),
  createBom: (body: { plantId: string; itemId: string; baseQuantity?: number; yieldPct?: number; effectiveFrom?: string; effectiveTo?: string | null; description?: string | null }) => post<Bom>('/boms', body),
  updateBom: (id: string, body: { baseQuantity: number; yieldPct: number; effectiveFrom: string; effectiveTo?: string | null; description?: string | null }) => put<Bom>(`/boms/${id}`, body),
  deleteBom: (id: string) => del(`/boms/${id}`),
  addBomLine: (id: string, body: BomLineBody) => post<Bom>(`/boms/${id}/lines`, body),
  updateBomLine: (id: string, lineNo: number, body: BomLineBody) => put<Bom>(`/boms/${id}/lines/${lineNo}`, body),
  removeBomLine: (id: string, lineNo: number) => del<Bom>(`/boms/${id}/lines/${lineNo}`),
  approveBom: (id: string) => post<Bom>(`/boms/${id}/approve`),
  obsoleteBom: (id: string, effectiveTo?: string) => post<Bom>(`/boms/${id}/obsolete`, { effectiveTo }),
  copyBom: (id: string, effectiveFrom?: string) => post<Bom>(`/boms/${id}/copy`, { effectiveFrom }),
  explode: (plantId: string, itemId: string, quantity: number, bomId?: string, date?: string) => get<Explosion>('/boms/explode', { plantId, itemId, quantity, bomId, date }),
  indented: (plantId: string, itemId: string, quantity: number, bomId?: string, date?: string) => get<IndentedNode>('/boms/indented', { plantId, itemId, quantity, bomId, date }),
  whereUsed: (plantId: string, itemId: string) => get<WhereUsedRow[]>('/boms/where-used', { plantId, itemId }),
  compareBoms: (left: string, right: string) => get<BomDifference[]>('/boms/compare', { left, right }),
  lowLevelCodes: (plantId: string) => get<LowLevelCode[]>('/boms/low-level-codes', { plantId }),
  importBom: (file: File, params: { plantId: string; itemId: string; effectiveFrom?: string; dryRun: boolean }) => csv('/boms/import', params, file),
  bomTemplateUrl: `${BASE}/boms/import/template`,
  // routing
  listRoutings: (plantId: string, itemId?: string, status?: VersionStatus) => get<RoutingListItem[]>('/routings', { plantId, itemId, status }),
  getRouting: (id: string) => get<Routing>(`/routings/${id}`),
  createRouting: (body: { plantId: string; itemId: string; effectiveFrom?: string; effectiveTo?: string | null; description?: string | null }) => post<Routing>('/routings', body),
  updateRouting: (id: string, body: { effectiveFrom: string; effectiveTo?: string | null; description?: string | null }) => put<Routing>(`/routings/${id}`, body),
  deleteRouting: (id: string) => del(`/routings/${id}`),
  addOperation: (id: string, body: RoutingOperationBody) => post<Routing>(`/routings/${id}/operations`, body),
  addOperationFromStandard: (id: string, standardOperationId: string, body: Partial<RoutingOperationBody> = {}) => post<Routing>(`/routings/${id}/operations/from-standard/${standardOperationId}`, { isSubcontract: false, ...body }),
  updateOperation: (id: string, seq: number, body: RoutingOperationBody) => put<Routing>(`/routings/${id}/operations/${seq}`, body),
  removeOperation: (id: string, seq: number) => del<Routing>(`/routings/${id}/operations/${seq}`),
  renumber: (id: string, orderedSeqs: number[]) => post<Routing>(`/routings/${id}/renumber`, { orderedSeqs }),
  approveRouting: (id: string) => post<Routing>(`/routings/${id}/approve`),
  obsoleteRouting: (id: string, effectiveTo?: string) => post<Routing>(`/routings/${id}/obsolete`, { effectiveTo }),
  copyRouting: (id: string, effectiveFrom?: string) => post<Routing>(`/routings/${id}/copy`, { effectiveFrom }),
  leadTime: (id: string, quantity: number, endDate: string) => get<RoutingLeadTime>(`/routings/${id}/lead-time`, { quantity, endDate }),
  importRouting: (file: File, params: { plantId: string; itemId: string; effectiveFrom?: string; dryRun: boolean }) => csv('/routings/import', params, file),
  routingTemplateUrl: `${BASE}/routings/import/template`,
  // production orders
  listOrders: (f: OrderListFilter) => get<OrderPage>('/orders', f),
  getOrder: (id: string) => get<Order>(`/orders/${id}`),
  orderHistory: (id: string) => get<OrderHistory[]>(`/orders/${id}/history`),
  orderAvailability: (id: string) => get<OrderAvailability>(`/orders/${id}/availability`),
  createOrder: (body: OrderCreateBody) => post<Order>('/orders', body),
  updateOrder: (id: string, body: OrderUpdateBody) => put<Order>(`/orders/${id}`, body),
  releaseOrder: (id: string) => post<OrderActionResult>(`/orders/${id}/release`),
  unreleaseOrder: (id: string) => post<OrderActionResult>(`/orders/${id}/unrelease`),
  cancelOrder: (id: string, reasonCodeId: string | null, note?: string | null) => post<OrderActionResult>(`/orders/${id}/cancel`, { reasonCodeId, note }),
  holdOrder: (id: string, reasonCodeId: string | null, note?: string | null) => post<OrderActionResult>(`/orders/${id}/hold`, { reasonCodeId, note }),
  resumeOrder: (id: string) => post<OrderActionResult>(`/orders/${id}/resume`),
  changeQuantity: (id: string, quantity: number, note?: string | null) => post<OrderActionResult>(`/orders/${id}/change-quantity`, { quantity, note }),
  completeOrder: (id: string, reasonCodeId?: string | null, note?: string | null) => post<OrderActionResult>(`/orders/${id}/complete`, { reasonCodeId: reasonCodeId ?? null, note: note ?? null }),
  reopenOrder: (id: string) => post<OrderActionResult>(`/orders/${id}/reopen`),
  closeChecklist: (id: string, confirmConsumeRemaining = false) => get<CloseChecklist>(`/orders/${id}/close-checklist`, { confirmConsumeRemaining }),
  closeOrder: (id: string, confirmConsumeRemaining: boolean) => post<Order>(`/orders/${id}/close`, { confirmConsumeRemaining }),
  // material issue / return, receipts, stock documents
  issueMaterial: (id: string, body: MaterialBody, key?: string) => postDocument(`/orders/${id}/material-issues`, body, key),
  returnMaterial: (id: string, body: MaterialBody, key?: string) => postDocument(`/orders/${id}/material-returns`, body, key),
  suggestLots: (id: string, lineNo: number, quantity: number) => get<MaterialLotSuggestion>(`/orders/${id}/material-issues/suggest-lots`, { lineNo, quantity }),
  receiveGoods: (id: string, body: { lines: ReceiptLineBody[]; note?: string | null }, key?: string) => postDocument(`/orders/${id}/receipts`, body, key),
  orderDocuments: (id: string) => get<StockDocument[]>(`/orders/${id}/stock-documents`),
  listDocuments: (f: StockDocumentFilter) => get<StockDocumentPage>('/stock-documents', f),
  retryDocument: (id: string) => postDocument(`/stock-documents/${id}/retry`),
  abandonDocument: (id: string) => postDocument(`/stock-documents/${id}/abandon`),
  qcRelease: (id: string) => post<StockDocument>(`/stock-documents/${id}/qc-release`),
  // operations
  orderOperations: (id: string) => get<OperationDetail[]>(`/orders/${id}/operations`),
  operationReports: (id: string) => get<OperationReport[]>(`/orders/${id}/operation-reports`),
  startOperation: (id: string, seq: number, employeeId: string, machineId?: string | null) => post<TimeLog>(`/orders/${id}/operations/${seq}/start`, { employeeId, machineId: machineId ?? null }),
  addManualTime: (id: string, seq: number, body: { employeeId: string; machineId?: string | null; startAt: string; endAt: string }) => post<TimeLog>(`/orders/${id}/operations/${seq}/time-logs`, body),
  pauseLog: (id: string, seq: number, logId: string) => post<TimeLog>(`/orders/${id}/operations/${seq}/time-logs/${logId}/pause`),
  resumeLog: (id: string, seq: number, logId: string) => post<TimeLog>(`/orders/${id}/operations/${seq}/time-logs/${logId}/resume`),
  stopLog: (id: string, seq: number, logId: string) => post<TimeLog>(`/orders/${id}/operations/${seq}/time-logs/${logId}/stop`),
  reportOperation: (id: string, seq: number, body: OperationReportBody, key?: string) =>
    client.post<OperationReportAnswer>(`${BASE}/orders/${id}/operations/${seq}/report`, body, idem(key)).then((r) => r.data),
  /** Reverses a Posted operation report with a counter-report (BR-OP-07): 201 when it counts, 202 (`isPending`) while the material is on its way back to the Warehouse. */
  reverseOperationReport: (orderId: string, reportId: string, note: string, key?: string) =>
    client.post<OperationReportAnswer>(`${BASE}/orders/${orderId}/operation-reports/${reportId}/reverse`, { note }, idem(key)).then((r) => r.data),
  /** Reverses a Posted finished-goods receipt (BR-FG-07): 201 Posted, 202 still Pending. */
  reverseReceipt: (orderId: string, documentId: string, note: string, key?: string) => postDocument(`/orders/${orderId}/receipts/${documentId}/reverse`, { note }, key),
  completeOperation: (id: string, seq: number) => post<OrderOperation>(`/orders/${id}/operations/${seq}/complete`),
  reopenOperation: (id: string, seq: number) => post<OrderOperation>(`/orders/${id}/operations/${seq}/reopen`),
}

/** Downloads a CSV template with the user's token (an anchor cannot send the Authorization header). */
export async function downloadTemplate(url: string, filename: string) {
  const res = await client.get<Blob>(url, { responseType: 'blob' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(res.data)
  link.download = filename
  link.click()
  URL.revokeObjectURL(link.href)
}
