import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Crew, type DowntimeCategory, type Machine, type MachineStatus, type ReasonCode, type StandardOperation } from '@/api/manufacturing/mfg.api'
import { employeesApi } from '@/api/masterdata/employees.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { TabList } from '@/components/shared/PageTabs'
import { ActiveBadge, MachineBadge, NoPlant, PageHeader, PlantSelector, fmtQty, todayIso, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Button, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const REASON_TYPES = ['Scrap', 'Rework', 'Downtime', 'Cancel', 'Hold', 'ShortClose', 'UnplannedMaterial', 'OverIssue']
type TabKey = 'machines' | 'operations' | 'reasons' | 'crews'

/** The delete / deactivate / activate trio every catalog row has; a delete that is refused because the row is used says "deactivate instead" (BR-GEN-02). */
function useRowActions(invalidate: () => void, setActive: (id: string, active: boolean) => Promise<unknown>, remove: (id: string) => Promise<unknown>) {
  const { t } = useTranslation()
  const toast = useToast()
  const [deleting, setDeleting] = useState<{ id: string; code: string } | null>(null)
  const toggle = useMutation({ mutationFn: (v: { id: string; active: boolean }) => setActive(v.id, v.active), onSuccess: invalidate, onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))) })
  const del = useMutation({
    mutationFn: (id: string) => remove(id),
    onSuccess: () => { invalidate(); setDeleting(null) },
    onError: (e) => { toast.error(`${serverMessage(e, t('common.failedToSave'))} ${t('mfg.deactivateInstead')}`); setDeleting(null) },
  })
  const confirm = (
    <ConfirmDialog open={!!deleting} danger title={t('common.delete')} message={t('mfg.deleteAsk', { code: deleting?.code ?? '' })} onConfirm={() => deleting && del.mutate(deleting.id)} onCancel={() => setDeleting(null)} />
  )
  return { toggle, askDelete: (id: string, code: string) => setDeleting({ id, code }), confirm }
}

function RowButtons({ active, onEdit, onToggle, onDelete }: { active: boolean; onEdit: () => void; onToggle: () => void; onDelete: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="flex gap-2">
      <Button variant="link" size="sm" onClick={onEdit}>{t('common.edit')}</Button>
      <Button variant="link" size="sm" onClick={onToggle}>{active ? t('common.deactivate') : t('common.activate')}</Button>
      <Button variant="link-danger" size="sm" onClick={onDelete}>{t('common.delete')}</Button>
    </div>
  )
}

function FormModal({ id, title, onClose, onSubmit, pending, error, children }: { id: string; title: string; onClose: () => void; onSubmit: () => void; pending: boolean; error: string | null; children: React.ReactNode }) {
  const { t } = useTranslation()
  return (
    <Modal open size="md" title={title} onClose={onClose} closeOnEsc={!pending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form={id} loading={pending}>{t('common.save')}</Button></>}>
      <form id={id} onSubmit={(e) => { e.preventDefault(); onSubmit() }} className="space-y-4">{error && <Alert variant="danger">{error}</Alert>}{children}</form>
    </Modal>
  )
}

// ── Machines ──────────────────────────────────────────────────────────────────────────────────────────────
interface MachineForm { code: string; name: string; workCenterId: string; idealCycleTimeSeconds: number; serialNo: string }

function MachinesPanel({ plantId, canManage }: { plantId: string; canManage: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Machine | 'new' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { data: machines = [], isLoading } = useQuery({ queryKey: ['mfg', 'machines', plantId], queryFn: () => mfgApi.listMachines(plantId) })
  const { data: centers = [] } = useQuery({ queryKey: ['mfg', 'work-centers', plantId], queryFn: () => mfgApi.listWorkCenters(plantId) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['mfg', 'machines'] })
  const rows = useRowActions(refresh, mfgApi.setMachineActive, mfgApi.deleteMachine)
  const status = useMutation({ mutationFn: (v: { id: string; status: MachineStatus }) => mfgApi.setMachineStatus(v.id, v.status), onSuccess: refresh, onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))) })
  const m = editing && editing !== 'new' ? editing : null
  const { register, handleSubmit } = useForm<MachineForm>({ values: { code: m?.code ?? '', name: m?.name ?? '', workCenterId: m?.workCenterId ?? '', idealCycleTimeSeconds: m?.idealCycleTimeSeconds ?? 60, serialNo: m?.serialNo ?? '' } })
  const save = useMutation({
    mutationFn: (f: MachineForm) => {
      const body = { name: f.name, workCenterId: f.workCenterId, idealCycleTimeSeconds: Number(f.idealCycleTimeSeconds), serialNo: f.serialNo || null }
      return m ? mfgApi.updateMachine(m.id, body) : mfgApi.createMachine(plantId, { code: f.code, ...body })
    },
    onSuccess: () => { refresh(); toast.success(t('common.saved')); setEditing(null) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const centerName = (id: string) => centers.find((c) => c.id === id)?.code ?? id.slice(0, 8)

  return (
    <div className="space-y-3">
      {canManage && <div className="flex justify-end"><Button onClick={() => { setEditing('new'); setError(null) }}>{t('mfg.catalogs.addMachine')}</Button></div>}
      <DataTable
        data={machines}
        isLoading={isLoading}
        emptyMessage={t('mfg.catalogs.noMachines')}
        columns={[
          { key: 'code', label: t('common.code'), render: (x: Machine) => <span className="font-mono text-xs">{x.code}</span> },
          { key: 'name', label: t('common.name') },
          { key: 'wc', label: t('mfg.catalogs.workCenter'), render: (x: Machine) => centerName(x.workCenterId) },
          { key: 'cycle', label: t('mfg.catalogs.idealCycle'), align: 'right' as const, render: (x: Machine) => fmtQty(x.idealCycleTimeSeconds, 2) },
          {
            key: 'status', label: t('common.status'),
            render: (x: Machine) => canManage
              ? <Select className="w-44" aria-label={`${t('common.status')} ${x.code}`} value={x.status} onChange={(e) => status.mutate({ id: x.id, status: e.target.value as MachineStatus })}>{['Active', 'UnderMaintenance', 'Retired'].map((s) => <option key={s} value={s}>{t(`mfg.machineStatus.${s}`)}</option>)}</Select>
              : <MachineBadge status={x.status} />,
          },
          { key: 'isActive', label: t('mfg.catalogs.enabled'), render: (x: Machine) => <ActiveBadge active={x.isActive} /> },
          { key: 'actions', label: t('common.actions'), render: (x: Machine) => canManage && <RowButtons active={x.isActive} onEdit={() => { setEditing(x); setError(null) }} onToggle={() => rows.toggle.mutate({ id: x.id, active: !x.isActive })} onDelete={() => rows.askDelete(x.id, x.code)} /> },
        ]}
      />
      {editing && (
        <FormModal id="machine-form" title={m ? t('mfg.catalogs.editMachine') : t('mfg.catalogs.addMachine')} onClose={() => setEditing(null)} onSubmit={handleSubmit((f) => save.mutate(f))} pending={save.isPending} error={error}>
          {!m && <FormField label={t('common.code')} required><Input {...register('code', { required: true })} /></FormField>}
          <FormField label={t('common.name')} required><Input {...register('name', { required: true })} /></FormField>
          <FormField label={t('mfg.catalogs.workCenter')} required>
            <Select {...register('workCenterId', { required: true })}><option value="">{t('common.select')}</option>{centers.map((c) => <option key={c.id} value={c.id}>{c.code} - {c.values.name}</option>)}</Select>
          </FormField>
          <FormField label={t('mfg.catalogs.idealCycle')} hint={t('mfg.catalogs.idealCycleHint')} required><Input type="number" step="any" min={0.01} {...register('idealCycleTimeSeconds', { required: true, min: 0.01 })} /></FormField>
          <FormField label={t('mfg.catalogs.serialNo')}><Input {...register('serialNo')} /></FormField>
        </FormModal>
      )}
      {rows.confirm}
    </div>
  )
}

// ── Standard operations ───────────────────────────────────────────────────────────────────────────────────
interface OpForm { code: string; name: string; defaultWorkCenterId: string; setupTimeMinutes: number; runTimeMinutes: number; runTimeBasis: 'PerUnit' | 'PerBatch'; batchSize: string; instructions: string }

function OperationsPanel({ plantId, canManage }: { plantId: string; canManage: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<StandardOperation | 'new' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { data: ops = [], isLoading } = useQuery({ queryKey: ['mfg', 'std-ops', plantId], queryFn: () => mfgApi.listStandardOperations(plantId) })
  const { data: centers = [] } = useQuery({ queryKey: ['mfg', 'work-centers', plantId], queryFn: () => mfgApi.listWorkCenters(plantId) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['mfg', 'std-ops'] })
  const rows = useRowActions(refresh, mfgApi.setStandardOperationActive, mfgApi.deleteStandardOperation)
  const o = editing && editing !== 'new' ? editing : null
  const { register, handleSubmit, watch } = useForm<OpForm>({
    values: { code: o?.code ?? '', name: o?.name ?? '', defaultWorkCenterId: o?.defaultWorkCenterId ?? '', setupTimeMinutes: o?.setupTimeMinutes ?? 0, runTimeMinutes: o?.runTimeMinutes ?? 1, runTimeBasis: o?.runTimeBasis ?? 'PerUnit', batchSize: o?.batchSize != null ? String(o.batchSize) : '', instructions: o?.instructions ?? '' },
  })
  const basis = watch('runTimeBasis')
  const save = useMutation({
    mutationFn: (f: OpForm) => {
      const body = { name: f.name, defaultWorkCenterId: f.defaultWorkCenterId, setupTimeMinutes: Number(f.setupTimeMinutes), runTimeMinutes: Number(f.runTimeMinutes), runTimeBasis: f.runTimeBasis, batchSize: f.runTimeBasis === 'PerBatch' && f.batchSize ? Number(f.batchSize) : null, instructions: f.instructions || null }
      return o ? mfgApi.updateStandardOperation(o.id, body) : mfgApi.createStandardOperation(plantId, f.code, body)
    },
    onSuccess: () => { refresh(); toast.success(t('common.saved')); setEditing(null) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <div className="space-y-3">
      {canManage && <div className="flex justify-end"><Button onClick={() => { setEditing('new'); setError(null) }}>{t('mfg.catalogs.addOperation')}</Button></div>}
      <DataTable
        data={ops}
        isLoading={isLoading}
        emptyMessage={t('mfg.catalogs.noOperations')}
        columns={[
          { key: 'code', label: t('common.code'), render: (x: StandardOperation) => <span className="font-mono text-xs">{x.code}</span> },
          { key: 'name', label: t('common.name') },
          { key: 'wc', label: t('mfg.catalogs.workCenter'), render: (x: StandardOperation) => centers.find((c) => c.id === x.defaultWorkCenterId)?.code ?? '' },
          { key: 'setup', label: t('mfg.routing.setup'), align: 'right' as const, render: (x: StandardOperation) => fmtQty(x.setupTimeMinutes, 2) },
          { key: 'run', label: t('mfg.routing.run'), align: 'right' as const, render: (x: StandardOperation) => `${fmtQty(x.runTimeMinutes, 4)} / ${t(`mfg.routing.${x.runTimeBasis}`)}` },
          { key: 'isActive', label: t('common.status'), render: (x: StandardOperation) => <ActiveBadge active={x.isActive} /> },
          { key: 'actions', label: t('common.actions'), render: (x: StandardOperation) => canManage && <RowButtons active={x.isActive} onEdit={() => { setEditing(x); setError(null) }} onToggle={() => rows.toggle.mutate({ id: x.id, active: !x.isActive })} onDelete={() => rows.askDelete(x.id, x.code)} /> },
        ]}
      />
      {editing && (
        <FormModal id="op-form" title={o ? t('mfg.catalogs.editOperation') : t('mfg.catalogs.addOperation')} onClose={() => setEditing(null)} onSubmit={handleSubmit((f) => save.mutate(f))} pending={save.isPending} error={error}>
          {!o && <FormField label={t('common.code')} required><Input {...register('code', { required: true })} /></FormField>}
          <FormField label={t('common.name')} required><Input {...register('name', { required: true })} /></FormField>
          <FormField label={t('mfg.catalogs.workCenter')} required>
            <Select {...register('defaultWorkCenterId', { required: true })}><option value="">{t('common.select')}</option>{centers.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.code} - {c.values.name}</option>)}</Select>
          </FormField>
          <div className="grid grid-cols-3 gap-3">
            <FormField label={t('mfg.routing.setup')}><Input type="number" step="any" min={0} {...register('setupTimeMinutes', { required: true, min: 0 })} /></FormField>
            <FormField label={t('mfg.routing.run')}><Input type="number" step="any" min={0.0001} {...register('runTimeMinutes', { required: true, min: 0.0001 })} /></FormField>
            <FormField label={t('mfg.routing.basis')}><Select {...register('runTimeBasis')}><option value="PerUnit">{t('mfg.routing.PerUnit')}</option><option value="PerBatch">{t('mfg.routing.PerBatch')}</option></Select></FormField>
          </div>
          {basis === 'PerBatch' && <FormField label={t('mfg.routing.batchSize')} required><Input type="number" step="any" min={0.000001} {...register('batchSize', { required: true })} /></FormField>}
          <FormField label={t('mfg.routing.instructions')}><Textarea {...register('instructions')} /></FormField>
        </FormModal>
      )}
      {rows.confirm}
    </div>
  )
}

// ── Reason codes ──────────────────────────────────────────────────────────────────────────────────────────
interface ReasonForm { type: string; code: string; name: string; category: string }

function ReasonsPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<ReasonCode | 'new' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { data: reasons = [], isLoading } = useQuery({ queryKey: ['mfg', 'reasons'], queryFn: () => mfgApi.listReasons() })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['mfg', 'reasons'] })
  const rows = useRowActions(refresh, mfgApi.setReasonActive, mfgApi.deleteReason)
  const seed = useMutation({ mutationFn: () => mfgApi.seedReasons(), onSuccess: (r) => { refresh(); toast.success(t('mfg.catalogs.seeded', { count: r.added })) }, onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))) })
  const r0 = editing && editing !== 'new' ? editing : null
  const { register, handleSubmit, watch } = useForm<ReasonForm>({ values: { type: r0?.type ?? 'Scrap', code: r0?.code ?? '', name: r0?.name ?? '', category: r0?.category ?? '' } })
  const type = watch('type')
  const save = useMutation({
    mutationFn: (f: ReasonForm) => {
      const category = f.type === 'Downtime' ? (f.category as DowntimeCategory) || null : null
      return r0 ? mfgApi.updateReason(r0.id, { name: f.name, category }) : mfgApi.createReason({ type: f.type, code: f.code, name: f.name, category })
    },
    onSuccess: () => { refresh(); toast.success(t('common.saved')); setEditing(null) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <div className="space-y-3">
      {canManage && (
        <div className="flex justify-end gap-2">
          <Button variant="outline" loading={seed.isPending} onClick={() => seed.mutate()}>{t('mfg.catalogs.seedDefaults')}</Button>
          <Button onClick={() => { setEditing('new'); setError(null) }}>{t('mfg.catalogs.addReason')}</Button>
        </div>
      )}
      <DataTable
        data={reasons}
        isLoading={isLoading}
        emptyMessage={t('mfg.catalogs.noReasons')}
        columns={[
          { key: 'type', label: t('mfg.catalogs.reasonType'), render: (x: ReasonCode) => t(`mfg.reason.${x.type}`, x.type) },
          { key: 'code', label: t('common.code'), render: (x: ReasonCode) => <span className="font-mono text-xs">{x.code}</span> },
          { key: 'name', label: t('common.name') },
          { key: 'category', label: t('mfg.catalogs.category'), render: (x: ReasonCode) => (x.category ? t(`mfg.catalogs.${x.category}`) : '') },
          { key: 'isActive', label: t('common.status'), render: (x: ReasonCode) => <ActiveBadge active={x.isActive} /> },
          { key: 'actions', label: t('common.actions'), render: (x: ReasonCode) => canManage && <RowButtons active={x.isActive} onEdit={() => { setEditing(x); setError(null) }} onToggle={() => rows.toggle.mutate({ id: x.id, active: !x.isActive })} onDelete={() => rows.askDelete(x.id, x.code)} /> },
        ]}
      />
      {editing && (
        <FormModal id="reason-form" title={r0 ? t('mfg.catalogs.editReason') : t('mfg.catalogs.addReason')} onClose={() => setEditing(null)} onSubmit={handleSubmit((f) => save.mutate(f))} pending={save.isPending} error={error}>
          {!r0 && (
            <>
              <FormField label={t('mfg.catalogs.reasonType')}><Select {...register('type')}>{REASON_TYPES.map((x) => <option key={x} value={x}>{t(`mfg.reason.${x}`)}</option>)}</Select></FormField>
              <FormField label={t('common.code')} required><Input {...register('code', { required: true })} /></FormField>
            </>
          )}
          <FormField label={t('common.name')} required><Input {...register('name', { required: true })} /></FormField>
          {(r0 ? r0.type === 'Downtime' : type === 'Downtime') && (
            <FormField label={t('mfg.catalogs.category')} hint={t('mfg.catalogs.categoryHint')} required>
              <Select {...register('category', { required: true })}><option value="">{t('common.select')}</option><option value="Planned">{t('mfg.catalogs.Planned')}</option><option value="Unplanned">{t('mfg.catalogs.Unplanned')}</option></Select>
            </FormField>
          )}
        </FormModal>
      )}
      {rows.confirm}
    </div>
  )
}

// ── Crews ─────────────────────────────────────────────────────────────────────────────────────────────────
interface CrewForm { code: string; name: string; workCenterId: string }

function CrewMembers({ crew, canManage, onClose }: { crew: Crew; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [employeeId, setEmployeeId] = useState('')
  const [validFrom, setValidFrom] = useState(todayIso())
  const [rate, setRate] = useState('')
  const { data: employees } = useQuery({ queryKey: ['employees', 'for-crews'], queryFn: () => employeesApi.list({ page: 1, pageSize: 100 } as never) })
  const { data: fresh } = useQuery({ queryKey: ['mfg', 'crews', 'one', crew.id], queryFn: async () => (await mfgApi.listCrews(crew.plantId)).find((c) => c.id === crew.id) ?? crew, initialData: crew })
  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'crews'] }) }
  const add = useMutation({
    mutationFn: () => mfgApi.addCrewMember(crew.id, { employeeId, validFrom, laborRatePerHour: rate ? Number(rate) : null }),
    onSuccess: () => { setError(null); setEmployeeId(''); setRate(''); refresh() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const remove = useMutation({ mutationFn: (id: string) => mfgApi.removeCrewMember(crew.id, id), onSuccess: refresh, onError: (e) => setError(serverMessage(e, t('common.failedToSave'))) })
  const members = (fresh ?? crew).members
  return (
    <Modal open size="lg" title={t('mfg.catalogs.membersOf', { code: crew.code })} onClose={onClose} footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}>
      <div className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        {canManage && (
          <div className="flex flex-wrap items-end gap-2">
            <FormField label={t('mfg.catalogs.employee')}>
              <Select className="w-64" value={employeeId} onChange={(e) => setEmployeeId(e.target.value)}>
                <option value="">{t('common.select')}</option>
                {(employees?.items ?? []).map((e) => <option key={e.id} value={e.id}>{e.employeeNumber} - {e.fullName}</option>)}
              </Select>
            </FormField>
            <FormField label={t('mfg.catalogs.validFrom')}><Input type="date" className="w-40" value={validFrom} onChange={(e) => setValidFrom(e.target.value)} /></FormField>
            <FormField label={t('mfg.catalogs.ownRate')} hint={t('mfg.catalogs.ownRateHint')}><Input type="number" min={0} className="w-32" value={rate} onChange={(e) => setRate(e.target.value)} /></FormField>
            <Button disabled={!employeeId} loading={add.isPending} onClick={() => add.mutate()}>{t('common.add')}</Button>
          </div>
        )}
        <DataTable
          data={members}
          emptyMessage={t('mfg.catalogs.noMembers')}
          columns={[
            { key: 'emp', label: t('mfg.catalogs.employee'), render: (m) => `${m.employeeNumber ?? ''} ${m.employeeName ?? m.employeeId.slice(0, 8)}` },
            { key: 'validFrom', label: t('mfg.catalogs.validFrom') },
            { key: 'validTo', label: t('mfg.catalogs.validTo'), render: (m) => m.validTo ?? '' },
            { key: 'rate', label: t('mfg.catalogs.ownRate'), align: 'right' as const, render: (m) => fmtQty(m.laborRatePerHour, 2) },
            { key: 'actions', label: t('common.actions'), render: (m) => canManage && <Button variant="link-danger" size="sm" onClick={() => remove.mutate(m.id)}>{t('common.delete')}</Button> },
          ]}
        />
      </div>
    </Modal>
  )
}

function CrewsPanel({ plantId, canManage }: { plantId: string; canManage: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<Crew | 'new' | null>(null)
  const [members, setMembers] = useState<Crew | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { data: crews = [], isLoading } = useQuery({ queryKey: ['mfg', 'crews', plantId], queryFn: () => mfgApi.listCrews(plantId) })
  const { data: centers = [] } = useQuery({ queryKey: ['mfg', 'work-centers', plantId], queryFn: () => mfgApi.listWorkCenters(plantId) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['mfg', 'crews'] })
  const rows = useRowActions(refresh, mfgApi.setCrewActive, mfgApi.deleteCrew)
  const c0 = editing && editing !== 'new' ? editing : null
  const { register, handleSubmit } = useForm<CrewForm>({ values: { code: c0?.code ?? '', name: c0?.name ?? '', workCenterId: c0?.workCenterId ?? '' } })
  const save = useMutation({
    mutationFn: (f: CrewForm) => {
      const body = { name: f.name, workCenterId: f.workCenterId || null, leaderEmployeeId: c0?.leaderEmployeeId ?? null }
      return c0 ? mfgApi.updateCrew(c0.id, body) : mfgApi.createCrew(plantId, { code: f.code, ...body })
    },
    onSuccess: () => { refresh(); toast.success(t('common.saved')); setEditing(null) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <div className="space-y-3">
      {canManage && <div className="flex justify-end"><Button onClick={() => { setEditing('new'); setError(null) }}>{t('mfg.catalogs.addCrew')}</Button></div>}
      <DataTable
        data={crews}
        isLoading={isLoading}
        emptyMessage={t('mfg.catalogs.noCrews')}
        columns={[
          { key: 'code', label: t('common.code'), render: (x: Crew) => <span className="font-mono text-xs">{x.code}</span> },
          { key: 'name', label: t('common.name') },
          { key: 'wc', label: t('mfg.catalogs.workCenter'), render: (x: Crew) => centers.find((c) => c.id === x.workCenterId)?.code ?? '' },
          { key: 'members', label: t('mfg.catalogs.members'), align: 'right' as const, render: (x: Crew) => x.members.length },
          { key: 'isActive', label: t('common.status'), render: (x: Crew) => <ActiveBadge active={x.isActive} /> },
          {
            key: 'actions', label: t('common.actions'),
            render: (x: Crew) => (
              <div className="flex gap-2">
                <Button variant="link" size="sm" onClick={() => setMembers(x)}>{t('mfg.catalogs.members')}</Button>
                {canManage && <RowButtons active={x.isActive} onEdit={() => { setEditing(x); setError(null) }} onToggle={() => rows.toggle.mutate({ id: x.id, active: !x.isActive })} onDelete={() => rows.askDelete(x.id, x.code)} />}
              </div>
            ),
          },
        ]}
      />
      {editing && (
        <FormModal id="crew-form" title={c0 ? t('mfg.catalogs.editCrew') : t('mfg.catalogs.addCrew')} onClose={() => setEditing(null)} onSubmit={handleSubmit((f) => save.mutate(f))} pending={save.isPending} error={error}>
          {!c0 && <FormField label={t('common.code')} required><Input {...register('code', { required: true })} /></FormField>}
          <FormField label={t('common.name')} required><Input {...register('name', { required: true })} /></FormField>
          <FormField label={t('mfg.catalogs.workCenter')}><Select {...register('workCenterId')}><option value="">{t('common.none')}</option>{centers.map((c) => <option key={c.id} value={c.id}>{c.code} - {c.values.name}</option>)}</Select></FormField>
        </FormModal>
      )}
      {members && <CrewMembers crew={members} canManage={canManage} onClose={() => setMembers(null)} />}
      {rows.confirm}
    </div>
  )
}

/** S06: machines, standard operations, reason codes and crews (BA §5.6-5.7). */
export function CatalogsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:masterdata:manage')
  const [tab, setTab] = useState<TabKey>('machines')
  const { plantId, isLoading } = usePlantContext()
  if (!isLoading && !plantId) return <div><PageHeader title={t('mfg.nav.catalogs')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.catalogs')} hint={t('mfg.catalogs.hint')} actions={tab !== 'reasons' ? <PlantSelector /> : undefined} />
      <TabList id="mfg-cat" label={t('mfg.nav.catalogs')} current={tab} onSelect={setTab}
        tabs={[{ key: 'machines', label: t('mfg.catalogs.machines') }, { key: 'operations', label: t('mfg.catalogs.operations') }, { key: 'reasons', label: t('mfg.catalogs.reasons') }, { key: 'crews', label: t('mfg.catalogs.crews') }]} />
      <div className="pt-4" role="tabpanel">
        {tab === 'reasons' ? <ReasonsPanel canManage={canManage} /> : plantId && (
          tab === 'machines' ? <MachinesPanel plantId={plantId} canManage={canManage} /> : tab === 'operations' ? <OperationsPanel plantId={plantId} canManage={canManage} /> : <CrewsPanel plantId={plantId} canManage={canManage} />
        )}
      </div>
    </div>
  )
}
