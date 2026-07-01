import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { paymentsApi, PaymentStatus } from '@/api/payments.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { format } from 'date-fns'
import type { Payment } from '@/api/payments.api'

const STATUS_FILTERS: { labelKey: string; value: PaymentStatus | '' }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'payments.statusFilters.pending', value: 'Pending' },
  { labelKey: 'payments.statusFilters.captured', value: 'Captured' },
  { labelKey: 'payments.statusFilters.failed', value: 'Failed' },
  { labelKey: 'payments.statusFilters.refunded', value: 'Refunded' },
]

export function PaymentsPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState<PaymentStatus | ''>('')

  const { data, isLoading } = useQuery({
    queryKey: ['payments', { page, pageSize, status }],
    queryFn: () => paymentsApi.list({ page, pageSize, status: status || undefined }),
  })

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  const columns = [
    { key: 'id', label: 'ID', className: 'font-mono text-xs', render: (p: Payment) => p.id.slice(0, 8) },
    { key: 'orderId', label: t('payments.order'), className: 'font-mono text-xs', render: (p: Payment) => p.orderId.slice(0, 8) },
    { key: 'method', label: t('payments.method') },
    {
      key: 'amount',
      label: t('payments.amount'),
      render: (p: Payment) => fmt(p.amount),
      className: 'text-right',
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (p: Payment) => <StatusBadge status={p.status} />,
    },
    {
      key: 'createdAt',
      label: t('common.date'),
      render: (p: Payment) => format(new Date(p.createdAt), 'dd MMM yyyy'),
    },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('payments.title')}</h1>

      <div className="flex gap-1">
        {STATUS_FILTERS.map((f) => (
          <button
            key={f.value}
            onClick={() => { setStatus(f.value as PaymentStatus | ''); setPage(1) }}
            className={`rounded px-3 py-1.5 text-sm ${
              status === f.value ? 'bg-primary-600 text-white' : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'
            }`}
          >
            {t(f.labelKey)}
          </button>
        ))}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />
    </div>
  )
}
