import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { purchaseRequestsApi } from '@/api/purchaseRequests.api'
import type { CreateOrdersResult, PurchaseRequest, PurchaseRequestStatus } from '@/api/purchaseRequests.api'
import { inventoryApi } from '@/api/inventory.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

const STATUS_TABS: (PurchaseRequestStatus | 'All')[] = ['Suggested', 'Ordered', 'Quoting', 'Dismissed', 'Converted', 'All']

export function PurchaseRequestsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [statusFilter, setStatusFilter] = useState<PurchaseRequestStatus | 'All'>('Suggested')
  const [selected, setSelected] = useState<string[]>([])
  const [result, setResult] = useState<CreateOrdersResult | null>(null)
  const [scanMessage, setScanMessage] = useState<string | null>(null)

  const { data: requests, isLoading } = useQuery({
    queryKey: ['purchaseRequests', statusFilter],
    queryFn: () => purchaseRequestsApi.list(statusFilter === 'All' ? undefined : statusFilter),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['purchaseRequests'] })

  const dismissMutation = useMutation({ mutationFn: (id: string) => purchaseRequestsApi.dismiss(id), onSuccess: refresh })
  const convertMutation = useMutation({ mutationFn: (id: string) => purchaseRequestsApi.convert(id), onSuccess: refresh })

  const createOrdersMutation = useMutation({
    mutationFn: () => purchaseRequestsApi.createOrders(selected.length > 0 ? selected : undefined),
    onSuccess: (r) => {
      setResult(r)
      setSelected([])
      refresh()
    },
  })

  const scanMutation = useMutation({
    mutationFn: () => inventoryApi.scanReplenishment(),
    onSuccess: (r) => {
      setScanMessage(t('purchasing.requests.scanDone', { checked: r.checked, below: r.belowMinimum, alerts: r.alertsSent }))
      // The alert reaches Purchasing through the message queue, a moment later.
      setTimeout(refresh, 2500)
    },
  })

  const canManage = hasPermission('purchase_request:manage')
  const canScan = hasPermission('inventory:manage')
  const rows = requests ?? []
  const openRows = rows.filter((r) => r.status === 'Suggested')
  const toggle = (id: string) => setSelected((s) => (s.includes(id) ? s.filter((x) => x !== id) : [...s, id]))

  const link = (r: PurchaseRequest) =>
    r.purchaseOrderId ? (
      <Link className="text-primary-600 hover:underline" to={`/orders/purchase/${r.purchaseOrderId}`}>{t('purchasing.requests.openOrder')}</Link>
    ) : r.rfqId ? (
      <Link className="text-primary-600 hover:underline" to={`/purchasing/rfqs/${r.rfqId}`}>{t('purchasing.requests.openRfq')}</Link>
    ) : null

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">{t('purchasing.requests.title')}</h1>
          <p className="mt-1 max-w-2xl text-sm text-gray-500">{t('purchasing.requests.description')}</p>
        </div>
        <div className="flex gap-2">
          {canScan && (
            <button className="btn-outline" disabled={scanMutation.isPending} onClick={() => scanMutation.mutate()}>
              {scanMutation.isPending ? t('common.loading') : t('purchasing.requests.scanNow')}
            </button>
          )}
          {canManage && (
            <button
              className="btn-primary"
              disabled={createOrdersMutation.isPending || openRows.length === 0}
              onClick={() => createOrdersMutation.mutate()}
            >
              {selected.length > 0
                ? t('purchasing.requests.createOrdersSelected', { count: selected.length })
                : t('purchasing.requests.createOrdersAll', { count: openRows.length })}
            </button>
          )}
        </div>
      </div>

      {scanMessage && <div className="rounded-md bg-blue-50 p-3 text-sm text-blue-800">{scanMessage}</div>}
      {createOrdersMutation.isError && <div className="rounded-md bg-red-50 p-3 text-sm text-red-700">{t('common.failedToSave')}</div>}

      {result && (
        <div className="card space-y-2 p-4 text-sm">
          <p className="font-medium text-gray-900">
            {t('purchasing.requests.created', { orders: result.purchaseOrders.length, rfq: result.rfq ? 1 : 0 })}
          </p>
          <ul className="list-disc space-y-1 pl-5">
            {result.purchaseOrders.map((o) => (
              <li key={o.id}>
                <Link className="text-primary-600 hover:underline" to={`/orders/purchase/${o.id}`}>{o.poNumber}</Link>
                {' — '}{o.supplierName}, {t('purchasing.requests.lines', { count: o.lines })}, {o.total.toLocaleString()} {o.currency}
              </li>
            ))}
            {result.rfq && (
              <li>
                <Link className="text-primary-600 hover:underline" to={`/purchasing/rfqs/${result.rfq.id}`}>{result.rfq.rfqNumber}</Link>
                {' — '}{t('purchasing.requests.rfqSummary', { lines: result.rfq.lines, suppliers: result.rfq.suppliers })}
              </li>
            )}
          </ul>
          {result.skipped.length > 0 && (
            <p className="text-amber-700">{t('purchasing.requests.skipped', { count: result.skipped.length })}: {result.skipped.map((s) => s.reason).join('; ')}</p>
          )}
          <button className="text-xs text-gray-500 hover:underline" onClick={() => setResult(null)}>{t('common.close')}</button>
        </div>
      )}

      <div className="flex gap-1 border-b border-gray-200">
        {STATUS_TABS.map((s) => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setSelected([]) }}
            className={`px-4 py-2 text-sm font-medium ${
              statusFilter === s ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t(`purchasing.requests.status.${s}`)}
          </button>
        ))}
      </div>

      <DataTable
        data={rows.map((r) => ({ ...r, id: r.id }))}
        isLoading={isLoading}
        columns={[
          {
            key: 'select',
            label: '',
            render: (r) =>
              canManage && r.status === 'Suggested' ? (
                <input type="checkbox" className="rounded border-gray-300" checked={selected.includes(r.id)} onChange={() => toggle(r.id)} />
              ) : null,
          },
          {
            key: 'productName',
            label: t('orders.product'),
            render: (r) => (
              <div>
                <div className="font-medium">{r.productName}</div>
                <div className="font-mono text-xs text-gray-400">{r.productCode}</div>
              </div>
            ),
          },
          { key: 'availableQuantity', label: t('inventory.available'), className: 'text-right' },
          { key: 'onOrderQuantity', label: t('purchasing.requests.onOrder'), className: 'text-right' },
          {
            key: 'minStock',
            label: t('purchasing.requests.policy'),
            className: 'text-right',
            render: (r) => `${r.minStock} – ${r.maxStock}${r.safetyStock > 0 ? ` (${t('purchasing.requests.safety')} ${r.safetyStock})` : ''}`,
          },
          { key: 'suggestedQuantity', label: t('purchasing.requests.suggestedQuantity'), className: 'text-right font-semibold' },
          {
            key: 'preferredSupplierName',
            label: t('purchasing.requests.supplier'),
            render: (r) =>
              r.preferredSupplierId ? (
                <div>
                  <div>{r.preferredSupplierName ?? r.preferredSupplierId}</div>
                  <div className="text-xs text-gray-400">
                    {r.unitPrice != null ? r.unitPrice.toLocaleString() : '—'}{r.leadTimeDays != null ? ` · ${r.leadTimeDays} ${t('purchasing.requests.days')}` : ''}
                  </div>
                </div>
              ) : (
                <span className="text-xs text-amber-700">{t('purchasing.requests.noPreferred')}</span>
              ),
          },
          {
            key: 'status',
            label: t('common.status'),
            render: (r) => (
              <div className="space-y-1">
                <StatusBadge status={r.status} />
                {r.note && <div className="text-xs text-gray-400">{r.note}</div>}
                <div className="text-xs">{link(r)}</div>
              </div>
            ),
          },
          { key: 'createdAt', label: t('common.date'), render: (r) => format(new Date(r.createdAt), 'dd MMM yyyy') },
          {
            key: 'actions',
            label: '',
            className: 'text-right',
            render: (r) =>
              canManage && r.status === 'Suggested' ? (
                <div className="flex justify-end gap-2">
                  <button className="btn-outline text-xs" disabled={dismissMutation.isPending} onClick={() => dismissMutation.mutate(r.id)}>
                    {t('purchasing.requests.dismiss')}
                  </button>
                  <button className="btn-outline text-xs" disabled={convertMutation.isPending} onClick={() => convertMutation.mutate(r.id)}>
                    {t('purchasing.requests.convert')}
                  </button>
                </div>
              ) : null,
          },
        ]}
      />
    </div>
  )
}
