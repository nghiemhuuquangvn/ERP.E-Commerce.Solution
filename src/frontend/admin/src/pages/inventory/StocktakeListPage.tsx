import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth.store'
import { stocktakesApi } from '@/api/stocktakes.api'
import type { Stocktake } from '@/api/stocktakes.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

export function StocktakeListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const user = useAuthStore((s) => s.user)
  const [starting, setStarting] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['stocktakes'],
    queryFn: stocktakesApi.list,
    staleTime: 15_000,
  })

  const startMutation = useMutation({
    mutationFn: () => stocktakesApi.start({ startedBy: user?.email ?? user?.id ?? 'admin' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['stocktakes'] })
      setStarting(false)
    },
  })

  const columns = useMemo(
    () => [
      {
        key: 'stocktakeNumber',
        label: t('inventory.stocktakes.number'),
        render: (s: Stocktake) => (
          <Link to={`/inventory/stocktakes/${s.stocktakeId}`} className="text-primary-600 hover:underline">
            {s.stocktakeNumber}
          </Link>
        ),
      },
      { key: 'lines', label: t('inventory.stocktakes.lineCount'), render: (s: Stocktake) => s.lines.length },
      { key: 'status', label: t('common.status'), render: (s: Stocktake) => <StatusBadge status={s.status} /> },
      { key: 'startedBy', label: t('inventory.stocktakes.startedBy') },
      { key: 'startedAt', label: t('common.date'), render: (s: Stocktake) => format(new Date(s.startedAt), 'dd MMM yyyy HH:mm') },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('inventory.stocktakes.title')}</h1>
        {hasPermission('inventory:manage') && (
          <button className="btn-primary" disabled={starting || startMutation.isPending} onClick={() => { setStarting(true); startMutation.mutate() }}>
            {t('inventory.stocktakes.start')}
          </button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={(data ?? []).map((s) => ({ ...s, id: s.stocktakeId }))}
        isLoading={isLoading}
      />
    </div>
  )
}
