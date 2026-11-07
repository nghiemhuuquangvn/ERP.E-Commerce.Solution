import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Bom, type BomLine, type BomLineBody, type BomListItem, type VersionStatus } from '@/api/manufacturing/mfg.api'
import { unitsOfMeasureApi } from '@/api/masterdata/unitsOfMeasure.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { TabList } from '@/components/shared/PageTabs'
import { BomTree } from '@/components/manufacturing/BomTree'
import { CsvImportDialog, NoPlant, PageHeader, PlantSelector, VersionBadge, fmtQty, todayIso, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Badge, Button, Checkbox, FormField, Input, Modal, Select, Skeleton, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

function useUnits() {
  const { data } = useQuery({ queryKey: ['units-of-measure', 'all'], queryFn: () => unitsOfMeasureApi.list({ page: 1, pageSize: 200 }), staleTime: 5 * 60_000 })
  const units = data?.items ?? []
  return { units, code: (id: string) => units.find((u) => u.id === id)?.code ?? id.slice(0, 8) }
}

// ── List ──────────────────────────────────────────────────────────────────────────────────────────────────
export function BomListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:bom:manage')
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [status, setStatus] = useState<VersionStatus | ''>('')
  const [search, setSearch] = useState('')
  const [picker, setPicker] = useState<'new' | 'import' | null>(null)
  const [importItem, setImportItem] = useState<string | null>(null)
  const { data: boms = [], isLoading } = useQuery({ queryKey: ['mfg', 'boms', plantId, status], queryFn: () => mfgApi.listBoms(plantId!, undefined, status || undefined), enabled: !!plantId })
  const create = useMutation({
    mutationFn: (itemId: string) => mfgApi.createBom({ plantId: plantId!, itemId, effectiveFrom: todayIso() }),
    onSuccess: (b) => navigate(`/manufacturing/boms/${b.id}`),
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  const rows = boms.filter((b) => !search || `${b.itemCode} ${b.itemName}`.toLowerCase().includes(search.toLowerCase()))

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.boms')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader
        title={t('mfg.nav.boms')}
        hint={t('mfg.bom.hint')}
        actions={<><PlantSelector />{canManage && <><Button variant="outline" onClick={() => setPicker('import')}>{t('mfg.import.openBom')}</Button><Button onClick={() => setPicker('new')}>{t('mfg.bom.new')}</Button></>}</>}
      />
      <div className="mb-3 flex flex-wrap gap-3">
        <div className="w-72"><Input type="search" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} /></div>
        <Select className="w-44" aria-label={t('common.status')} value={status} onChange={(e) => setStatus(e.target.value as VersionStatus | '')}>
          <option value="">{t('common.all')}</option>
          {['Draft', 'Approved', 'Obsolete'].map((s) => <option key={s} value={s}>{t(`mfg.status.${s}`)}</option>)}
        </Select>
        <Link to="/manufacturing/where-used" className="self-center text-sm font-medium text-brand-action underline">{t('mfg.nav.whereUsed')}</Link>
      </div>
      <DataTable
        data={rows}
        isLoading={isLoading}
        emptyMessage={t('mfg.bom.empty')}
        columns={[
          { key: 'itemCode', label: t('common.code'), render: (b: BomListItem) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/boms/${b.id}`}>{b.itemCode}</Link> },
          { key: 'itemName', label: t('common.name') },
          { key: 'version', label: t('mfg.bom.version'), align: 'right' as const, render: (b: BomListItem) => `v${b.version}` },
          { key: 'status', label: t('common.status'), render: (b: BomListItem) => <VersionBadge status={b.status} /> },
          { key: 'valid', label: t('mfg.bom.validity'), render: (b: BomListItem) => `${b.effectiveFrom} → ${b.effectiveTo ?? '…'}` },
          { key: 'lineCount', label: t('mfg.bom.lines'), align: 'right' as const },
        ]}
      />
      <ProductPickerModal open={picker !== null} multiple={false} onClose={() => setPicker(null)} onConfirm={(p) => { const kind = picker; setPicker(null); if (!p[0]) return; if (kind === 'new') create.mutate(p[0].id); else setImportItem(p[0].id) }} />
      {importItem && plantId && <CsvImportDialog open kind="bom" plantId={plantId} itemId={importItem} effectiveFrom={todayIso()} onClose={() => setImportItem(null)} onImported={(id) => { setImportItem(null); navigate(`/manufacturing/boms/${id}`) }} />}
    </div>
  )
}

// ── Line modal ────────────────────────────────────────────────────────────────────────────────────────────
interface LineForm {
  lineType: BomLine['lineType']; quantity: string; uomId: string; scrapPct: string; isFixedQuantity: boolean; isPhantom: boolean; issueMethod: BomLine['issueMethod']
  operationSeq: string; alternativeGroup: string; alternativePriority: string; costAllocationPct: string; notes: string
}
const n = (v: string) => (v === '' ? null : Number(v))

function LineModal({ bom, line, onClose }: { bom: Bom; line: BomLine | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { units } = useUnits()
  const [component, setComponent] = useState<{ id: string; label: string } | null>(line ? { id: line.componentItemId, label: `${line.componentCode} ${line.componentName}` } : null)
  const [picker, setPicker] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit, watch, formState: { errors } } = useForm<LineForm>({
    defaultValues: {
      lineType: line?.lineType ?? 'Component', quantity: line ? String(line.quantity) : '', uomId: line?.uomId ?? '', scrapPct: String(line?.scrapPct ?? 0), isFixedQuantity: line?.isFixedQuantity ?? false,
      isPhantom: line?.isPhantom ?? false, issueMethod: line?.issueMethod ?? 'Manual', operationSeq: line?.operationSeq != null ? String(line.operationSeq) : '', alternativeGroup: line?.alternativeGroup ?? '',
      alternativePriority: line?.alternativePriority != null ? String(line.alternativePriority) : '', costAllocationPct: line?.costAllocationPct != null ? String(line.costAllocationPct) : '', notes: line?.notes ?? '',
    },
  })
  const type = watch('lineType')
  const save = useMutation({
    mutationFn: (f: LineForm) => {
      const body: BomLineBody = {
        lineType: f.lineType, componentItemId: component!.id, quantity: Number(f.quantity), uomId: f.uomId, scrapPct: Number(f.scrapPct || 0), isFixedQuantity: f.isFixedQuantity, isPhantom: f.lineType === 'Component' && f.isPhantom,
        issueMethod: f.issueMethod, operationSeq: n(f.operationSeq), alternativeGroup: f.alternativeGroup || null, alternativePriority: f.alternativeGroup ? n(f.alternativePriority) : null,
        costAllocationPct: f.lineType === 'CoProduct' ? n(f.costAllocationPct) : null, notes: f.notes || null,
      }
      return line ? mfgApi.updateBomLine(bom.id, line.lineNo, body) : mfgApi.addBomLine(bom.id, body)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'bom', bom.id] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <Modal open size="lg" title={line ? t('mfg.bom.editLine', { no: line.lineNo }) : t('mfg.bom.addLine')} onClose={onClose} closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="bom-line-form" disabled={!component} loading={save.isPending}>{t('common.save')}</Button></>}>
      <form id="bom-line-form" onSubmit={handleSubmit((f) => save.mutate(f))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <div className="flex items-end gap-3">
          <div className="flex-1"><span className="mb-1 block text-sm font-medium text-ink-secondary">{t('mfg.bom.component')}</span><div className="flex h-9 items-center rounded-md border border-line px-3 text-sm" aria-live="polite">{component?.label ?? t('mfg.bom.noComponent')}</div></div>
          <Button variant="outline" onClick={() => setPicker(true)}>{t('mfg.bom.pick')}</Button>
        </div>
        <div className="grid gap-4 sm:grid-cols-4">
          <FormField label={t('mfg.bom.lineType')}><Select {...register('lineType')}>{['Component', 'ByProduct', 'CoProduct'].map((x) => <option key={x} value={x}>{t(`mfg.bom.${x}`)}</option>)}</Select></FormField>
          <FormField label={t('mfg.bom.quantity')} required error={errors.quantity ? t('common.required') : undefined}><Input type="number" step="any" min={0} {...register('quantity', { required: true })} /></FormField>
          <FormField label={t('mfg.bom.uom')} required error={errors.uomId ? t('common.required') : undefined}>
            <Select {...register('uomId', { required: true })}><option value="">{t('common.select')}</option>{units.map((u) => <option key={u.id} value={u.id}>{u.code}</option>)}</Select>
          </FormField>
          <FormField label={t('mfg.bom.scrap')}><Input type="number" step="0.01" min={0} max={99.99} {...register('scrapPct')} /></FormField>
          <FormField label={t('mfg.bom.operation')}><Input type="number" min={1} {...register('operationSeq')} /></FormField>
          <FormField label={t('mfg.bom.issueMethod')}><Select {...register('issueMethod')}><option value="Manual">{t('mfg.issue.Manual')}</option><option value="Backflush">{t('mfg.issue.Backflush')}</option></Select></FormField>
          <FormField label={t('mfg.bom.altGroup')} hint={t('mfg.bom.altHint')}><Input {...register('alternativeGroup')} /></FormField>
          <FormField label={t('mfg.bom.altPriority')}><Input type="number" min={1} {...register('alternativePriority')} /></FormField>
        </div>
        {type === 'CoProduct' && <FormField label={t('mfg.bom.costShare')} hint={t('mfg.bom.costShareHint')}><Input type="number" step="0.01" min={0} max={99.99} {...register('costAllocationPct')} /></FormField>}
        {type === 'Component' && (
          <div className="flex gap-6"><Checkbox label={t('mfg.bom.isPhantom')} {...register('isPhantom')} /><Checkbox label={t('mfg.bom.isFixed')} {...register('isFixedQuantity')} /></div>
        )}
        <FormField label={t('common.notes')}><Input {...register('notes')} /></FormField>
      </form>
      <ProductPickerModal open={picker} multiple={false} onClose={() => setPicker(false)} onConfirm={(p) => { setPicker(false); if (p[0]) setComponent({ id: p[0].id, label: `${p[0].sku} ${p[0].name}` }) }} />
    </Modal>
  )
}

// ── Editor ────────────────────────────────────────────────────────────────────────────────────────────────
type EditorTab = 'lines' | 'tree' | 'explosion' | 'versions'
type Action = 'approve' | 'obsolete' | 'copy' | 'delete'

function LinesTab({ bom, canEdit }: { bom: Bom; canEdit: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { code } = useUnits()
  const [editing, setEditing] = useState<BomLine | 'new' | null>(null)
  const remove = useMutation({
    mutationFn: (no: number) => mfgApi.removeBomLine(bom.id, no),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['mfg', 'bom', bom.id] }),
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <div className="space-y-3">
      {canEdit && <div className="flex justify-end"><Button onClick={() => setEditing('new')}>{t('mfg.bom.addLine')}</Button></div>}
      <DataTable
        data={bom.lines.map((l) => ({ id: String(l.lineNo), ...l }))}
        emptyMessage={t('mfg.bom.noLines')}
        columns={[
          { key: 'lineNo', label: '#', render: (l: BomLine) => <span className="font-mono text-xs">{l.lineNo}</span> },
          { key: 'component', label: t('mfg.bom.component'), render: (l: BomLine) => <div><span className="font-mono text-xs">{l.componentCode}</span> {l.componentName}</div> },
          { key: 'lineType', label: t('mfg.bom.lineType'), render: (l: BomLine) => l.lineType === 'Component' ? '' : <Badge variant="info">{t(`mfg.bom.${l.lineType}`)}</Badge> },
          { key: 'quantity', label: t('mfg.bom.quantity'), align: 'right' as const, render: (l: BomLine) => `${fmtQty(l.quantity)} ${code(l.uomId)}` },
          { key: 'scrapPct', label: t('mfg.bom.scrap'), align: 'right' as const, render: (l: BomLine) => (l.scrapPct ? `${fmtQty(l.scrapPct, 2)}%` : '') },
          { key: 'operationSeq', label: t('mfg.bom.operation'), align: 'right' as const, render: (l: BomLine) => l.operationSeq ?? '' },
          { key: 'issueMethod', label: t('mfg.bom.issueMethod'), render: (l: BomLine) => t(`mfg.issue.${l.issueMethod}`) },
          {
            key: 'flags', label: t('mfg.bom.flags'),
            render: (l: BomLine) => (
              <div className="flex flex-wrap gap-1">
                {l.isPhantom && <Badge variant="info">{t('mfg.bom.phantom')}</Badge>}
                {l.isFixedQuantity && <Badge variant="muted">{t('mfg.bom.fixed')}</Badge>}
                {l.alternativeGroup && <Badge variant="brand">{l.alternativeGroup} #{l.alternativePriority}</Badge>}
                {l.costAllocationPct != null && <Badge variant="muted">{fmtQty(l.costAllocationPct, 2)}%</Badge>}
              </div>
            ),
          },
          {
            key: 'actions', label: t('common.actions'),
            render: (l: BomLine) => (
              <div className="flex gap-2">
                <Link className="text-sm text-brand-action hover:underline" to={`/manufacturing/where-used?itemId=${l.componentItemId}`}>{t('mfg.nav.whereUsed')}</Link>
                {canEdit && <><Button variant="link" size="sm" onClick={() => setEditing(l)}>{t('common.edit')}</Button><Button variant="link-danger" size="sm" onClick={() => remove.mutate(l.lineNo)}>{t('common.delete')}</Button></>}
              </div>
            ),
          },
        ]}
      />
      {editing && <LineModal bom={bom} line={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
    </div>
  )
}

function TreeTab({ bom }: { bom: Bom }) {
  const { t } = useTranslation()
  const [qty, setQty] = useState('1')
  const [date, setDate] = useState(todayIso())
  const q = Number(qty) > 0 ? Number(qty) : 1
  const { data, isLoading, error } = useQuery({ queryKey: ['mfg', 'bom-tree', bom.id, q, date], queryFn: () => mfgApi.indented(bom.plantId, bom.itemId, q, bom.id, date) })
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-3">
        <FormField label={t('mfg.bom.forQuantity')}><Input type="number" min={0} step="any" className="w-32" value={qty} onChange={(e) => setQty(e.target.value)} /></FormField>
        <FormField label={t('mfg.bom.onDate')}><Input type="date" className="w-44" value={date} onChange={(e) => setDate(e.target.value)} /></FormField>
      </div>
      {error && <Alert variant="danger">{serverMessage(error, t('mfg.bom.treeFailed'))}</Alert>}
      {isLoading ? <Skeleton className="h-40 w-full" /> : data && <BomTree root={data} />}
    </div>
  )
}

function ExplosionTab({ bom }: { bom: Bom }) {
  const { t } = useTranslation()
  const [qty, setQty] = useState('100')
  const [date, setDate] = useState(todayIso())
  const q = Number(qty) > 0 ? Number(qty) : 1
  const { data, isLoading, error } = useQuery({ queryKey: ['mfg', 'bom-explosion', bom.id, q, date], queryFn: () => mfgApi.explode(bom.plantId, bom.itemId, q, bom.id, date) })
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-3">
        <FormField label={t('mfg.bom.orderQuantity')}><Input type="number" min={0} step="any" className="w-32" value={qty} onChange={(e) => setQty(e.target.value)} /></FormField>
        <FormField label={t('mfg.bom.onDate')}><Input type="date" className="w-44" value={date} onChange={(e) => setDate(e.target.value)} /></FormField>
      </div>
      {error && <Alert variant="danger">{serverMessage(error, t('mfg.bom.treeFailed'))}</Alert>}
      {isLoading ? <Skeleton className="h-40 w-full" /> : data && (
        <>
          <h3 className="text-sm font-semibold">{t('mfg.bom.requirements')}</h3>
          <DataTable
            data={data.requirements.map((r) => ({ id: `${r.componentItemId}-${r.operationSeq}`, ...r }))}
            emptyMessage={t('mfg.bom.noLines')}
            columns={[
              { key: 'componentCode', label: t('mfg.bom.component'), render: (r) => <div><span className="font-mono text-xs">{r.componentCode}</span> {r.componentName}</div> },
              { key: 'quantity', label: t('mfg.bom.quantity'), align: 'right' as const, render: (r) => fmtQty(r.quantity) },
              { key: 'operationSeq', label: t('mfg.bom.operation'), align: 'right' as const, render: (r) => r.operationSeq ?? '' },
              { key: 'issueMethod', label: t('mfg.bom.issueMethod'), render: (r) => t(`mfg.issue.${r.issueMethod}`) },
            ]}
          />
          {data.outputs.length > 0 && (
            <>
              <h3 className="text-sm font-semibold">{t('mfg.bom.outputs')}</h3>
              <DataTable
                data={data.outputs.map((o) => ({ id: `${o.itemId}-${o.sourceLineNo}`, ...o }))}
                columns={[
                  { key: 'itemCode', label: t('mfg.bom.component'), render: (o) => <div><span className="font-mono text-xs">{o.itemCode}</span> {o.itemName}</div> },
                  { key: 'lineType', label: t('mfg.bom.lineType'), render: (o) => t(`mfg.bom.${o.lineType}`) },
                  { key: 'plannedQuantity', label: t('mfg.bom.quantity'), align: 'right' as const, render: (o) => fmtQty(o.plannedQuantity) },
                ]}
              />
            </>
          )}
        </>
      )}
    </div>
  )
}

function VersionsTab({ bom }: { bom: Bom }) {
  const { t } = useTranslation()
  const { data: versions = [] } = useQuery({ queryKey: ['mfg', 'boms', bom.plantId, bom.itemId], queryFn: () => mfgApi.listBoms(bom.plantId, bom.itemId) })
  const others = versions.filter((v) => v.id !== bom.id)
  const [otherId, setOtherId] = useState('')
  const right = otherId || others[0]?.id || ''
  const { data: diff, error } = useQuery({ queryKey: ['mfg', 'bom-compare', bom.id, right], queryFn: () => mfgApi.compareBoms(bom.id, right), enabled: !!right })
  const rightVersion = versions.find((v) => v.id === right)
  const kindVariant = { Added: 'success', Removed: 'danger', Changed: 'warning' } as const
  return (
    <div className="space-y-4">
      <DataTable
        data={versions.slice().sort((a, b) => b.version - a.version)}
        emptyMessage={t('mfg.bom.empty')}
        columns={[
          { key: 'version', label: t('mfg.bom.version'), render: (v: BomListItem) => <Link className="text-brand-action hover:underline" to={`/manufacturing/boms/${v.id}`}>v{v.version}{v.id === bom.id ? ` (${t('mfg.bom.thisOne')})` : ''}</Link> },
          { key: 'status', label: t('common.status'), render: (v: BomListItem) => <VersionBadge status={v.status} /> },
          { key: 'valid', label: t('mfg.bom.validity'), render: (v: BomListItem) => `${v.effectiveFrom} → ${v.effectiveTo ?? '…'}` },
          { key: 'lineCount', label: t('mfg.bom.lines'), align: 'right' as const },
        ]}
      />
      <section aria-labelledby="mfg-compare">
        <div className="mb-2 flex flex-wrap items-end gap-3">
          <h3 id="mfg-compare" className="text-sm font-semibold">{t('mfg.bom.compare', { version: bom.version })}</h3>
          {others.length > 0 && (
            <Select className="w-48" aria-label={t('mfg.bom.compareWith')} value={right} onChange={(e) => setOtherId(e.target.value)}>
              {others.map((v) => <option key={v.id} value={v.id}>v{v.version} ({t(`mfg.status.${v.status}`)})</option>)}
            </Select>
          )}
        </div>
        {others.length === 0 && <p className="text-sm text-ink-secondary">{t('mfg.bom.noOtherVersion')}</p>}
        {error && <Alert variant="danger">{serverMessage(error, t('mfg.bom.treeFailed'))}</Alert>}
        {diff && (
          <>
            <p className="mb-2 text-sm text-ink-secondary">{t('mfg.bom.compareSummary', { count: diff.length, left: bom.version, right: rightVersion?.version ?? '' })}</p>
            <DataTable
              data={diff.map((d, i) => ({ id: String(i), ...d }))}
              emptyMessage={t('mfg.bom.noDifference')}
              columns={[
                { key: 'kind', label: t('mfg.bom.change'), render: (d) => <Badge variant={kindVariant[d.kind]}>{t(`mfg.bom.kind.${d.kind}`)}</Badge> },
                { key: 'componentCode', label: t('mfg.bom.component'), render: (d) => <span className="font-mono text-xs">{d.componentCode}</span> },
                { key: 'operationSeq', label: t('mfg.bom.operation'), align: 'right' as const, render: (d) => d.operationSeq ?? '' },
                { key: 'left', label: `v${bom.version}`, align: 'right' as const, render: (d) => fmtQty(d.leftQuantity) },
                { key: 'right', label: `v${rightVersion?.version ?? ''}`, align: 'right' as const, render: (d) => fmtQty(d.rightQuantity) },
                { key: 'detail', label: t('mfg.bom.detail'), render: (d) => d.detail ?? '' },
              ]}
            />
          </>
        )}
      </section>
    </div>
  )
}

export function BomEditorPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const [tab, setTab] = useState<EditorTab>('lines')
  const [action, setAction] = useState<Action | null>(null)
  const [warnings, setWarnings] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)
  const { data: bom, isLoading } = useQuery({ queryKey: ['mfg', 'bom', id], queryFn: () => mfgApi.getBom(id) })
  const draft = bom?.status === 'Draft'
  const canManage = hasPermission('manufacturing:bom:manage')
  const canApprove = hasPermission('manufacturing:bom:approve')

  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'bom', id] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'boms'] }) }
  const run = useMutation({
    mutationFn: async (a: Action) => {
      if (a === 'approve') return { kind: a, bom: await mfgApi.approveBom(id) }
      if (a === 'obsolete') return { kind: a, bom: await mfgApi.obsoleteBom(id) }
      if (a === 'copy') return { kind: a, bom: await mfgApi.copyBom(id) }
      await mfgApi.deleteBom(id)
      return { kind: a, bom: null }
    },
    onSuccess: (r) => {
      setAction(null); setError(null)
      if (r.kind === 'delete') { navigate('/manufacturing/boms'); return }
      if (r.kind === 'copy' && r.bom) { toast.success(t('mfg.bom.copied', { version: r.bom.version })); navigate(`/manufacturing/boms/${r.bom.id}`); return }
      if (r.kind === 'approve') setWarnings(r.bom?.warnings ?? [])
      toast.success(t('common.saved'))
      refresh()
    },
    onError: (e) => { setAction(null); setError(serverMessage(e, t('common.failedToSave'))) },
  })

  const header = useForm<{ baseQuantity: string; yieldPct: string; effectiveFrom: string; effectiveTo: string; description: string }>({ values: bom ? { baseQuantity: String(bom.baseQuantity), yieldPct: String(bom.yieldPct), effectiveFrom: bom.effectiveFrom, effectiveTo: bom.effectiveTo ?? '', description: bom.description ?? '' } : undefined })
  const saveHeader = useMutation({
    mutationFn: (f: { baseQuantity: string; yieldPct: string; effectiveFrom: string; effectiveTo: string; description: string }) =>
      mfgApi.updateBom(id, { baseQuantity: Number(f.baseQuantity), yieldPct: Number(f.yieldPct), effectiveFrom: f.effectiveFrom, effectiveTo: f.effectiveTo || null, description: f.description || null }),
    onSuccess: () => { setError(null); toast.success(t('common.saved')); refresh() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  const askText = useMemo(() => ({ approve: 'mfg.bom.approveAsk', obsolete: 'mfg.bom.obsoleteAsk', copy: 'mfg.bom.copyAsk', delete: 'mfg.bom.deleteAsk' }), [])
  if (isLoading || !bom) return <Skeleton className="h-64 w-full" />
  const canEdit = draft && canManage

  return (
    <div className="space-y-4">
      <PageHeader
        title={`${bom.itemCode} · v${bom.version}`}
        hint={bom.itemName}
        actions={
          <>
            <VersionBadge status={bom.status} />
            <Link to="/manufacturing/boms" className="text-sm text-brand-action underline">{t('mfg.bom.backToList')}</Link>
            {canManage && <Button variant="outline" onClick={() => setAction('copy')}>{t('mfg.bom.copy')}</Button>}
            {draft && canApprove && <Button onClick={() => setAction('approve')}>{t('mfg.bom.approve')}</Button>}
            {bom.status === 'Approved' && canApprove && <Button variant="outline" onClick={() => setAction('obsolete')}>{t('mfg.bom.obsolete')}</Button>}
            {canEdit && <Button variant="danger" onClick={() => setAction('delete')}>{t('mfg.bom.delete')}</Button>}
          </>
        }
      />
      {error && <Alert variant="danger" onClose={() => setError(null)}>{error}</Alert>}
      {warnings.length > 0 && (
        <Alert variant="warning" title={t('mfg.bom.warningsTitle')} onClose={() => setWarnings([])}>
          <ul className="list-disc pl-5">{warnings.map((w) => <li key={w}>{w}</li>)}</ul>
        </Alert>
      )}
      {bom.status !== 'Draft' && <Alert variant="info">{t('mfg.bom.immutable')}</Alert>}

      <form onSubmit={header.handleSubmit((f) => saveHeader.mutate(f))} className="grid gap-4 rounded-lg border border-line bg-surface p-4 sm:grid-cols-5" aria-label={t('mfg.bom.header')}>
        <fieldset disabled={!canEdit} className="contents">
          <FormField label={t('mfg.bom.baseQuantity')}><Input type="number" step="any" min={0} {...header.register('baseQuantity', { required: true })} /></FormField>
          <FormField label={t('mfg.bom.yield')}><Input type="number" step="0.01" min={0} max={100} {...header.register('yieldPct', { required: true })} /></FormField>
          <FormField label={t('mfg.bom.validFrom')}><Input type="date" {...header.register('effectiveFrom', { required: true })} /></FormField>
          <FormField label={t('mfg.bom.validTo')}><Input type="date" {...header.register('effectiveTo')} /></FormField>
          {canEdit && <div className="flex items-end"><Button type="submit" variant="secondary" loading={saveHeader.isPending}>{t('common.save')}</Button></div>}
        </fieldset>
        {bom.approvedBy && <p className="text-xs text-ink-secondary sm:col-span-5">{t('mfg.bom.approvedBy', { by: bom.approvedBy, at: bom.approvedAt?.slice(0, 16).replace('T', ' ') })}</p>}
      </form>

      <TabList id="mfg-bom" label={t('mfg.nav.boms')} current={tab} onSelect={setTab}
        tabs={[{ key: 'lines', label: t('mfg.bom.lines') }, { key: 'tree', label: t('mfg.bom.tree') }, { key: 'explosion', label: t('mfg.bom.explosion') }, { key: 'versions', label: t('mfg.bom.versions') }]} />
      <div role="tabpanel">
        {tab === 'lines' && <LinesTab bom={bom} canEdit={canEdit} />}
        {tab === 'tree' && <TreeTab bom={bom} />}
        {tab === 'explosion' && <ExplosionTab bom={bom} />}
        {tab === 'versions' && <VersionsTab bom={bom} />}
      </div>

      <ConfirmDialog
        open={!!action}
        danger={action === 'delete' || action === 'obsolete'}
        title={action ? t(`mfg.bom.${action}`) : ''}
        message={action ? t(askText[action], { code: bom.itemCode, version: bom.version }) : ''}
        onConfirm={() => action && run.mutate(action)}
        onCancel={() => setAction(null)}
      />
    </div>
  )
}

// ── Where used ────────────────────────────────────────────────────────────────────────────────────────────
export function WhereUsedPage() {
  const { t } = useTranslation()
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [params] = useSearchParams()
  const [itemId, setItemId] = useState<string | null>(params.get('itemId'))
  const [picker, setPicker] = useState(false)
  const [label, setLabel] = useState('')
  const { data = [], isLoading, error } = useQuery({ queryKey: ['mfg', 'where-used', plantId, itemId], queryFn: () => mfgApi.whereUsed(plantId!, itemId!), enabled: !!plantId && !!itemId })
  const { data: boms = [] } = useQuery({ queryKey: ['mfg', 'boms', plantId, 'approved'], queryFn: () => mfgApi.listBoms(plantId!, undefined, 'Approved'), enabled: !!plantId })
  const bomOf = (code: string, version: number) => boms.find((b) => b.itemCode === code && b.version === version)

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.whereUsed')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.whereUsed')} hint={t('mfg.bom.whereUsedHint')} actions={<><PlantSelector /><Button onClick={() => setPicker(true)}>{t('mfg.bom.pickItem')}</Button></>} />
      {label && <p className="mb-3 text-sm font-medium">{label}</p>}
      {error && <Alert variant="danger">{serverMessage(error, t('mfg.bom.treeFailed'))}</Alert>}
      {!itemId ? <Alert variant="info">{t('mfg.bom.whereUsedPick')}</Alert> : (
        <DataTable
          data={data.map((r, i) => ({ id: String(i), ...r }))}
          isLoading={isLoading}
          emptyMessage={t('mfg.bom.whereUsedNone')}
          columns={[
            { key: 'level', label: t('mfg.bom.level'), align: 'right' as const },
            { key: 'parent', label: t('mfg.bom.parent'), render: (r) => { const b = bomOf(r.parentCode, r.bomVersion); return <span><span className="font-mono text-xs">{b ? <Link className="text-brand-action hover:underline" to={`/manufacturing/boms/${b.id}`}>{r.parentCode}</Link> : r.parentCode}</span> {r.parentName}</span> } },
            { key: 'bomVersion', label: t('mfg.bom.version'), align: 'right' as const, render: (r) => `v${r.bomVersion}` },
            { key: 'quantityPer', label: t('mfg.bom.quantityPer'), align: 'right' as const, render: (r) => fmtQty(r.quantityPer) },
          ]}
        />
      )}
      <ProductPickerModal open={picker} multiple={false} onClose={() => setPicker(false)} onConfirm={(p) => { setPicker(false); if (p[0]) { setItemId(p[0].id); setLabel(`${p[0].sku} ${p[0].name}`) } }} />
    </div>
  )
}
