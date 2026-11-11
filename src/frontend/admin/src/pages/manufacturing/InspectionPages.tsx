import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle, CheckCircle2 } from 'lucide-react'
import { mfgApi, type Inspection, type InspectionCharacteristic, type InspectionListItem, type InspectionResultBody, type InspectionStatus, type InspectionType } from '@/api/manufacturing/mfg.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { NoPlant, PageHeader, PlantSelector, fmtDateTime, fmtQty, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Badge, Button, FormField, Input, Select, Skeleton, useToast } from '@/components/ui'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const statusVariant = { Pending: 'warning', Passed: 'success', Failed: 'danger', Cancelled: 'muted' } as const

export function InspectionStatusBadge({ status }: { status: InspectionStatus }) {
  const { t } = useTranslation()
  return <Badge variant={statusVariant[status]}>{t(`mfg.qc.insp.status.${status}`)}</Badge>
}

// ── Worklist (S22) ────────────────────────────────────────────────────────────────────────────────────────
export function InspectionListPage() {
  const { t } = useTranslation()
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [status, setStatus] = useState<InspectionStatus | ''>('Pending')
  const [type, setType] = useState<InspectionType | ''>('')
  const [search, setSearch] = useState('')
  const { page, pageSize, setPage } = usePagination()
  const { data, isLoading } = useQuery({
    queryKey: ['mfg', 'inspections', plantId, status, type, search, page, pageSize],
    queryFn: () => mfgApi.listInspections(plantId!, { status: status || undefined, type: type || undefined, search: search || undefined, page, pageSize }),
    enabled: !!plantId,
  })

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.inspections')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.inspections')} hint={t('mfg.qc.insp.hint')} actions={<PlantSelector />} />
      <div className="mb-3 flex flex-wrap gap-3">
        <div className="w-72"><Input type="search" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} /></div>
        <Select className="w-44" aria-label={t('common.status')} value={status} onChange={(e) => { setStatus(e.target.value as InspectionStatus | ''); setPage(1) }}>
          <option value="">{t('common.all')}</option>
          {(['Pending', 'Passed', 'Failed', 'Cancelled'] as InspectionStatus[]).map((s) => <option key={s} value={s}>{t(`mfg.qc.insp.status.${s}`)}</option>)}
        </Select>
        <Select className="w-44" aria-label={t('mfg.qc.plan.type')} value={type} onChange={(e) => { setType(e.target.value as InspectionType | ''); setPage(1) }}>
          <option value="">{t('common.all')}</option>
          {(['Fqc', 'Ipqc'] as InspectionType[]).map((x) => <option key={x} value={x}>{t(`mfg.qc.type.${x}`)}</option>)}
        </Select>
      </div>
      <DataTable
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyMessage={t('mfg.qc.insp.empty')}
        columns={[
          { key: 'docNo', label: t('mfg.qc.insp.docNo'), render: (i: InspectionListItem) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/inspections/${i.id}`}>{i.docNo}</Link> },
          { key: 'type', label: t('mfg.qc.plan.type'), render: (i: InspectionListItem) => <Badge variant="info">{t(`mfg.qc.type.${i.type}`)}</Badge> },
          { key: 'order', label: t('mfg.order.orderNo'), render: (i: InspectionListItem) => <Link className="text-brand-action hover:underline" to={`/manufacturing/orders/${i.orderId}`}>{i.orderNo}</Link> },
          { key: 'item', label: t('mfg.qc.plan.item'), render: (i: InspectionListItem) => <span><span className="font-mono text-xs">{i.itemCode}</span>{i.operationSeq != null ? ` · ${t('mfg.qc.insp.operation', { seq: i.operationSeq })}` : ''}</span> },
          { key: 'lot', label: t('mfg.qc.insp.lot'), render: (i: InspectionListItem) => <span className="font-mono text-xs">{i.lotNumber ?? ''}</span> },
          { key: 'lotQty', label: t('mfg.qc.insp.lotQty'), align: 'right' as const, render: (i: InspectionListItem) => fmtQty(i.lotQty) },
          { key: 'sampleQty', label: t('mfg.qc.insp.sampleQty'), align: 'right' as const, render: (i: InspectionListItem) => fmtQty(i.sampleQty) },
          { key: 'status', label: t('common.status'), render: (i: InspectionListItem) => <span className="flex items-center gap-2"><InspectionStatusBadge status={i.status} />{i.ncrRequired && <Badge variant="danger">{t('mfg.qc.insp.ncr')}</Badge>}</span> },
        ]}
      />
      {data && <Pagination page={page} pageSize={pageSize} totalCount={data.total} onPageChange={setPage} />}
    </div>
  )
}

// ── Inspection document (S22) ──────────────────────────────────────────────────────────────────────────────
const key = (characteristicId: string, sampleNo: number) => `${characteristicId}:${sampleNo}`

function outOfRange(c: InspectionCharacteristic, raw: string) {
  if (raw === '' || Number.isNaN(Number(raw))) return false
  const v = Number(raw)
  return (c.minValue != null && v < c.minValue) || (c.maxValue != null && v > c.maxValue)
}

export function InspectionPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canInspect = hasPermission('manufacturing:qc:inspect')
  const canReinspect = hasPermission('manufacturing:qc:reinspect')
  const [values, setValues] = useState<Record<string, string>>({})
  const [confirm, setConfirm] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { data, isLoading } = useQuery({ queryKey: ['mfg', 'inspection', id], queryFn: () => mfgApi.getInspection(id) })

  // the saved values fill the grid whenever the document is (re)loaded
  useEffect(() => {
    if (!data) return
    const next: Record<string, string> = {}
    const byId = new Map(data.characteristics.map((c) => [c.id, c]))
    for (const r of data.results) {
      const c = byId.get(r.characteristicId)
      next[key(r.characteristicId, r.sampleNo)] = c?.kind === 'Measurement' ? String(r.measuredValue ?? '') : r.attributePassed ? 'pass' : r.attributePassed === false ? 'fail' : ''
    }
    setValues(next)
  }, [data])

  const body = (d: Inspection): InspectionResultBody[] => {
    const out: InspectionResultBody[] = []
    for (const c of d.characteristics) {
      if (c.kind === 'Measurement') {
        for (let no = 1; no <= d.units; no++) {
          const raw = values[key(c.id, no)] ?? ''
          if (raw !== '' && !Number.isNaN(Number(raw))) out.push({ characteristicId: c.id, sampleNo: no, value: Number(raw) })
        }
      } else {
        const raw = values[key(c.id, 1)] ?? ''
        if (raw === 'pass' || raw === 'fail') out.push({ characteristicId: c.id, sampleNo: 1, passed: raw === 'pass' })
      }
    }
    return out
  }

  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'inspection', id] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'inspections'] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'order'] }) }
  const save = useMutation({
    mutationFn: () => mfgApi.saveInspectionResults(id, body(data!), data!.inspection.sampleQty),
    onSuccess: () => { setError(null); toast.success(t('mfg.qc.insp.draftSaved')); refresh() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const submit = useMutation({
    mutationFn: () => mfgApi.submitInspection(id, body(data!), data!.inspection.sampleQty),
    onSuccess: (d) => { setConfirm(false); setError(null); toast.success(t(d.inspection.status === 'Passed' ? 'mfg.qc.insp.passed' : 'mfg.qc.insp.failed')); refresh() },
    onError: (e) => { setConfirm(false); setError(serverMessage(e, t('common.failedToSave'))) },
  })
  const again = useMutation({
    mutationFn: () => mfgApi.reinspect(id),
    onSuccess: (d) => { toast.success(t('mfg.qc.insp.reinspectCreated', { doc: d.inspection.docNo })); refresh(); navigate(`/manufacturing/inspections/${d.inspection.id}`) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  const total = useMemo(() => (data ? data.characteristics.length : 0), [data])
  if (isLoading || !data) return <Skeleton className="h-64 w-full" />
  const i = data.inspection
  const editable = i.status === 'Pending' && canInspect

  return (
    <div className="space-y-4">
      <PageHeader
        title={i.docNo}
        hint={`${i.itemCode} ${i.itemName}`}
        actions={
          <>
            <InspectionStatusBadge status={i.status} />
            <Link to="/manufacturing/inspections" className="text-sm text-brand-action underline">{t('mfg.qc.insp.backToList')}</Link>
            {i.status === 'Failed' && !i.reinspectedById && canReinspect && <Button variant="secondary" loading={again.isPending} onClick={() => again.mutate()}>{t('mfg.qc.insp.reinspect')}</Button>}
          </>
        }
      />
      {error && <Alert variant="danger" onClose={() => setError(null)}>{error}</Alert>}
      {i.status === 'Failed' && (
        <Alert variant="danger" title={t('mfg.qc.insp.failedTitle')}>
          {i.ncrRequired ? t('mfg.qc.insp.failedNcr') : ''} {i.reinspectedById ? t('mfg.qc.insp.reinspected') : t('mfg.qc.insp.failedStays')}
        </Alert>
      )}
      {i.status === 'Passed' && <Alert variant="success">{i.type === 'Fqc' ? t('mfg.qc.insp.passedFqc') : t('mfg.qc.insp.passedIpqc', { qty: fmtQty(i.lotQty) })}</Alert>}
      {i.status === 'Cancelled' && <Alert variant="info">{t('mfg.qc.insp.cancelled')}</Alert>}
      {i.status === 'Pending' && !canInspect && <Alert variant="info">{t('mfg.qc.insp.noRight')}</Alert>}

      <dl className="grid gap-3 rounded-lg border border-line bg-surface p-4 sm:grid-cols-4" aria-label={t('mfg.qc.insp.header')}>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.qc.plan.type')}</dt><dd>{t(`mfg.qc.type.${i.type}`)}</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.order.orderNo')}</dt><dd><Link className="text-brand-action hover:underline" to={`/manufacturing/orders/${i.orderId}`}>{i.orderNo}</Link></dd></div>
        <div><dt className="text-xs text-ink-secondary">{i.lotNumber ? t('mfg.qc.insp.lot') : t('mfg.qc.insp.operationLabel')}</dt><dd className="font-mono text-sm">{i.lotNumber ?? (i.operationSeq != null ? t('mfg.qc.insp.operation', { seq: i.operationSeq }) : '')}</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.qc.insp.plan')}</dt><dd className="font-mono text-sm">{i.planCode} v{i.planVersion}</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.qc.insp.lotQty')}</dt><dd>{fmtQty(i.lotQty)}</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.qc.insp.sampleQty')}</dt><dd>{fmtQty(i.sampleQty)} ({t('mfg.qc.insp.unitsNeeded', { count: data.units })})</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.qc.plan.allowedMinor')}</dt><dd>{data.allowedMinorDefects}</dd></div>
        {i.inspectedBy && <div><dt className="text-xs text-ink-secondary">{t('mfg.qc.insp.inspectedBy')}</dt><dd>{i.inspectedBy} · {fmtDateTime(i.inspectedAt)}</dd></div>}
      </dl>

      <section aria-labelledby="qc-results">
        <h2 id="qc-results" className="mb-2 text-sm font-semibold text-ink">{t('mfg.qc.insp.results')} ({total})</h2>
        <div className="space-y-3">
          {data.characteristics.map((c) => (
            <fieldset key={c.id} className="rounded-lg border border-line bg-surface p-3" disabled={!editable}>
              <legend className="px-1 text-sm font-medium text-ink">
                {c.seq} · {c.name} {c.isCritical ? <Badge variant="danger" className="ml-1">{t('mfg.qc.plan.critical')}</Badge> : <Badge variant="muted" className="ml-1">{t('mfg.qc.plan.minor')}</Badge>}
                {c.kind === 'Measurement' && <span className="ml-2 text-xs font-normal text-ink-secondary">{c.minValue ?? '…'} – {c.maxValue ?? '…'} {c.unit ?? ''}</span>}
              </legend>
              {c.kind === 'Measurement' ? (
                <div className="flex flex-wrap gap-3">
                  {Array.from({ length: data.units }, (_, n) => n + 1).map((no) => {
                    const raw = values[key(c.id, no)] ?? ''
                    const bad = outOfRange(c, raw)
                    return (
                      <FormField key={no} label={t('mfg.qc.insp.sampleUnit', { no })} className="w-28">
                        <Input type="number" step="any" value={raw} onChange={(e) => setValues((v) => ({ ...v, [key(c.id, no)]: e.target.value }))} aria-invalid={bad || undefined} />
                        {raw !== '' && (bad
                          ? <span className="mt-1 flex items-center gap-1 text-xs text-danger-soft-text"><AlertTriangle className="h-3 w-3" aria-hidden="true" />{t('mfg.qc.insp.outOfRange')}</span>
                          : <span className="mt-1 flex items-center gap-1 text-xs text-success-soft-text"><CheckCircle2 className="h-3 w-3" aria-hidden="true" />{t('mfg.qc.insp.inRange')}</span>)}
                      </FormField>
                    )
                  })}
                </div>
              ) : (
                <div className="flex gap-6" role="radiogroup" aria-label={c.name}>
                  {(['pass', 'fail'] as const).map((opt) => (
                    <label key={opt} className="flex items-center gap-2 text-sm">
                      <input type="radio" name={`attr-${c.id}`} value={opt} checked={(values[key(c.id, 1)] ?? '') === opt} onChange={() => setValues((v) => ({ ...v, [key(c.id, 1)]: opt }))} />
                      {t(`mfg.qc.insp.${opt}`)}
                    </label>
                  ))}
                </div>
              )}
            </fieldset>
          ))}
        </div>
      </section>

      {editable && (
        <div className="flex justify-end gap-3">
          <Button variant="outline" loading={save.isPending} onClick={() => save.mutate()}>{t('mfg.qc.insp.saveDraft')}</Button>
          <Button loading={submit.isPending} onClick={() => setConfirm(true)}>{t('mfg.qc.insp.submit')}</Button>
        </div>
      )}
      <ConfirmDialog
        open={confirm}
        title={t('mfg.qc.insp.submit')}
        message={t(i.type === 'Fqc' ? 'mfg.qc.insp.submitFqc' : 'mfg.qc.insp.submitIpqc', { doc: i.docNo })}
        confirmLabel={t('mfg.qc.insp.submit')}
        onConfirm={() => submit.mutate()}
        onCancel={() => setConfirm(false)}
      />
    </div>
  )
}
