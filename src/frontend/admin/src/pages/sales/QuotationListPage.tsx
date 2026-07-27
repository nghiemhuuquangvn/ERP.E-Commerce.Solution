import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { quotationsApi } from '@/api/quotations.api'
import type { QuotationStatus, SalesQuotationSummary } from '@/api/quotations.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

export function QuotationListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [customerId, setCustomerId] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['quotations', { page, pageSize, customerId }],
    queryFn: () => quotationsApi.list({ page, pageSize, customerId: customerId || undefined }),
    staleTime: 30_000,
  })

  const columns = [
    {
      key: 'quotationNo',
      label: t('quotations.quotationNo'),
      render: (q: SalesQuotationSummary) => (
        <Link to={`/sales/quotations/${q.quotationId}`} className="text-primary-600 hover:underline">
          {q.quotationNo}
        </Link>
      ),
    },
    { key: 'customerName', label: t('orders.customer') },
    {
      key: 'totalAmount',
      label: t('orders.total'),
      render: (q: SalesQuotationSummary) =>
        new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(q.totalAmount),
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (q: SalesQuotationSummary) => <StatusBadge status={q.status as QuotationStatus} />,
    },
    {
      key: 'validUntil',
      label: t('quotations.validUntil'),
      render: (q: SalesQuotationSummary) => format(new Date(q.validUntil), 'dd MMM yyyy'),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('quotations.title')}</h1>
        {hasPermission('sales_order:create') && (
          <Link to="/sales/quotations/new" className="btn-primary">
            {t('quotations.newQuotation')}
          </Link>
        )}
      </div>

      <div className="flex gap-3">
        <input
          className="input max-w-xs"
          placeholder={t('quotations.filterByCustomer')}
          value={customerId}
          onChange={(e) => {
            setCustomerId(e.target.value)
            setPage(1)
          }}
        />
      </div>

      <DataTable
        columns={columns}
        data={(data?.items ?? []).map((q) => ({ ...q, id: q.quotationId }))}
        isLoading={isLoading}
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
    </div>
  )
}
