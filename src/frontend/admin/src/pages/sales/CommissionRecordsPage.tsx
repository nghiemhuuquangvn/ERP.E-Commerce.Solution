import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { commissionsApi } from '@/api/commissions.api'
import type { CommissionRecord } from '@/api/commissions.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { format } from 'date-fns'

export function CommissionRecordsPage() {
  const { t } = useTranslation()
  const [salesRepId, setSalesRepId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['commissionRecords', { salesRepId, from, to }],
    queryFn: () =>
      commissionsApi.list({
        salesRepId: salesRepId || undefined,
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to).toISOString() : undefined,
      }),
    staleTime: 30_000,
  })

  const fmt = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  const columns = useMemo(
    () => [
      { key: 'salesRepId', label: t('commission.records.salesRep'), className: 'font-mono text-xs' },
      { key: 'orderId', label: t('commission.records.order'), className: 'font-mono text-xs' },
      { key: 'amount', label: t('commission.records.amount'), render: (r: CommissionRecord) => fmt(r.amount) },
      {
        key: 'status',
        label: t('common.status'),
        render: (r: CommissionRecord) => <StatusBadge status={r.status} />,
      },
      {
        key: 'calculatedAt',
        label: t('commission.records.calculatedAt'),
        render: (r: CommissionRecord) => format(new Date(r.calculatedAt), 'dd MMM yyyy HH:mm'),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('commission.records.title')}</h1>

      <div className="flex flex-wrap gap-3">
        <input
          className="input max-w-xs"
          placeholder={t('commission.records.filterBySalesRep')}
          value={salesRepId}
          onChange={(e) => setSalesRepId(e.target.value)}
        />
        <input type="date" className="input" value={from} onChange={(e) => setFrom(e.target.value)} />
        <input type="date" className="input" value={to} onChange={(e) => setTo(e.target.value)} />
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />
    </div>
  )
}
