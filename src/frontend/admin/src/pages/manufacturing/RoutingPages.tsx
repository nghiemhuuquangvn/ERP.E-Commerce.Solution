import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { GripVertical, MoveDown, MoveUp } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Routing, type RoutingListItem, type RoutingOperation, type RoutingOperationBody, type RunTimeBasis, type VersionStatus } from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { TabList } from '@/components/shared/PageTabs'
import { CsvImportDialog, NoPlant, PageHeader, PlantSelector, VersionBadge, fmtQty, todayIso, usePlantContext } from '@/components/manufacturing/shared'
import { reorderSeqs } from '@/components/manufacturing/reorder'
import { Alert, Badge, Button, Checkbox, FormField, Input, Modal, Select, Skeleton, Textarea, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

// ── List ──────────────────────────────────────────────────────────────────────────────────────────────────
export function RoutingListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:routing:manage')
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [status, setStatus] = useState<VersionStatus | ''>('')
  const [search, setSearch] = useState('')
  const [picker, setPicker] = useState<'new' | 'import' | null>(null)
  const [importItem, setImportItem] = useState<string | null>(null)
  const { data: routings = [], isLoading } = useQuery({ queryKey: ['mfg', 'routings', plantId, status], queryFn: () => mfgApi.listRoutings(plantId!, undefined, status || undefined), enabled: !!plantId })
  const create = useMutation({
    mutationFn: (itemId: string) => mfgApi.createRouting({ plantId: plantId!, itemId, effectiveFrom: todayIso() }),
    onSuccess: (r) => navigate(`/manufacturing/routings/${r.id}`),
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  const rows = routings.filter((r) => !search || `${r.itemCode} ${r.itemName}`.toLowerCase().includes(search.toLowerCase()))

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.routings')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader
        title={t('mfg.nav.routings')}
        hint={t('mfg.routing.hint')}
        actions={<><PlantSelector />{canManage && <><Button variant="outline" onClick={() => setPicker('import')}>{t('mfg.import.openRouting')}</Button><Button onClick={() => setPicker('new')}>{t('mfg.routing.new')}</Button></>}</>}
      />
      <div className="mb-3 flex flex-wrap gap-3">
        <div className="w-72"><Input type="search" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} /></div>
        <Select className="w-44" aria-label={t('common.status')} value={status} onChange={(e) => setStatus(e.target.value as VersionStatus | '')}>
          <option value="">{t('common.all')}</option>
          {['Draft', 'Approved', 'Obsolete'].map((s) => <option key={s} value={s}>{t(`mfg.status.${s}`)}</option>)}
        </Select>
      </div>
      <DataTable
        data={rows}
        isLoading={isLoading}
        emptyMessage={t('mfg.routing.empty')}
        columns={[
          { key: 'itemCode', label: t('common.code'), render: (r: RoutingListItem) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/routings/${r.id}`}>{r.itemCode}</Link> },
          { key: 'itemName', label: t('common.name') },
          { key: 'version', label: t('mfg.bom.version'), align: 'right' as const, render: (r: RoutingListItem) => `v${r.version}` },
          { key: 'status', label: t('common.status'), render: (r: RoutingListItem) => <VersionBadge status={r.status} /> },
          { key: 'valid', label: t('mfg.bom.validity'), render: (r: RoutingListItem) => `${r.effectiveFrom} → ${r.effectiveTo ?? '…'}` },
          { key: 'operationCount', label: t('mfg.routing.operations'), align: 'right' as const },
        ]}
      />
      <ProductPickerModal open={picker !== null} multiple={false} onClose={() => setPicker(null)} onConfirm={(p) => { const kind = picker; setPicker(null); if (!p[0]) return; if (kind === 'new') create.mutate(p[0].id); else setImportItem(p[0].id) }} />
      {importItem && plantId && <CsvImportDialog open kind="routing" plantId={plantId} itemId={importItem} effectiveFrom={todayIso()} onClose={() => setImportItem(null)} onImported={(id) => { setImportItem(null); navigate(`/manufacturing/routings/${id}`) }} />}
    </div>
  )
}

// ── Operation modal ───────────────────────────────────────────────────────────────────────────────────────
interface OpForm {
  standardOperationId: string; operationSeq: string; name: string; workCenterId: string; setupTimeMinutes: string; runTimeMinutes: string; runTimeBasis: RunTimeBasis
  batchSize: string; queueTimeMinutes: string; moveTimeMinutes: string; isSubcontract: boolean; instructions: string; ipqcInspectionPlanId: string
}
const num = (v: string) => (v === '' ? null : Number(v))

function OperationModal({ routing, op, onClose }: { routing: Routing; op: RoutingOperation | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const { data: centers = [] } = useQuery({ queryKey: ['mfg', 'work-centers', routing.plantId], queryFn: () => mfgApi.listWorkCenters(routing.plantId) })
  const { data: standards = [] } = useQuery({ queryKey: ['mfg', 'std-ops', routing.plantId], queryFn: () => mfgApi.listStandardOperations(routing.plantId) })
  // P4-3: the IPQC plans (Approved) of the item of this routing
  const { data: ipqcPlans = [] } = useQuery({ queryKey: ['mfg', 'inspection-plans', routing.plantId, 'Ipqc', routing.itemId], queryFn: () => mfgApi.listInspectionPlans(routing.plantId, { type: 'Ipqc', itemId: routing.itemId, status: 'Approved' }) })
  const { register, handleSubmit, watch, setValue, formState: { errors } } = useForm<OpForm>({
    defaultValues: {
      standardOperationId: op?.standardOperationId ?? '', operationSeq: op ? String(op.operationSeq) : '', name: op?.name ?? '', workCenterId: op?.workCenterId ?? '', setupTimeMinutes: String(op?.setupTimeMinutes ?? 0),
      runTimeMinutes: op ? String(op.runTimeMinutes) : '', runTimeBasis: op?.runTimeBasis ?? 'PerUnit', batchSize: op?.batchSize != null ? String(op.batchSize) : '', queueTimeMinutes: String(op?.queueTimeMinutes ?? 0),
      moveTimeMinutes: String(op?.moveTimeMinutes ?? 0), isSubcontract: op?.isSubcontract ?? false, instructions: op?.instructions ?? '', ipqcInspectionPlanId: op?.ipqcInspectionPlanId ?? '',
    },
  })
  const basis = watch('runTimeBasis')
  const fromStandard = (id: string) => {
    const s = standards.find((x) => x.id === id)
    if (!s) return
    setValue('name', s.name); setValue('workCenterId', s.defaultWorkCenterId); setValue('setupTimeMinutes', String(s.setupTimeMinutes)); setValue('runTimeMinutes', String(s.runTimeMinutes))
    setValue('runTimeBasis', s.runTimeBasis); setValue('batchSize', s.batchSize != null ? String(s.batchSize) : ''); setValue('instructions', s.instructions ?? '')
  }
  const save = useMutation({
    mutationFn: (f: OpForm) => {
      const body: RoutingOperationBody = {
        operationSeq: num(f.operationSeq), standardOperationId: f.standardOperationId || null, name: f.name, workCenterId: f.workCenterId, setupTimeMinutes: Number(f.setupTimeMinutes || 0), runTimeMinutes: Number(f.runTimeMinutes),
        runTimeBasis: f.runTimeBasis, batchSize: f.runTimeBasis === 'PerBatch' ? num(f.batchSize) : null, queueTimeMinutes: Number(f.queueTimeMinutes || 0), moveTimeMinutes: Number(f.moveTimeMinutes || 0),
        isSubcontract: f.isSubcontract, instructions: f.instructions || null, ipqcInspectionPlanId: f.ipqcInspectionPlanId || null,
      }
      return op ? mfgApi.updateOperation(routing.id, op.operationSeq, body) : mfgApi.addOperation(routing.id, body)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'routing', routing.id] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <Modal open size="lg" title={op ? t('mfg.routing.editOperation', { seq: op.operationSeq }) : t('mfg.routing.addOperation')} onClose={onClose} closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="op-form-r" loading={save.isPending}>{t('common.save')}</Button></>}>
      <form id="op-form-r" onSubmit={handleSubmit((f) => save.mutate(f))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        {!op && standards.length > 0 && (
          <FormField label={t('mfg.routing.fromStandard')} hint={t('mfg.routing.fromStandardHint')}>
            <Select {...register('standardOperationId', { onChange: (e) => fromStandard(e.target.value) })}><option value="">{t('common.none')}</option>{standards.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.code} - {s.name}</option>)}</Select>
          </FormField>
        )}
        <div className="grid gap-4 sm:grid-cols-4">
          <FormField label={t('mfg.routing.seq')} hint={t('mfg.routing.seqHint')}><Input type="number" min={1} {...register('operationSeq')} /></FormField>
          <FormField label={t('common.name')} className="sm:col-span-3"><Input {...register('name')} /></FormField>
          <FormField label={t('mfg.routing.workCenter')} required error={errors.workCenterId ? t('common.required') : undefined} className="sm:col-span-2">
            <Select {...register('workCenterId', { required: true })}><option value="">{t('common.select')}</option>{centers.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.code} - {c.values.name}</option>)}</Select>
          </FormField>
          <div className="flex items-end sm:col-span-2"><Checkbox label={t('mfg.routing.subcontract')} {...register('isSubcontract')} /></div>
          <FormField label={t('mfg.routing.setup')}><Input type="number" step="any" min={0} {...register('setupTimeMinutes')} /></FormField>
          <FormField label={t('mfg.routing.run')} required error={errors.runTimeMinutes ? t('common.required') : undefined}><Input type="number" step="any" min={0.0001} {...register('runTimeMinutes', { required: true })} /></FormField>
          <FormField label={t('mfg.routing.basis')}><Select {...register('runTimeBasis')}><option value="PerUnit">{t('mfg.routing.PerUnit')}</option><option value="PerBatch">{t('mfg.routing.PerBatch')}</option></Select></FormField>
          {basis === 'PerBatch' && <FormField label={t('mfg.routing.batchSize')} required error={errors.batchSize ? t('common.required') : undefined}><Input type="number" step="any" min={0.000001} {...register('batchSize', { required: true })} /></FormField>}
          <FormField label={t('mfg.qc.routing.ipqcPlan')} hint={t('mfg.qc.routing.ipqcPlanHint')} className="sm:col-span-4">
            <Select {...register('ipqcInspectionPlanId')}>
              <option value="">{t('common.none')}</option>
              {op?.ipqcInspectionPlanId && !ipqcPlans.some((p) => p.id === op.ipqcInspectionPlanId) && <option value={op.ipqcInspectionPlanId}>{t('mfg.qc.routing.currentPlan')}</option>}
              {ipqcPlans.map((p) => <option key={p.id} value={p.id}>{p.code} (v{p.version})</option>)}
            </Select>
          </FormField>
          <FormField label={t('mfg.routing.queue')}><Input type="number" min={0} {...register('queueTimeMinutes')} /></FormField>
          <FormField label={t('mfg.routing.move')}><Input type="number" min={0} {...register('moveTimeMinutes')} /></FormField>
        </div>
        <FormField label={t('mfg.routing.instructions')}><Textarea {...register('instructions')} /></FormField>
      </form>
    </Modal>
  )
}

// ── Operations grid with drag and drop ────────────────────────────────────────────────────────────────────
function OperationsGrid({ routing, canEdit }: { routing: Routing; canEdit: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<RoutingOperation | 'new' | null>(null)
  const [dragFrom, setDragFrom] = useState<number | null>(null)
  const [over, setOver] = useState<number | null>(null)
  const ops = routing.operations
  const key = ['mfg', 'routing', routing.id]
  const renumber = useMutation({
    mutationFn: (order: number[]) => mfgApi.renumber(routing.id, order),
    onSuccess: (r) => { queryClient.setQueryData(key, r); queryClient.invalidateQueries({ queryKey: ['mfg', 'routing-lead', routing.id] }); toast.success(t('mfg.routing.renumbered')) },
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  const remove = useMutation({
    mutationFn: (seq: number) => mfgApi.removeOperation(routing.id, seq),
    onSuccess: (r) => queryClient.setQueryData(key, r),
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  const move = (from: number, to: number) => {
    const order = reorderSeqs(ops.map((o) => o.operationSeq), from, to)
    if (order) renumber.mutate(order)
  }
  const drop = (to: number) => { if (dragFrom !== null) move(dragFrom, to); setDragFrom(null); setOver(null) }

  return (
    <div className="space-y-3">
      {canEdit && <div className="flex justify-end"><Button onClick={() => setEditing('new')}>{t('mfg.routing.addOperation')}</Button></div>}
      {canEdit && ops.length > 1 && <p className="text-xs text-ink-secondary">{t('mfg.routing.dragHint')}</p>}
      <div className="overflow-x-auto rounded-lg border border-line">
        <table className="min-w-full divide-y divide-line bg-surface text-sm">
          <caption className="sr-only">{t('mfg.routing.operations')}</caption>
          <thead className="bg-surface-muted text-left text-ink-secondary">
            <tr>
              {canEdit && <th scope="col" className="w-24 px-2 py-2"><span className="sr-only">{t('mfg.routing.order')}</span></th>}
              <th scope="col" className="px-3 py-2">#</th><th scope="col" className="px-3 py-2">{t('common.name')}</th><th scope="col" className="px-3 py-2">{t('mfg.routing.workCenter')}</th>
              <th scope="col" className="px-3 py-2 text-right">{t('mfg.routing.setup')}</th><th scope="col" className="px-3 py-2 text-right">{t('mfg.routing.run')}</th>
              <th scope="col" className="px-3 py-2 text-right">{t('mfg.routing.queueMove')}</th><th scope="col" className="px-3 py-2">{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {ops.length === 0 && <tr><td colSpan={8} className="px-3 py-6 text-center text-ink-secondary">{t('mfg.routing.noOperations')}</td></tr>}
            {ops.map((o, i) => (
              <tr
                key={o.operationSeq}
                data-testid={`op-row-${o.operationSeq}`}
                draggable={canEdit && ops.length > 1}
                onDragStart={(e) => { setDragFrom(i); if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move' }}
                onDragOver={(e) => { if (dragFrom !== null) { e.preventDefault(); setOver(i) } }}
                onDrop={(e) => { e.preventDefault(); drop(i) }}
                onDragEnd={() => { setDragFrom(null); setOver(null) }}
                className={`${over === i && dragFrom !== null && dragFrom !== i ? 'bg-brand-soft' : ''} ${dragFrom === i ? 'opacity-50' : ''}`}
              >
                {canEdit && (
                  <td className="px-2 py-2">
                    <div className="flex items-center gap-1">
                      <GripVertical size={16} aria-hidden="true" className="cursor-grab text-ink-muted" />
                      <Button variant="ghost" size="sm" iconOnly aria-label={t('mfg.routing.moveUp', { seq: o.operationSeq })} disabled={i === 0 || renumber.isPending} onClick={() => move(i, i - 1)}><MoveUp size={14} aria-hidden="true" /></Button>
                      <Button variant="ghost" size="sm" iconOnly aria-label={t('mfg.routing.moveDown', { seq: o.operationSeq })} disabled={i === ops.length - 1 || renumber.isPending} onClick={() => move(i, i + 1)}><MoveDown size={14} aria-hidden="true" /></Button>
                    </div>
                  </td>
                )}
                <th scope="row" className="px-3 py-2 text-left font-mono text-xs">{o.operationSeq}</th>
                <td className="px-3 py-2">{o.name} {o.isSubcontract && <Badge variant="info">{t('mfg.routing.subcontractShort')}</Badge>}</td>
                <td className="px-3 py-2 font-mono text-xs">{o.workCenterCode}</td>
                <td className="px-3 py-2 text-right">{fmtQty(o.setupTimeMinutes, 2)}</td>
                <td className="px-3 py-2 text-right">{fmtQty(o.runTimeMinutes, 4)} / {t(`mfg.routing.${o.runTimeBasis}`)}{o.runTimeBasis === 'PerBatch' ? ` (${fmtQty(o.batchSize)})` : ''}</td>
                <td className="px-3 py-2 text-right">{o.queueTimeMinutes} / {o.moveTimeMinutes}</td>
                <td className="px-3 py-2">{canEdit && <div className="flex gap-2"><Button variant="link" size="sm" onClick={() => setEditing(o)}>{t('common.edit')}</Button><Button variant="link-danger" size="sm" onClick={() => remove.mutate(o.operationSeq)}>{t('common.delete')}</Button></div>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {editing && <OperationModal routing={routing} op={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

function LeadTimePanel({ routing }: { routing: Routing }) {
  const { t } = useTranslation()
  const [qty, setQty] = useState('100')
  const [end, setEnd] = useState(todayIso())
  const q = Number(qty)
  const { data, isLoading, error } = useQuery({ queryKey: ['mfg', 'routing-lead', routing.id, q, end, routing.operations.length], queryFn: () => mfgApi.leadTime(routing.id, q, end), enabled: q > 0 && !!end && routing.operations.length > 0, retry: false })
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-3">
        <FormField label={t('mfg.bom.orderQuantity')}><Input type="number" min={0} step="any" className="w-32" value={qty} onChange={(e) => setQty(e.target.value)} /></FormField>
        <FormField label={t('mfg.routing.endDate')} hint={t('mfg.routing.endDateHint')}><Input type="date" className="w-44" value={end} onChange={(e) => setEnd(e.target.value)} /></FormField>
      </div>
      {error && <Alert variant="danger">{serverMessage(error, t('mfg.routing.leadFailed'))}</Alert>}
      {isLoading && <Skeleton className="h-24 w-full" />}
      {data && (
        <>
          <div className="grid gap-3 sm:grid-cols-3" role="status">
            <div className="rounded-lg border border-line bg-surface p-3"><div className="text-xs text-ink-secondary">{t('mfg.routing.totalMinutes')}</div><div className="text-2xl font-semibold">{fmtQty(data.totalMinutes, 0)}</div></div>
            <div className="rounded-lg border border-line bg-surface p-3"><div className="text-xs text-ink-secondary">{t('mfg.routing.startDate')}</div><div className="text-2xl font-semibold">{data.startDate}</div></div>
            <div className="rounded-lg border border-line bg-surface p-3"><div className="text-xs text-ink-secondary">{t('mfg.routing.startAt')}</div><div className="text-2xl font-semibold">{data.startTime.slice(11, 16)}</div></div>
          </div>
          <DataTable
            data={data.operations.map((o) => ({ id: String(o.operationSeq), ...o }))}
            columns={[
              { key: 'operationSeq', label: '#', render: (o) => <span className="font-mono text-xs">{o.operationSeq}</span> },
              { key: 'name', label: t('common.name'), render: (o) => `${o.name} (${o.workCenterCode})` },
              { key: 'standardMinutes', label: t('mfg.routing.standardMinutes'), align: 'right' as const, render: (o) => fmtQty(o.standardMinutes, 2) },
              { key: 'calendarMinutes', label: t('mfg.routing.calendarMinutes'), align: 'right' as const },
              { key: 'start', label: t('mfg.routing.starts'), render: (o) => o.startTime.slice(0, 16).replace('T', ' ') },
              { key: 'end', label: t('mfg.routing.ends'), render: (o) => o.endTime.slice(0, 16).replace('T', ' ') },
            ]}
          />
        </>
      )}
    </div>
  )
}

function VersionsPanel({ routing }: { routing: Routing }) {
  const { t } = useTranslation()
  const { data: versions = [] } = useQuery({ queryKey: ['mfg', 'routings', routing.plantId, routing.itemId], queryFn: () => mfgApi.listRoutings(routing.plantId, routing.itemId) })
  const others = versions.filter((v) => v.id !== routing.id)
  const [otherId, setOtherId] = useState('')
  const right = otherId || others[0]?.id || ''
  const { data: other } = useQuery({ queryKey: ['mfg', 'routing', right], queryFn: () => mfgApi.getRouting(right), enabled: !!right })
  const diff = other ? compareOperations(routing.operations, other.operations) : []
  return (
    <div className="space-y-4">
      <DataTable
        data={versions.slice().sort((a, b) => b.version - a.version)}
        columns={[
          { key: 'version', label: t('mfg.bom.version'), render: (v: RoutingListItem) => <Link className="text-brand-action hover:underline" to={`/manufacturing/routings/${v.id}`}>v{v.version}{v.id === routing.id ? ` (${t('mfg.bom.thisOne')})` : ''}</Link> },
          { key: 'status', label: t('common.status'), render: (v: RoutingListItem) => <VersionBadge status={v.status} /> },
          { key: 'valid', label: t('mfg.bom.validity'), render: (v: RoutingListItem) => `${v.effectiveFrom} → ${v.effectiveTo ?? '…'}` },
          { key: 'operationCount', label: t('mfg.routing.operations'), align: 'right' as const },
        ]}
      />
      <section aria-labelledby="mfg-rt-compare">
        <div className="mb-2 flex flex-wrap items-end gap-3">
          <h3 id="mfg-rt-compare" className="text-sm font-semibold">{t('mfg.bom.compare', { version: routing.version })}</h3>
          {others.length > 0 && <Select className="w-48" aria-label={t('mfg.bom.compareWith')} value={right} onChange={(e) => setOtherId(e.target.value)}>{others.map((v) => <option key={v.id} value={v.id}>v{v.version} ({t(`mfg.status.${v.status}`)})</option>)}</Select>}
        </div>
        {others.length === 0 ? <p className="text-sm text-ink-secondary">{t('mfg.bom.noOtherVersion')}</p> : (
          <DataTable
            data={diff.map((d, i) => ({ id: String(i), ...d }))}
            emptyMessage={t('mfg.bom.noDifference')}
            columns={[
              { key: 'kind', label: t('mfg.bom.change'), render: (d) => <Badge variant={d.kind === 'Added' ? 'success' : d.kind === 'Removed' ? 'danger' : 'warning'}>{t(`mfg.bom.kind.${d.kind}`)}</Badge> },
              { key: 'seq', label: '#', render: (d) => <span className="font-mono text-xs">{d.seq}</span> },
              { key: 'name', label: t('common.name') },
              { key: 'detail', label: t('mfg.bom.detail') },
            ]}
          />
        )}
      </section>
    </div>
  )
}

/** Operation by operation (matched by number): added, removed, or changed in work center and times. */
export function compareOperations(left: RoutingOperation[], right: RoutingOperation[]) {
  const l = new Map(left.map((o) => [o.operationSeq, o]))
  const r = new Map(right.map((o) => [o.operationSeq, o]))
  const out: { kind: 'Added' | 'Removed' | 'Changed'; seq: number; name: string; detail: string }[] = []
  for (const [seq, a] of l) {
    const b = r.get(seq)
    if (!b) { out.push({ kind: 'Removed', seq, name: a.name, detail: '' }); continue }
    const changes: string[] = []
    if (a.workCenterCode !== b.workCenterCode) changes.push(`${a.workCenterCode} → ${b.workCenterCode}`)
    if (a.setupTimeMinutes !== b.setupTimeMinutes) changes.push(`setup ${a.setupTimeMinutes} → ${b.setupTimeMinutes}`)
    if (a.runTimeMinutes !== b.runTimeMinutes) changes.push(`run ${a.runTimeMinutes} → ${b.runTimeMinutes}`)
    if (a.runTimeBasis !== b.runTimeBasis || a.batchSize !== b.batchSize) changes.push(`${a.runTimeBasis}/${a.batchSize ?? ''} → ${b.runTimeBasis}/${b.batchSize ?? ''}`)
    if (a.queueTimeMinutes !== b.queueTimeMinutes || a.moveTimeMinutes !== b.moveTimeMinutes) changes.push(`queue/move ${a.queueTimeMinutes}/${a.moveTimeMinutes} → ${b.queueTimeMinutes}/${b.moveTimeMinutes}`)
    if (a.isSubcontract !== b.isSubcontract) changes.push('subcontract')
    if (changes.length) out.push({ kind: 'Changed', seq, name: a.name, detail: changes.join('; ') })
  }
  for (const [seq, b] of r) if (!l.has(seq)) out.push({ kind: 'Added', seq, name: b.name, detail: '' })
  return out.sort((x, y) => x.seq - y.seq)
}

// ── Editor ────────────────────────────────────────────────────────────────────────────────────────────────
type Action = 'approve' | 'obsolete' | 'copy' | 'delete'

export function RoutingEditorPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const [tab, setTab] = useState<'operations' | 'lead' | 'versions'>('operations')
  const [action, setAction] = useState<Action | null>(null)
  const [warnings, setWarnings] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const { data: routing, isLoading } = useQuery({ queryKey: ['mfg', 'routing', id], queryFn: () => mfgApi.getRouting(id) })
  const canManage = hasPermission('manufacturing:routing:manage')
  const canApprove = hasPermission('manufacturing:routing:approve')
  const draft = routing?.status === 'Draft'

  const run = useMutation({
    mutationFn: async (a: Action) => {
      if (a === 'approve') return { kind: a, routing: await mfgApi.approveRouting(id) }
      if (a === 'obsolete') return { kind: a, routing: await mfgApi.obsoleteRouting(id) }
      if (a === 'copy') return { kind: a, routing: await mfgApi.copyRouting(id) }
      await mfgApi.deleteRouting(id)
      return { kind: a, routing: null }
    },
    onSuccess: (r) => {
      setAction(null); setError(null)
      if (r.kind === 'delete') { navigate('/manufacturing/routings'); return }
      if (r.kind === 'copy' && r.routing) { toast.success(t('mfg.bom.copied', { version: r.routing.version })); navigate(`/manufacturing/routings/${r.routing.id}`); return }
      if (r.kind === 'approve') setWarnings(r.routing?.warnings ?? [])
      toast.success(t('common.saved'))
      queryClient.invalidateQueries({ queryKey: ['mfg', 'routing', id] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'routings'] })
    },
    onError: (e) => { setAction(null); setError(serverMessage(e, t('common.failedToSave'))) },
  })

  if (isLoading || !routing) return <Skeleton className="h-64 w-full" />
  const canEdit = draft && canManage
  const ask = { approve: 'mfg.routing.approveAsk', obsolete: 'mfg.routing.obsoleteAsk', copy: 'mfg.routing.copyAsk', delete: 'mfg.routing.deleteAsk' } as const

  return (
    <div className="space-y-4">
      <PageHeader
        title={`${routing.itemCode} · v${routing.version}`}
        hint={routing.itemName}
        actions={
          <>
            <VersionBadge status={routing.status} />
            <Link to="/manufacturing/routings" className="text-sm text-brand-action underline">{t('mfg.bom.backToList')}</Link>
            {canManage && <Button variant="outline" onClick={() => setAction('copy')}>{t('mfg.bom.copy')}</Button>}
            {draft && canApprove && <Button onClick={() => setAction('approve')}>{t('mfg.bom.approve')}</Button>}
            {routing.status === 'Approved' && canApprove && <Button variant="outline" onClick={() => setAction('obsolete')}>{t('mfg.bom.obsolete')}</Button>}
            {canEdit && <Button variant="danger" onClick={() => setAction('delete')}>{t('mfg.bom.delete')}</Button>}
          </>
        }
      />
      {error && <Alert variant="danger" onClose={() => setError(null)}>{error}</Alert>}
      {warnings.length > 0 && <Alert variant="warning" title={t('mfg.routing.warningsTitle')} onClose={() => setWarnings([])}><ul className="list-disc pl-5">{warnings.map((w) => <li key={w}>{w}</li>)}</ul></Alert>}
      {routing.status !== 'Draft' && <Alert variant="info">{t('mfg.bom.immutable')}</Alert>}
      <p className="text-sm text-ink-secondary">{t('mfg.bom.validity')}: {routing.effectiveFrom} → {routing.effectiveTo ?? '…'}{routing.approvedBy ? ` · ${t('mfg.bom.approvedBy', { by: routing.approvedBy, at: routing.approvedAt?.slice(0, 16).replace('T', ' ') })}` : ''}</p>

      <TabList id="mfg-rt" label={t('mfg.nav.routings')} current={tab} onSelect={setTab}
        tabs={[{ key: 'operations', label: t('mfg.routing.operations') }, { key: 'lead', label: t('mfg.routing.leadTime') }, { key: 'versions', label: t('mfg.bom.versions') }]} />
      <div role="tabpanel">
        {tab === 'operations' && <OperationsGrid routing={routing} canEdit={canEdit} />}
        {tab === 'lead' && <LeadTimePanel routing={routing} />}
        {tab === 'versions' && <VersionsPanel routing={routing} />}
      </div>
      <ConfirmDialog
        open={!!action}
        danger={action === 'delete' || action === 'obsolete'}
        title={action ? t(`mfg.bom.${action}`) : ''}
        message={action ? t(ask[action], { code: routing.itemCode, version: routing.version }) : ''}
        onConfirm={() => action && run.mutate(action)}
        onCancel={() => setAction(null)}
      />
    </div>
  )
}
