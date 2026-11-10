import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Order, type OrderOperation, type OperationReportAnswer } from '@/api/manufacturing/mfg.api'
import { employeesApi } from '@/api/masterdata/employees.api'
import { Alert, Button, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { fmtQty, useIdempotencyKey } from './shared'

/** Active employees to pick from (first 200; filtered by what is typed in the select's label list). */
function useEmployees() {
  return useQuery({ queryKey: ['mfg', 'employees', 'active'], queryFn: () => employeesApi.list({ status: 0, pageSize: 200 }).then((p) => p.items), staleTime: 60_000 })
}

function useMachines(order: Order, op: OrderOperation) {
  const { data = [] } = useQuery({ queryKey: ['mfg', 'machines', order.plantId], queryFn: () => mfgApi.listMachines(order.plantId), staleTime: 60_000 })
  return data.filter((m) => m.workCenterId === op.workCenterId && m.isActive && m.status !== 'Retired')
}

interface OperationDialogProps<T = void> {
  order: Order
  op: OrderOperation
  onClose: () => void
  onDone: (result: T) => void
}

function EmployeeAndMachine({ order, op, employeeId, machineId, setEmployeeId, setMachineId }: {
  order: Order; op: OrderOperation; employeeId: string; machineId: string; setEmployeeId: (v: string) => void; setMachineId: (v: string) => void
}) {
  const { t } = useTranslation()
  const { data: employees = [], isLoading } = useEmployees()
  const machines = useMachines(order, op)
  return (
    <>
      <FormField label={t('mfg.opx.employee')} required hint={isLoading ? t('common.loading') : undefined}>
        <Select value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
          <option value="">{t('common.select')}</option>
          {employees.map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} - {e.fullName}</option>)}
        </Select>
      </FormField>
      {machines.length > 0 && (
        <FormField label={t('mfg.opx.machine')}>
          <Select value={machineId} onChange={(e) => setMachineId(e.target.value)}>
            <option value="">{t('common.none')}</option>
            {machines.map((m) => <option key={m.id} value={m.id}>{m.code} - {m.name}</option>)}
          </Select>
        </FormField>
      )}
    </>
  )
}

/** An employee starts working on an operation (BR-OP-02): a Ready or Paused operation becomes Running, the first start also starts the order. */
export function StartOperationDialog({ order, op, onClose, onDone }: OperationDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const [employeeId, setEmployeeId] = useState('')
  const [machineId, setMachineId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const start = useMutation({
    mutationFn: () => mfgApi.startOperation(order.id, op.operationSeq, employeeId, machineId || null),
    onSuccess: () => { toast.success(t('mfg.opx.started', { seq: op.operationSeq })); onDone() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <Modal open size="sm" title={t('mfg.opx.startTitle', { seq: op.operationSeq, name: op.name })} onClose={onClose} closeOnEsc={!start.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={!employeeId} loading={start.isPending} onClick={() => { setError(null); start.mutate() }}>{t('mfg.opx.start')}</Button></>}>
      <div className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <EmployeeAndMachine order={order} op={op} employeeId={employeeId} machineId={machineId} setEmployeeId={setEmployeeId} setMachineId={setMachineId} />
      </div>
    </Modal>
  )
}

const localInput = (iso: Date) => new Date(iso.getTime() - iso.getTimezoneOffset() * 60000).toISOString().slice(0, 16)

/** A finished work session entered afterwards (BR-OP-05: end after start, start not in the future, no overlap with the employee's other sessions). */
export function ManualTimeDialog({ order, op, onClose, onDone }: OperationDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const [employeeId, setEmployeeId] = useState('')
  const [machineId, setMachineId] = useState('')
  const [startAt, setStartAt] = useState(() => localInput(new Date(Date.now() - 3600_000)))
  const [endAt, setEndAt] = useState(() => localInput(new Date()))
  const [error, setError] = useState<string | null>(null)
  const add = useMutation({
    mutationFn: () => mfgApi.addManualTime(order.id, op.operationSeq, { employeeId, machineId: machineId || null, startAt: new Date(startAt).toISOString(), endAt: new Date(endAt).toISOString() }),
    onSuccess: () => { toast.success(t('mfg.opx.timeAdded')); onDone() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const ordered = startAt !== '' && endAt !== '' && new Date(endAt) > new Date(startAt)
  return (
    <Modal open size="md" title={t('mfg.opx.manualTitle', { seq: op.operationSeq, name: op.name })} onClose={onClose} closeOnEsc={!add.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={!employeeId || !ordered} loading={add.isPending} onClick={() => { setError(null); add.mutate() }}>{t('mfg.opx.addTime')}</Button></>}>
      <div className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <EmployeeAndMachine order={order} op={op} employeeId={employeeId} machineId={machineId} setEmployeeId={setEmployeeId} setMachineId={setMachineId} />
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label={t('mfg.opx.from')} required><Input type="datetime-local" value={startAt} onChange={(e) => setStartAt(e.target.value)} /></FormField>
          <FormField label={t('mfg.opx.to')} required error={startAt && endAt && !ordered ? t('mfg.opx.endAfterStart') : undefined}><Input type="datetime-local" value={endAt} onChange={(e) => setEndAt(e.target.value)} /></FormField>
        </div>
      </div>
    </Modal>
  )
}

/** The operation report (document OR): good, scrap and rework quantities; scrap and rework need a reason; backflush materials are booked with it. */
export function ReportOperationDialog({ order, op, onClose, onDone }: OperationDialogProps<OperationReportAnswer>) {
  const { t } = useTranslation()
  const toast = useToast()
  const keyFor = useIdempotencyKey()
  const [good, setGood] = useState('')
  const [scrap, setScrap] = useState('')
  const [rework, setRework] = useState('')
  const [scrapReason, setScrapReason] = useState('')
  const [reworkReason, setReworkReason] = useState('')
  const [machineId, setMachineId] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const machines = useMachines(order, op)
  const { data: reasons = [] } = useQuery({ queryKey: ['mfg', 'reasons'], queryFn: () => mfgApi.listReasons(), staleTime: 30_000 })
  const num = (v: string) => (v.trim() === '' ? 0 : Number(v))
  const total = num(good) + num(scrap) + num(rework)
  const valid = total > 0 && num(good) >= 0 && num(scrap) >= 0 && num(rework) >= 0 && (num(scrap) === 0 || !!scrapReason) && (num(rework) === 0 || !!reworkReason)

  const report = useMutation({
    mutationFn: () => {
      const body = { goodQty: num(good), scrapQty: num(scrap), reworkQty: num(rework), scrapReasonId: num(scrap) > 0 ? scrapReason : null, reworkReasonId: num(rework) > 0 ? reworkReason : null, machineId: machineId || null, note: note.trim() || null }
      return mfgApi.reportOperation(order.id, op.operationSeq, body, keyFor(body))
    },
    onSuccess: (answer) => { answer.isPending ? toast.info(t('mfg.opx.reportPending', { doc: answer.report.docNo })) : toast.success(t('mfg.opx.reported', { doc: answer.report.docNo })); onDone(answer) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const reasonSelect = (type: 'Scrap' | 'Rework', value: string, set: (v: string) => void, label: string) => (
    <FormField label={label} required={type === 'Scrap' ? num(scrap) > 0 : num(rework) > 0}>
      <Select value={value} onChange={(e) => set(e.target.value)}>
        <option value="">{t('common.select')}</option>
        {reasons.filter((r) => r.type === type && r.isActive).map((r) => <option key={r.id} value={r.id}>{r.code} - {r.name}</option>)}
      </Select>
    </FormField>
  )

  return (
    <Modal open size="md" title={t('mfg.opx.reportTitle', { seq: op.operationSeq, name: op.name })} onClose={onClose} closeOnEsc={!report.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button disabled={!valid} loading={report.isPending} onClick={() => { setError(null); report.mutate() }}>{t('mfg.opx.report')}</Button></>}>
      <div className="space-y-4">
        <p className="text-sm text-ink-secondary">{t('mfg.opx.reportHint', { good: fmtQty(op.goodQty), scrap: fmtQty(op.scrapQty), rework: fmtQty(op.reworkQty) })}</p>
        {error && <Alert variant="danger">{error}</Alert>}
        <div className="grid gap-4 sm:grid-cols-3">
          <FormField label={t('mfg.opx.good')}><Input type="number" step="any" min="0" value={good} onChange={(e) => setGood(e.target.value)} /></FormField>
          <FormField label={t('mfg.opx.scrap')}><Input type="number" step="any" min="0" value={scrap} onChange={(e) => setScrap(e.target.value)} /></FormField>
          <FormField label={t('mfg.opx.rework')}><Input type="number" step="any" min="0" value={rework} onChange={(e) => setRework(e.target.value)} /></FormField>
        </div>
        {num(scrap) > 0 && reasonSelect('Scrap', scrapReason, setScrapReason, t('mfg.opx.scrapReason'))}
        {num(rework) > 0 && reasonSelect('Rework', reworkReason, setReworkReason, t('mfg.opx.reworkReason'))}
        {machines.length > 0 && (
          <FormField label={t('mfg.opx.machine')}>
            <Select value={machineId} onChange={(e) => setMachineId(e.target.value)}>
              <option value="">{t('common.none')}</option>
              {machines.map((m) => <option key={m.id} value={m.id}>{m.code} - {m.name}</option>)}
            </Select>
          </FormField>
        )}
        <FormField label={t('mfg.reasonDialog.note')}><Textarea rows={2} maxLength={500} value={note} onChange={(e) => setNote(e.target.value)} /></FormField>
      </div>
    </Modal>
  )
}
