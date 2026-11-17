import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2 } from 'lucide-react'
import { mfgApi, type DispositionBody, type DispositionType, type Ncr, type NcrDisposition, type NcrListItem, type NcrSourceType, type NcrStatus } from '@/api/manufacturing/mfg.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { NoPlant, PageHeader, PlantSelector, fmtDateTime, fmtQty, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Badge, Button, FormField, Input, Select, Skeleton, useToast } from '@/components/ui'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const statusVariant = { Open: 'warning', InDisposition: 'info', Closed: 'success', Cancelled: 'muted' } as const

export function NcrStatusBadge({ status }: { status: NcrStatus }) {
  const { t } = useTranslation()
  return <Badge variant={statusVariant[status]}>{t(`mfg.ncr.status.${status}`)}</Badge>
}

// ── Worklist (S23) ────────────────────────────────────────────────────────────────────────────────────────
export function NcrListPage() {
  const { t } = useTranslation()
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [status, setStatus] = useState<NcrStatus | 'open' | ''>('open')
  const [source, setSource] = useState<NcrSourceType | ''>('')
  const [search, setSearch] = useState('')
  const { page, pageSize, setPage } = usePagination()
  const { data, isLoading } = useQuery({
    queryKey: ['mfg', 'ncrs', plantId, status, source, search, page, pageSize],
    queryFn: () => mfgApi.listNcrs(plantId!, { openOnly: status === 'open' || undefined, status: status && status !== 'open' ? status : undefined, source: source || undefined, search: search || undefined, page, pageSize }),
    enabled: !!plantId,
  })

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.ncrs')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.ncrs')} hint={t('mfg.ncr.hint')} actions={<PlantSelector />} />
      <div className="mb-3 flex flex-wrap gap-3">
        <div className="w-72"><Input type="search" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} /></div>
        <Select className="w-52" aria-label={t('common.status')} value={status} onChange={(e) => { setStatus(e.target.value as NcrStatus | 'open' | ''); setPage(1) }}>
          <option value="open">{t('mfg.ncr.openOnes')}</option>
          <option value="">{t('common.all')}</option>
          {(['Open', 'InDisposition', 'Closed', 'Cancelled'] as NcrStatus[]).map((s) => <option key={s} value={s}>{t(`mfg.ncr.status.${s}`)}</option>)}
        </Select>
        <Select className="w-52" aria-label={t('mfg.ncr.source')} value={source} onChange={(e) => { setSource(e.target.value as NcrSourceType | ''); setPage(1) }}>
          <option value="">{t('common.all')}</option>
          {(['Inspection', 'OperationReport'] as NcrSourceType[]).map((x) => <option key={x} value={x}>{t(`mfg.ncr.sourceType.${x}`)}</option>)}
        </Select>
      </div>
      <DataTable
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyMessage={t('mfg.ncr.empty')}
        columns={[
          { key: 'ncrNo', label: t('mfg.ncr.ncrNo'), render: (n: NcrListItem) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/ncrs/${n.id}`}>{n.ncrNo}</Link> },
          { key: 'source', label: t('mfg.ncr.source'), render: (n: NcrListItem) => <Badge variant="info">{t(`mfg.ncr.sourceType.${n.sourceType}`)}</Badge> },
          { key: 'order', label: t('mfg.order.orderNo'), render: (n: NcrListItem) => <Link className="text-brand-action hover:underline" to={`/manufacturing/orders/${n.orderId}`}>{n.orderNo}</Link> },
          { key: 'item', label: t('mfg.qc.plan.item'), render: (n: NcrListItem) => <span><span className="font-mono text-xs">{n.itemCode}</span>{n.operationSeq != null ? ` · ${t('mfg.qc.insp.operation', { seq: n.operationSeq })}` : ''}</span> },
          { key: 'lot', label: t('mfg.qc.insp.lot'), render: (n: NcrListItem) => <span className="font-mono text-xs">{n.lotNumber ?? ''}</span> },
          { key: 'qty', label: t('mfg.ncr.qty'), align: 'right' as const, render: (n: NcrListItem) => fmtQty(n.nonconformingQty) },
          { key: 'status', label: t('common.status'), render: (n: NcrListItem) => <NcrStatusBadge status={n.status} /> },
        ]}
      />
      {data && <Pagination page={page} pageSize={pageSize} totalCount={data.total} onPageChange={setPage} />}
    </div>
  )
}

// ── The NCR document (S23) ───────────────────────────────────────────────────────────────────────────────────
interface Row { type: DispositionType; qty: string; reasonCodeId: string; note: string }

const toRow = (d: NcrDisposition): Row => ({ type: d.type, qty: String(d.qty), reasonCodeId: d.reasonCodeId ?? '', note: d.note ?? '' })
const toBody = (r: Row): DispositionBody => ({ type: r.type, qty: Number(r.qty), reasonCodeId: r.type === 'Scrap' ? r.reasonCodeId || null : null, note: r.note.trim() || null })

export function NcrPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canDispose = hasPermission('manufacturing:qc:disposition')
  const canApprove = hasPermission('manufacturing:qc:approve-deviation')
  const [rows, setRows] = useState<Row[]>([])
  const [error, setError] = useState<string | null>(null)
  const [executing, setExecuting] = useState<NcrDisposition | null>(null)
  const { data, isLoading } = useQuery({ queryKey: ['mfg', 'ncr', id], queryFn: () => mfgApi.getNcr(id) })
  const { data: reasons = [] } = useQuery({ queryKey: ['mfg', 'reasons'], queryFn: () => mfgApi.listReasons(), staleTime: 30_000 })
  const scrapReasons = reasons.filter((r) => r.type === 'Scrap' && r.isActive)

  useEffect(() => { if (data) setRows(data.dispositions.map(toRow)) }, [data])

  const refresh = (answer?: Ncr) => {
    if (answer) queryClient.setQueryData(['mfg', 'ncr', id], answer)
    queryClient.invalidateQueries({ queryKey: ['mfg', 'ncr', id] })
    queryClient.invalidateQueries({ queryKey: ['mfg', 'ncrs'] })
    queryClient.invalidateQueries({ queryKey: ['mfg', 'order'] })
    queryClient.invalidateQueries({ queryKey: ['mfg', 'inspection'] })
  }
  const save = useMutation({
    mutationFn: () => mfgApi.saveNcrDispositions(id, rows.map(toBody)),
    onSuccess: (a) => { setError(null); toast.success(t('mfg.ncr.saved')); refresh(a) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const execute = useMutation({
    mutationFn: (d: NcrDisposition) => mfgApi.executeDisposition(id, d.lineNo),
    onSuccess: (a) => {
      setExecuting(null); setError(null)
      toast.success(a.reworkOrderNo ? t('mfg.ncr.reworkCreated', { order: a.reworkOrderNo }) : t('mfg.ncr.executed'))
      refresh(a.ncr)
    },
    onError: (e) => { setExecuting(null); setError(serverMessage(e, t('common.failedToSave'))) },
  })

  const total = data?.ncr.nonconformingQty ?? 0
  const sum = useMemo(() => rows.reduce((acc, r) => acc + (Number(r.qty) > 0 ? Number(r.qty) : 0), 0), [rows])
  const dirty = useMemo(() => !!data && JSON.stringify(rows) !== JSON.stringify(data.dispositions.map(toRow)), [rows, data])
  if (isLoading || !data) return <Skeleton className="h-64 w-full" />

  const n = data.ncr
  const open = n.status === 'Open' || n.status === 'InDisposition'
  const started = data.dispositions.some((d) => d.isStarted)
  const editable = open && !started && canDispose
  const rowsValid = rows.length > 0 && rows.every((r) => Number(r.qty) > 0 && (r.type !== 'Scrap' || r.reasonCodeId)) && Math.abs(sum - total) < 0.0000005
  const needsApproval = rows.some((r) => r.type === 'UseAsIs') && !canApprove
  const types: DispositionType[] = n.isInProcess ? ['Scrap', 'UseAsIs'] : ['Rework', 'Scrap', 'UseAsIs']
  const diff = total - sum

  const confirmText = (d: NcrDisposition) => {
    const lot = n.lotNumber ?? ''
    if (n.isRecordOnly) return t('mfg.ncr.executeRecord', { qty: fmtQty(d.qty) })
    if (n.isInProcess) return t(d.type === 'Scrap' ? 'mfg.ncr.executeInProcessScrap' : 'mfg.ncr.executeInProcessUseAsIs', { qty: fmtQty(d.qty), seq: n.operationSeq })
    if (d.type === 'Scrap') return t('mfg.ncr.executeScrap', { qty: fmtQty(d.qty), lot })
    if (d.type === 'UseAsIs') return t('mfg.ncr.executeUseAsIs', { qty: fmtQty(d.qty), lot, by: d.approvedBy ?? '' })
    return t('mfg.ncr.executeRework', { qty: fmtQty(d.qty), lot })
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={n.ncrNo}
        hint={`${n.itemCode} ${n.itemName}`}
        actions={<><NcrStatusBadge status={n.status} /><Link to="/manufacturing/ncrs" className="text-sm text-brand-action underline">{t('mfg.ncr.backToList')}</Link></>}
      />
      {error && <Alert variant="danger" onClose={() => setError(null)}>{error}</Alert>}
      {n.isRecordOnly && <Alert variant="info">{t('mfg.ncr.recordOnly')}</Alert>}
      {n.isInProcess && <Alert variant="info">{t('mfg.ncr.inProcess')}</Alert>}
      {n.status === 'Cancelled' && <Alert variant="info">{t('mfg.ncr.cancelled')}</Alert>}
      {n.status === 'Closed' && <Alert variant="success">{t('mfg.ncr.closed', { when: fmtDateTime(n.closedAt) })}</Alert>}
      {open && !canDispose && <Alert variant="info">{t('mfg.ncr.noRight')}</Alert>}

      <dl className="grid gap-3 rounded-lg border border-line bg-surface p-4 sm:grid-cols-4" aria-label={t('mfg.ncr.header')}>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.ncr.source')}</dt><dd>{t(`mfg.ncr.sourceType.${n.sourceType}`)}</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.order.orderNo')}</dt><dd><Link className="text-brand-action hover:underline" to={`/manufacturing/orders/${n.orderId}`}>{n.orderNo}</Link></dd></div>
        <div><dt className="text-xs text-ink-secondary">{n.lotNumber ? t('mfg.qc.insp.lot') : t('mfg.qc.insp.operationLabel')}</dt><dd className="font-mono text-sm">{n.lotNumber ?? (n.operationSeq != null ? t('mfg.qc.insp.operation', { seq: n.operationSeq }) : '')}</dd></div>
        <div><dt className="text-xs text-ink-secondary">{t('mfg.ncr.qty')}</dt><dd>{fmtQty(n.nonconformingQty)}</dd></div>
        {n.inspectionId && <div><dt className="text-xs text-ink-secondary">{t('mfg.ncr.inspection')}</dt><dd><Link className="text-brand-action hover:underline" to={`/manufacturing/inspections/${n.inspectionId}`}>{t('mfg.ncr.openInspection')}</Link></dd></div>}
        <div className="sm:col-span-3"><dt className="text-xs text-ink-secondary">{t('mfg.ncr.description')}</dt><dd>{n.description ?? ''}</dd></div>
      </dl>

      <section aria-labelledby="ncr-lines">
        <h2 id="ncr-lines" className="mb-2 text-sm font-semibold text-ink">{t('mfg.ncr.dispositions')}</h2>
        {open && !started && <p className="mb-2 text-sm text-ink-secondary">{t('mfg.ncr.dispositionsHint')}</p>}
        {started && open && <Alert variant="info">{t('mfg.ncr.started')}</Alert>}
        <ul className="space-y-3">
          {rows.map((r, i) => {
            const saved = data.dispositions[i]
            return (
              <li key={i} className="rounded-lg border border-line bg-surface p-3">
                <div className="grid gap-3 sm:grid-cols-[1.2fr_1fr_1.6fr_2fr_auto] sm:items-end">
                  <FormField label={t('mfg.ncr.type')}>
                    <Select value={r.type} disabled={!editable} onChange={(e) => setRows((all) => all.map((x, k) => (k === i ? { ...x, type: e.target.value as DispositionType } : x)))}>
                      {types.map((x) => <option key={x} value={x}>{t(`mfg.ncr.disposition.${x}`)}</option>)}
                    </Select>
                  </FormField>
                  <FormField label={t('mfg.issue.quantity')}>
                    <Input type="number" step="any" min="0" value={r.qty} disabled={!editable} onChange={(e) => setRows((all) => all.map((x, k) => (k === i ? { ...x, qty: e.target.value } : x)))} />
                  </FormField>
                  {r.type === 'Scrap' ? (
                    <FormField label={t('mfg.ncr.scrapReason')} required>
                      <Select value={r.reasonCodeId} disabled={!editable} onChange={(e) => setRows((all) => all.map((x, k) => (k === i ? { ...x, reasonCodeId: e.target.value } : x)))}>
                        <option value="">{t('common.select')}</option>
                        {scrapReasons.map((x) => <option key={x.id} value={x.id}>{x.code} - {x.name}</option>)}
                      </Select>
                    </FormField>
                  ) : <div />}
                  <FormField label={t('mfg.reasonDialog.note')}>
                    <Input value={r.note} maxLength={500} disabled={!editable} onChange={(e) => setRows((all) => all.map((x, k) => (k === i ? { ...x, note: e.target.value } : x)))} />
                  </FormField>
                  {editable && <Button type="button" size="sm" variant="ghost" iconOnly aria-label={t('mfg.ncr.removeLine', { n: i + 1 })} onClick={() => setRows((all) => all.filter((_, k) => k !== i))}><Trash2 size={16} aria-hidden="true" /></Button>}
                </div>
                {saved && !dirty && (
                  <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
                    <LineStatus line={saved} />
                    {saved.type === 'UseAsIs' && saved.approvedBy && <span className="text-ink-secondary">{t('mfg.ncr.approvedBy', { by: saved.approvedBy })}</span>}
                    {saved.resultLotNumber && <span className="text-ink-secondary">{t('mfg.ncr.resultLot', { lot: saved.resultLotNumber })}</span>}
                    {saved.reworkOrderId && <Link className="text-brand-action hover:underline" to={`/manufacturing/orders/${saved.reworkOrderId}`}>{t('mfg.ncr.reworkOrder', { order: saved.reworkOrderNo ?? '' })}</Link>}
                    {open && canDispose && !saved.isExecuted && !saved.reworkOrderId && n.status === 'InDisposition' && (
                      <Button size="sm" variant="secondary" onClick={() => { setError(null); setExecuting(saved) }}>{t('mfg.ncr.execute')}</Button>
                    )}
                  </div>
                )}
              </li>
            )
          })}
        </ul>
        {editable && (
          <div className="mt-3 flex flex-wrap items-center gap-3">
            <Button type="button" variant="outline" size="sm" onClick={() => setRows((all) => [...all, { type: types[0], qty: diff > 0 ? String(Math.round(diff * 1e6) / 1e6) : '', reasonCodeId: '', note: '' }])}><Plus size={14} aria-hidden="true" /> {t('mfg.ncr.addLine')}</Button>
            <span role="status" className={Math.abs(diff) < 0.0000005 ? 'text-sm text-success-soft-text' : 'text-sm text-danger-soft-text'}>
              {Math.abs(diff) < 0.0000005 ? t('mfg.ncr.sumOk', { total: fmtQty(total) }) : diff > 0 ? t('mfg.ncr.sumMissing', { sum: fmtQty(sum), total: fmtQty(total), missing: fmtQty(diff) }) : t('mfg.ncr.sumOver', { sum: fmtQty(sum), total: fmtQty(total), over: fmtQty(-diff) })}
            </span>
          </div>
        )}
        {editable && needsApproval && <Alert variant="warning" className="mt-3">{t('mfg.ncr.needApproval')}</Alert>}
      </section>

      {editable && (
        <div className="flex justify-end">
          <Button loading={save.isPending} disabled={!rowsValid || needsApproval} onClick={() => save.mutate()}>{t('mfg.ncr.save')}</Button>
        </div>
      )}
      {dirty && editable && data.dispositions.length > 0 && <p className="text-sm text-ink-secondary">{t('mfg.ncr.saveFirst')}</p>}

      <ConfirmDialog
        open={!!executing}
        title={t('mfg.ncr.executeTitle', { line: executing?.lineNo ?? '' })}
        message={executing ? confirmText(executing) : ''}
        confirmLabel={t('mfg.ncr.execute')}
        danger={executing?.type === 'Scrap'}
        onConfirm={() => executing && execute.mutate(executing)}
        onCancel={() => setExecuting(null)}
      />
    </div>
  )
}

function LineStatus({ line }: { line: NcrDisposition }) {
  const { t } = useTranslation()
  if (line.isExecuted) return <Badge variant="success">{t('mfg.ncr.lineExecuted')}</Badge>
  if (line.isStarted) return <Badge variant="info">{t('mfg.ncr.lineStarted')}</Badge>
  return <Badge variant="warning">{t('mfg.ncr.linePending')}</Badge>
}
