import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type MaterialAvailability, type OrderListItem, type OrderStatus } from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { Alert, Button, Checkbox, Input, Modal, Progress, Select } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { usePermission } from '@/hooks/usePermission'
import { ORDER_PERMISSION, canBulkRelease, errorCodeOf } from '@/components/manufacturing/orderActions'
import { LateFlag, OrderStatusBadge } from '@/components/manufacturing/OrderBadges'
import { OrderFormModal } from '@/components/manufacturing/OrderFormModal'
import { NoPlant, PageHeader, PlantSelector, fmtDate, fmtQty, usePlantContext } from '@/components/manufacturing/shared'

const STATUSES: OrderStatus[] = ['Draft', 'Released', 'InProgress', 'OnHold', 'Completed', 'Closed', 'Cancelled']
const PAGE_SIZE = 25
const MAX_BULK = 20

interface BulkResult { orderNo: string; ok: boolean; shortages: MaterialAvailability[]; message?: string; code?: string | null }

/** S15 - the production orders of the plant: filters, status, late flag, progress, and releasing several Draft orders at once. */
export function ProductionOrderListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const { plant, plantId, isLoading: plantsLoading } = usePlantContext()
  const [status, setStatus] = useState<OrderStatus | ''>('')
  const [lateOnly, setLateOnly] = useState(false)
  const [priority, setPriority] = useState('')
  const [dueFrom, setDueFrom] = useState('')
  const [dueTo, setDueTo] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [results, setResults] = useState<BulkResult[] | null>(null)
  const [releasing, setReleasing] = useState(false)

  const filter = { plantId: plantId ?? '', status: status || undefined, isLate: lateOnly ? true : undefined, priority: priority ? Number(priority) : undefined, dueFrom: dueFrom || undefined, dueTo: dueTo || undefined, search: search.trim() || undefined, page, pageSize: PAGE_SIZE }
  const { data, isLoading, isError } = useQuery({ queryKey: ['mfg', 'orders', filter], queryFn: () => mfgApi.listOrders(filter), enabled: !!plantId })
  const canCreate = hasPermission(ORDER_PERMISSION.create)
  const canRelease = hasPermission(ORDER_PERMISSION.release)
  const rows = data?.items ?? []

  const reset = <T,>(set: (v: T) => void) => (v: T) => { set(v); setPage(1); setSelected(new Set()) }
  const toggle = (id: string) => setSelected((s) => { const n = new Set(s); if (n.has(id)) n.delete(id); else if (n.size < MAX_BULK) n.add(id); return n })

  const releaseSelected = async () => {
    setReleasing(true)
    const out: BulkResult[] = []
    for (const row of rows.filter((r) => selected.has(r.id))) {
      try {
        const answer = await mfgApi.releaseOrder(row.id)
        out.push({ orderNo: row.orderNo, ok: true, shortages: answer.shortages ?? [] })
      } catch (e) {
        out.push({ orderNo: row.orderNo, ok: false, shortages: [], message: serverMessage(e, t('mfg.order.bulk.failed')), code: errorCodeOf(e) })
      }
    }
    setResults(out)
    setSelected(new Set())
    setReleasing(false)
    queryClient.invalidateQueries({ queryKey: ['mfg', 'orders'] })
  }

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.orders')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader
        title={t('mfg.nav.orders')}
        hint={t('mfg.order.hint')}
        actions={<><PlantSelector />{canCreate && <Button onClick={() => setCreating(true)}>{t('mfg.order.new')}</Button>}</>}
      />
      <div className="mb-3 flex flex-wrap items-end gap-3">
        <div className="w-64"><Input type="search" placeholder={t('mfg.order.searchHint')} aria-label={t('common.search')} value={search} onChange={(e) => reset(setSearch)(e.target.value)} /></div>
        <Select className="w-44" aria-label={t('common.status')} value={status} onChange={(e) => reset(setStatus)(e.target.value as OrderStatus | '')}>
          <option value="">{t('common.all')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{t(`mfg.orderStatus.${s}`)}</option>)}
        </Select>
        <Select className="w-36" aria-label={t('mfg.order.priority')} value={priority} onChange={(e) => reset(setPriority)(e.target.value)}>
          <option value="">{t('mfg.order.anyPriority')}</option>
          {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{t('mfg.order.priorityN', { n })}</option>)}
        </Select>
        <label className="flex items-center gap-2 text-sm text-ink-secondary">{t('mfg.order.dueFrom')}<Input type="date" className="w-40" value={dueFrom} onChange={(e) => reset(setDueFrom)(e.target.value)} /></label>
        <label className="flex items-center gap-2 text-sm text-ink-secondary">{t('mfg.order.dueTo')}<Input type="date" className="w-40" value={dueTo} onChange={(e) => reset(setDueTo)(e.target.value)} /></label>
        <Checkbox label={t('mfg.order.lateOnly')} checked={lateOnly} onChange={(e) => reset(setLateOnly)(e.target.checked)} />
        {canRelease && (
          <Button variant="secondary" disabled={selected.size === 0} loading={releasing} onClick={releaseSelected}>{t('mfg.order.bulk.release', { count: selected.size })}</Button>
        )}
      </div>
      {isError && <Alert variant="danger" className="mb-3">{t('mfg.order.loadFailed')}</Alert>}
      <DataTable
        data={rows}
        isLoading={isLoading}
        emptyMessage={t('mfg.order.empty')}
        columns={[
          ...(canRelease ? [{
            key: 'select', label: t('mfg.order.bulk.select'),
            render: (r: OrderListItem) => canBulkRelease(r.status)
              ? <input type="checkbox" className="h-4 w-4" aria-label={t('mfg.order.bulk.selectOne', { no: r.orderNo })} checked={selected.has(r.id)} onChange={() => toggle(r.id)} />
              : null,
          }] : []),
          { key: 'orderNo', label: t('mfg.order.orderNo'), render: (r: OrderListItem) => <Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/orders/${r.id}`}>{r.orderNo}</Link> },
          { key: 'itemCode', label: t('mfg.order.item'), render: (r: OrderListItem) => <span><span className="font-mono text-xs">{r.itemCode}</span> <span className="text-ink-secondary">{r.itemName}</span></span> },
          {
            key: 'quantity', label: t('mfg.order.progress'),
            render: (r: OrderListItem) => (
              <div className="w-40">
                <div className="mb-1 text-xs tabular-nums text-ink-secondary">{fmtQty(r.completedQty)} / {fmtQty(r.quantity)}</div>
                <Progress label={t('mfg.order.progressOf', { no: r.orderNo })} value={r.quantity > 0 ? (r.completedQty / r.quantity) * 100 : 0} />
              </div>
            ),
          },
          { key: 'status', label: t('common.status'), render: (r: OrderListItem) => <OrderStatusBadge status={r.status} /> },
          { key: 'priority', label: t('mfg.order.priority'), align: 'right' as const },
          { key: 'dueDate', label: t('mfg.order.dueDate'), render: (r: OrderListItem) => <span className="flex items-center gap-2">{fmtDate(r.dueDate)}<LateFlag late={r.isLate} /></span> },
          { key: 'plannedStart', label: t('mfg.order.planned'), render: (r: OrderListItem) => `${fmtDate(r.plannedStart)} → ${fmtDate(r.plannedEnd)}` },
        ]}
      />
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.total ?? 0} onPageChange={setPage} />
      {selected.size >= MAX_BULK && <p className="mt-2 text-xs text-ink-secondary">{t('mfg.order.bulk.max', { max: MAX_BULK })}</p>}

      {creating && plant && <OrderFormModal plant={plant} order={null} onClose={() => setCreating(false)} onSaved={(o) => { setCreating(false); queryClient.invalidateQueries({ queryKey: ['mfg', 'orders'] }); navigate(`/manufacturing/orders/${o.id}`) }} />}

      <Modal open={!!results} size="lg" title={t('mfg.order.bulk.resultTitle')} onClose={() => setResults(null)} footer={<Button onClick={() => setResults(null)}>{t('common.close')}</Button>}>
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{t('mfg.order.bulk.resultTitle')}</caption>
          <thead><tr className="border-b border-line text-ink-secondary"><th scope="col" className="py-1 pr-3">{t('mfg.order.orderNo')}</th><th scope="col" className="py-1 pr-3">{t('common.status')}</th><th scope="col" className="py-1">{t('mfg.order.bulk.detail')}</th></tr></thead>
          <tbody>
            {(results ?? []).map((r) => (
              <tr key={r.orderNo} className="border-b border-line align-top">
                <td className="py-1 pr-3 font-mono">{r.orderNo}</td>
                <td className="py-1 pr-3">{r.ok ? t('mfg.order.bulk.ok') : t('mfg.order.bulk.refused')}</td>
                <td className="py-1">
                  {r.ok ? (r.shortages.length > 0 ? t('mfg.order.bulk.shortages', { list: r.shortages.map((s) => `${s.componentCode} (${fmtQty(s.shortage)})`).join(', ') }) : t('mfg.order.bulk.noShortage')) : `${r.code ? `${r.code}: ` : ''}${r.message ?? ''}`}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Modal>
    </div>
  )
}
