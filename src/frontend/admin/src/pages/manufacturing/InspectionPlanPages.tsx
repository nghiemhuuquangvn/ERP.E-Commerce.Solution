import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  mfgApi, type CharacteristicKind, type InspectionCharacteristic, type InspectionCharacteristicBody, type InspectionPlanListItem, type InspectionType, type SamplingMethod, type VersionStatus,
} from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { TabList } from '@/components/shared/PageTabs'
import { NoPlant, PageHeader, PlantSelector, VersionBadge, fmtQty, todayIso, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Badge, Button, Checkbox, FormField, Input, Modal, Select, Skeleton, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const n = (v: string) => (v === '' ? null : Number(v))

/** "5 units", "10 %" or "all" - how a plan takes its sample. */
function useSampleText() {
  const { t } = useTranslation()
  return (method: SamplingMethod, value?: number | null) => (method === 'Full' ? t('mfg.qc.sample.Full') : method === 'Percent' ? `${fmtQty(value ?? 0, 2)} %` : t('mfg.qc.sample.units', { count: value ?? 0 }))
}

// ── List ──────────────────────────────────────────────────────────────────────────────────────────────────
interface NewPlanForm { code: string; type: InspectionType; samplingMethod: SamplingMethod; sampleValue: string; allowedMinorDefects: string }

function NewPlanModal({ plantId, onClose }: { plantId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [item, setItem] = useState<{ id: string; label: string } | null>(null)
  const [picker, setPicker] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit, watch, formState: { errors } } = useForm<NewPlanForm>({ defaultValues: { code: '', type: 'Fqc', samplingMethod: 'Fixed', sampleValue: '5', allowedMinorDefects: '0' } })
  const method = watch('samplingMethod')
  const create = useMutation({
    mutationFn: (f: NewPlanForm) => mfgApi.createInspectionPlan({
      plantId, itemId: item!.id, code: f.code, type: f.type, samplingMethod: f.samplingMethod, sampleValue: f.samplingMethod === 'Full' ? null : n(f.sampleValue),
      allowedMinorDefects: Number(f.allowedMinorDefects || 0), effectiveFrom: todayIso(),
    }),
    onSuccess: (p) => navigate(`/manufacturing/inspection-plans/${p.id}`),
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <Modal open size="lg" title={t('mfg.qc.plan.new')} onClose={onClose} closeOnEsc={!create.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="qc-plan-form" disabled={!item} loading={create.isPending}>{t('common.save')}</Button></>}>
      <form id="qc-plan-form" onSubmit={handleSubmit((f) => create.mutate(f))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <div className="flex items-end gap-3">
          <div className="flex-1"><span className="mb-1 block text-sm font-medium text-ink-secondary">{t('mfg.qc.plan.item')}</span><div className="flex h-9 items-center rounded-md border border-line px-3 text-sm" aria-live="polite">{item?.label ?? t('mfg.qc.plan.noItem')}</div></div>
          <Button variant="outline" onClick={() => setPicker(true)}>{t('mfg.bom.pick')}</Button>
        </div>
        <div className="grid gap-4 sm:grid-cols-3">
          <FormField label={t('mfg.qc.plan.code')} required hint={t('mfg.qc.plan.codeHint')} error={errors.code ? t('common.required') : undefined}><Input {...register('code', { required: true })} /></FormField>
          <FormField label={t('mfg.qc.plan.type')}><Select {...register('type')}>{(['Fqc', 'Ipqc'] as InspectionType[]).map((x) => <option key={x} value={x}>{t(`mfg.qc.type.${x}`)}</option>)}</Select></FormField>
          <FormField label={t('mfg.qc.plan.allowedMinor')} hint={t('mfg.qc.plan.allowedMinorHint')}><Input type="number" min={0} step={1} {...register('allowedMinorDefects')} /></FormField>
          <FormField label={t('mfg.qc.plan.sampling')}><Select {...register('samplingMethod')}>{(['Fixed', 'Percent', 'Full'] as SamplingMethod[]).map((x) => <option key={x} value={x}>{t(`mfg.qc.sampling.${x}`)}</option>)}</Select></FormField>
          {method !== 'Full' && <FormField label={method === 'Percent' ? t('mfg.qc.plan.percent') : t('mfg.qc.plan.units')} required error={errors.sampleValue ? t('common.required') : undefined}><Input type="number" min={0} step="any" {...register('sampleValue', { required: true })} /></FormField>}
        </div>
      </form>
      <ProductPickerModal open={picker} multiple={false} onClose={() => setPicker(false)} onConfirm={(p) => { setPicker(false); if (p[0]) setItem({ id: p[0].id, label: `${p[0].sku} ${p[0].name}` }) }} />
    </Modal>
  )
}

export function InspectionPlanListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:qc:plan-manage')
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [type, setType] = useState<InspectionType | ''>('')
  const [status, setStatus] = useState<VersionStatus | ''>('')
  const [search, setSearch] = useState('')
  const [creating, setCreating] = useState(false)
  const sampleText = useSampleText()
  const { data: plans = [], isLoading } = useQuery({
    queryKey: ['mfg', 'inspection-plans', plantId, type, status],
    queryFn: () => mfgApi.listInspectionPlans(plantId!, { type: type || undefined, status: status || undefined }),
    enabled: !!plantId,
  })
  const rows = plans.filter((p) => !search || `${p.code} ${p.itemCode} ${p.itemName}`.toLowerCase().includes(search.toLowerCase()))

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.inspectionPlans')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.inspectionPlans')} hint={t('mfg.qc.plan.hint')} actions={<><PlantSelector />{canManage && <Button onClick={() => setCreating(true)}>{t('mfg.qc.plan.new')}</Button>}</>} />
      <div className="mb-3 flex flex-wrap gap-3">
        <div className="w-72"><Input type="search" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} /></div>
        <Select className="w-40" aria-label={t('mfg.qc.plan.type')} value={type} onChange={(e) => setType(e.target.value as InspectionType | '')}>
          <option value="">{t('common.all')}</option>
          {(['Fqc', 'Ipqc'] as InspectionType[]).map((x) => <option key={x} value={x}>{t(`mfg.qc.type.${x}`)}</option>)}
        </Select>
        <Select className="w-44" aria-label={t('common.status')} value={status} onChange={(e) => setStatus(e.target.value as VersionStatus | '')}>
          <option value="">{t('common.all')}</option>
          {['Draft', 'Approved', 'Obsolete'].map((s) => <option key={s} value={s}>{t(`mfg.status.${s}`)}</option>)}
        </Select>
      </div>
      <DataTable
        data={rows}
        isLoading={isLoading}
        emptyMessage={t('mfg.qc.plan.empty')}
        columns={[
          { key: 'code', label: t('common.code'), render: (p: InspectionPlanListItem) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/inspection-plans/${p.id}`}>{p.code}</Link> },
          { key: 'item', label: t('mfg.qc.plan.item'), render: (p: InspectionPlanListItem) => <div><span className="font-mono text-xs">{p.itemCode}</span> {p.itemName}</div> },
          { key: 'type', label: t('mfg.qc.plan.type'), render: (p: InspectionPlanListItem) => <Badge variant="info">{t(`mfg.qc.type.${p.type}`)}</Badge> },
          { key: 'version', label: t('mfg.bom.version'), align: 'right' as const, render: (p: InspectionPlanListItem) => `v${p.version}` },
          { key: 'status', label: t('common.status'), render: (p: InspectionPlanListItem) => <VersionBadge status={p.status} /> },
          { key: 'sample', label: t('mfg.qc.plan.sample'), render: (p: InspectionPlanListItem) => sampleText(p.samplingMethod, p.sampleValue) },
          { key: 'valid', label: t('mfg.bom.validity'), render: (p: InspectionPlanListItem) => `${p.effectiveFrom} → ${p.effectiveTo ?? '…'}` },
          { key: 'characteristicCount', label: t('mfg.qc.plan.characteristics'), align: 'right' as const },
        ]}
      />
      {creating && plantId && <NewPlanModal plantId={plantId} onClose={() => setCreating(false)} />}
    </div>
  )
}

// ── Characteristic modal ──────────────────────────────────────────────────────────────────────────────────
interface CharForm { name: string; kind: CharacteristicKind; minValue: string; maxValue: string; unit: string; isCritical: boolean }

function CharacteristicModal({ planId, item, onClose }: { planId: string; item: InspectionCharacteristic | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit, watch, formState: { errors } } = useForm<CharForm>({
    defaultValues: { name: item?.name ?? '', kind: item?.kind ?? 'Measurement', minValue: item?.minValue != null ? String(item.minValue) : '', maxValue: item?.maxValue != null ? String(item.maxValue) : '', unit: item?.unit ?? '', isCritical: item?.isCritical ?? false },
  })
  const kind = watch('kind')
  const save = useMutation({
    mutationFn: (f: CharForm) => {
      const body: InspectionCharacteristicBody = {
        name: f.name, kind: f.kind, minValue: f.kind === 'Measurement' ? n(f.minValue) : null, maxValue: f.kind === 'Measurement' ? n(f.maxValue) : null,
        unit: f.kind === 'Measurement' ? f.unit || null : null, isCritical: f.isCritical,
      }
      return item ? mfgApi.updateCharacteristic(planId, item.seq, body) : mfgApi.addCharacteristic(planId, body)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'inspection-plan', planId] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <Modal open size="lg" title={item ? t('mfg.qc.plan.editCharacteristic', { no: item.seq }) : t('mfg.qc.plan.addCharacteristic')} onClose={onClose} closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="qc-char-form" loading={save.isPending}>{t('common.save')}</Button></>}>
      <form id="qc-char-form" onSubmit={handleSubmit((f) => save.mutate(f))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <div className="grid gap-4 sm:grid-cols-3">
          <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}><Input {...register('name', { required: true })} /></FormField>
          <FormField label={t('mfg.qc.plan.kind')}><Select {...register('kind')}>{(['Measurement', 'Attribute'] as CharacteristicKind[]).map((x) => <option key={x} value={x}>{t(`mfg.qc.kind.${x}`)}</option>)}</Select></FormField>
          <div className="flex items-end"><Checkbox label={t('mfg.qc.plan.critical')} {...register('isCritical')} /></div>
          {kind === 'Measurement' && (
            <>
              <FormField label={t('mfg.qc.plan.min')}><Input type="number" step="any" {...register('minValue')} /></FormField>
              <FormField label={t('mfg.qc.plan.max')}><Input type="number" step="any" {...register('maxValue')} /></FormField>
              <FormField label={t('mfg.qc.plan.unit')}><Input {...register('unit')} /></FormField>
            </>
          )}
        </div>
        {kind === 'Measurement' && <p className="text-xs text-ink-secondary">{t('mfg.qc.plan.rangeHint')}</p>}
      </form>
    </Modal>
  )
}

// ── Editor ────────────────────────────────────────────────────────────────────────────────────────────────
type EditorTab = 'characteristics' | 'versions'
type Action = 'approve' | 'obsolete' | 'copy' | 'delete'
interface HeaderForm { samplingMethod: SamplingMethod; sampleValue: string; allowedMinorDefects: string; effectiveFrom: string; effectiveTo: string; description: string }

export function InspectionPlanEditorPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const [tab, setTab] = useState<EditorTab>('characteristics')
  const [action, setAction] = useState<Action | null>(null)
  const [editing, setEditing] = useState<InspectionCharacteristic | 'new' | null>(null)
  const [error, setError] = useState<string | null>(null)
  const sampleText = useSampleText()
  const { data: plan, isLoading } = useQuery({ queryKey: ['mfg', 'inspection-plan', id], queryFn: () => mfgApi.getInspectionPlan(id) })
  const { data: versions = [] } = useQuery({
    queryKey: ['mfg', 'inspection-plans', plan?.plantId, 'series', plan?.code],
    queryFn: async () => (await mfgApi.listInspectionPlans(plan!.plantId, { type: plan!.type, itemId: plan!.itemId })).filter((p) => p.code === plan!.code),
    enabled: !!plan,
  })
  const draft = plan?.status === 'Draft'
  const canManage = hasPermission('manufacturing:qc:plan-manage')
  const canApprove = hasPermission('manufacturing:qc:plan-approve')
  const canEdit = draft && canManage

  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'inspection-plan', id] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'inspection-plans'] }) }
  const run = useMutation({
    mutationFn: async (a: Action) => {
      if (a === 'approve') return { kind: a, plan: await mfgApi.approveInspectionPlan(id) }
      if (a === 'obsolete') return { kind: a, plan: await mfgApi.obsoleteInspectionPlan(id) }
      if (a === 'copy') return { kind: a, plan: await mfgApi.copyInspectionPlan(id) }
      await mfgApi.deleteInspectionPlan(id)
      return { kind: a, plan: null }
    },
    onSuccess: (r) => {
      setAction(null); setError(null)
      if (r.kind === 'delete') { navigate('/manufacturing/inspection-plans'); return }
      if (r.kind === 'copy' && r.plan) { toast.success(t('mfg.qc.plan.copied', { version: r.plan.version })); navigate(`/manufacturing/inspection-plans/${r.plan.id}`); return }
      toast.success(t('common.saved'))
      refresh()
    },
    onError: (e) => { setAction(null); setError(serverMessage(e, t('common.failedToSave'))) },
  })
  const removeChar = useMutation({
    mutationFn: (seq: number) => mfgApi.removeCharacteristic(id, seq),
    onSuccess: refresh,
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  const header = useForm<HeaderForm>({
    values: plan ? { samplingMethod: plan.samplingMethod, sampleValue: plan.sampleValue != null ? String(plan.sampleValue) : '', allowedMinorDefects: String(plan.allowedMinorDefects), effectiveFrom: plan.effectiveFrom, effectiveTo: plan.effectiveTo ?? '', description: plan.description ?? '' } : undefined,
  })
  const method = header.watch('samplingMethod')
  const saveHeader = useMutation({
    mutationFn: (f: HeaderForm) => mfgApi.updateInspectionPlan(id, {
      samplingMethod: f.samplingMethod, sampleValue: f.samplingMethod === 'Full' ? null : n(f.sampleValue), allowedMinorDefects: Number(f.allowedMinorDefects || 0),
      effectiveFrom: f.effectiveFrom, effectiveTo: f.effectiveTo || null, description: f.description || null,
    }),
    onSuccess: () => { setError(null); toast.success(t('common.saved')); refresh() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  const askText = useMemo(() => ({ approve: 'mfg.qc.plan.approveAsk', obsolete: 'mfg.qc.plan.obsoleteAsk', copy: 'mfg.qc.plan.copyAsk', delete: 'mfg.qc.plan.deleteAsk' }), [])
  if (isLoading || !plan) return <Skeleton className="h-64 w-full" />

  return (
    <div className="space-y-4">
      <PageHeader
        title={`${plan.code} · v${plan.version}`}
        hint={`${plan.itemCode} ${plan.itemName} · ${t(`mfg.qc.type.${plan.type}`)}`}
        actions={
          <>
            <VersionBadge status={plan.status} />
            <Link to="/manufacturing/inspection-plans" className="text-sm text-brand-action underline">{t('mfg.qc.plan.backToList')}</Link>
            {canManage && <Button variant="outline" onClick={() => setAction('copy')}>{t('mfg.qc.plan.copy')}</Button>}
            {draft && canApprove && <Button onClick={() => setAction('approve')}>{t('mfg.qc.plan.approve')}</Button>}
            {plan.status === 'Approved' && canApprove && <Button variant="outline" onClick={() => setAction('obsolete')}>{t('mfg.qc.plan.obsolete')}</Button>}
            {canEdit && <Button variant="danger" onClick={() => setAction('delete')}>{t('mfg.qc.plan.delete')}</Button>}
          </>
        }
      />
      {error && <Alert variant="danger" onClose={() => setError(null)}>{error}</Alert>}
      {plan.status !== 'Draft' && <Alert variant="info">{t('mfg.qc.plan.immutable')}</Alert>}

      <form onSubmit={header.handleSubmit((f) => saveHeader.mutate(f))} className="grid gap-4 rounded-lg border border-line bg-surface p-4 sm:grid-cols-6" aria-label={t('mfg.qc.plan.header')}>
        <fieldset disabled={!canEdit} className="contents">
          <FormField label={t('mfg.qc.plan.sampling')}><Select {...header.register('samplingMethod')}>{(['Fixed', 'Percent', 'Full'] as SamplingMethod[]).map((x) => <option key={x} value={x}>{t(`mfg.qc.sampling.${x}`)}</option>)}</Select></FormField>
          {method !== 'Full' && <FormField label={method === 'Percent' ? t('mfg.qc.plan.percent') : t('mfg.qc.plan.units')}><Input type="number" min={0} step="any" {...header.register('sampleValue')} /></FormField>}
          <FormField label={t('mfg.qc.plan.allowedMinor')}><Input type="number" min={0} step={1} {...header.register('allowedMinorDefects')} /></FormField>
          <FormField label={t('mfg.bom.validFrom')}><Input type="date" {...header.register('effectiveFrom', { required: true })} /></FormField>
          <FormField label={t('mfg.bom.validTo')}><Input type="date" {...header.register('effectiveTo')} /></FormField>
          {canEdit && <div className="flex items-end"><Button type="submit" variant="secondary" loading={saveHeader.isPending}>{t('common.save')}</Button></div>}
        </fieldset>
        <p className="text-xs text-ink-secondary sm:col-span-6">{t('mfg.qc.plan.sampleSummary', { sample: sampleText(plan.samplingMethod, plan.sampleValue), minor: plan.allowedMinorDefects })}{plan.approvedBy ? ` · ${t('mfg.bom.approvedBy', { by: plan.approvedBy, at: plan.approvedAt?.slice(0, 16).replace('T', ' ') })}` : ''}</p>
      </form>

      <TabList id="mfg-qc-plan" label={t('mfg.nav.inspectionPlans')} current={tab} onSelect={setTab}
        tabs={[{ key: 'characteristics', label: t('mfg.qc.plan.characteristics') }, { key: 'versions', label: t('mfg.bom.versions') }]} />
      <div role="tabpanel">
        {tab === 'characteristics' && (
          <div className="space-y-3">
            {canEdit && <div className="flex justify-end"><Button onClick={() => setEditing('new')}>{t('mfg.qc.plan.addCharacteristic')}</Button></div>}
            <DataTable
              data={plan.characteristics.map((c) => ({ ...c, id: String(c.seq) }))}
              emptyMessage={t('mfg.qc.plan.noCharacteristics')}
              columns={[
                { key: 'seq', label: '#', render: (c: InspectionCharacteristic) => <span className="font-mono text-xs">{c.seq}</span> },
                { key: 'name', label: t('common.name') },
                { key: 'kind', label: t('mfg.qc.plan.kind'), render: (c: InspectionCharacteristic) => t(`mfg.qc.kind.${c.kind}`) },
                { key: 'range', label: t('mfg.qc.plan.range'), render: (c: InspectionCharacteristic) => (c.kind === 'Measurement' ? `${c.minValue ?? '…'} – ${c.maxValue ?? '…'} ${c.unit ?? ''}` : t('mfg.qc.passFail')) },
                { key: 'isCritical', label: t('mfg.qc.plan.critical'), render: (c: InspectionCharacteristic) => (c.isCritical ? <Badge variant="danger">{t('mfg.qc.plan.critical')}</Badge> : <Badge variant="muted">{t('mfg.qc.plan.minor')}</Badge>) },
                {
                  key: 'actions', label: t('common.actions'),
                  render: (c: InspectionCharacteristic) => canEdit ? <div className="flex gap-2"><Button variant="link" size="sm" onClick={() => setEditing(c)}>{t('common.edit')}</Button><Button variant="link-danger" size="sm" onClick={() => removeChar.mutate(c.seq)}>{t('common.delete')}</Button></div> : null,
                },
              ]}
            />
          </div>
        )}
        {tab === 'versions' && (
          <DataTable
            data={versions.slice().sort((a, b) => b.version - a.version)}
            emptyMessage={t('mfg.qc.plan.empty')}
            columns={[
              { key: 'version', label: t('mfg.bom.version'), render: (v: InspectionPlanListItem) => <Link className="text-brand-action hover:underline" to={`/manufacturing/inspection-plans/${v.id}`}>v{v.version}{v.id === plan.id ? ` (${t('mfg.bom.thisOne')})` : ''}</Link> },
              { key: 'status', label: t('common.status'), render: (v: InspectionPlanListItem) => <VersionBadge status={v.status} /> },
              { key: 'valid', label: t('mfg.bom.validity'), render: (v: InspectionPlanListItem) => `${v.effectiveFrom} → ${v.effectiveTo ?? '…'}` },
              { key: 'characteristicCount', label: t('mfg.qc.plan.characteristics'), align: 'right' as const },
            ]}
          />
        )}
      </div>

      {editing && <CharacteristicModal planId={plan.id} item={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!action}
        danger={action === 'delete' || action === 'obsolete'}
        title={action ? t(`mfg.qc.plan.${action}`) : ''}
        message={action ? t(askText[action], { code: plan.code, version: plan.version }) : ''}
        onConfirm={() => action && run.mutate(action)}
        onCancel={() => setAction(null)}
      />
    </div>
  )
}
