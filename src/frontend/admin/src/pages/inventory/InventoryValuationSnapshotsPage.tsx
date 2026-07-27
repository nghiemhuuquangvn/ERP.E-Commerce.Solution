import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { valuationSnapshotsApi } from '@/api/valuationSnapshots.api'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

interface CloseFormValues {
  periodCode: string
  warehouseId: string
}

export function InventoryValuationSnapshotsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [lookupPeriod, setLookupPeriod] = useState('')
  const [searchedPeriod, setSearchedPeriod] = useState<string | null>(null)

  const { register, handleSubmit, reset, formState: { errors } } = useForm<CloseFormValues>({
    defaultValues: { periodCode: '', warehouseId: '' },
  })

  const closeMutation = useMutation({
    mutationFn: (v: CloseFormValues) =>
      valuationSnapshotsApi.close({ periodCode: v.periodCode, warehouseId: v.warehouseId || undefined }),
    onSuccess: (result) => {
      reset()
      setSearchedPeriod(result.periodCode)
    },
  })

  const { data: snapshot, isLoading, isFetched } = useQuery({
    queryKey: ['valuationSnapshot', searchedPeriod],
    queryFn: () => valuationSnapshotsApi.getByPeriod(searchedPeriod!),
    enabled: Boolean(searchedPeriod),
    retry: false,
  })

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">{t('inventory.valuationSnapshot.title')}</h1>
      <p className="max-w-2xl text-sm text-gray-500">{t('inventory.valuationSnapshot.description')}</p>

      {hasPermission('inventory:valuation:close') && (
        <div className="card max-w-xl p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">
            {t('inventory.valuationSnapshot.close')}
          </h2>
          <form
            onSubmit={handleSubmit((v) => closeMutation.mutate(v))}
            className="flex flex-wrap items-end gap-3"
          >
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('inventory.valuationSnapshot.periodCode')}
              </label>
              <input
                className="input"
                placeholder="2026-07"
                {...register('periodCode', { required: true })}
              />
              {errors.periodCode && (
                <p className="mt-1 text-xs text-red-600">{t('inventory.valuationSnapshot.periodCodeRequired')}</p>
              )}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('inventory.valuationSnapshot.warehouseIdOptional')}
              </label>
              <input className="input" {...register('warehouseId')} />
            </div>
            <button type="submit" disabled={closeMutation.isPending} className="btn-primary">
              {closeMutation.isPending ? t('common.saving') : t('inventory.valuationSnapshot.close')}
            </button>
          </form>
          {closeMutation.isError && (
            <p className="mt-2 text-xs text-red-600">{t('inventory.valuationSnapshot.closeFailed')}</p>
          )}
        </div>
      )}

      <div className="card max-w-3xl p-5">
        <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">
          {t('inventory.valuationSnapshot.viewByPeriod')}
        </h2>
        <div className="flex gap-2">
          <input
            className="input flex-1"
            placeholder="2026-07"
            value={lookupPeriod}
            onChange={(e) => setLookupPeriod(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && setSearchedPeriod(lookupPeriod.trim())}
          />
          <button className="btn-primary" onClick={() => setSearchedPeriod(lookupPeriod.trim())}>
            {t('common.search')}
          </button>
        </div>

        {searchedPeriod && (
          <div className="mt-4 border-t border-gray-100 pt-4">
            {isLoading ? (
              <p className="text-xs text-gray-400">{t('common.loading')}</p>
            ) : snapshot ? (
              <div className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500">{t('inventory.valuationSnapshot.snapshotDate')}</span>
                  <span>{format(new Date(snapshot.snapshotDate), 'dd MMM yyyy HH:mm')}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500">{t('inventory.valuationSnapshot.totalValue')}</span>
                  <span className="font-semibold">{snapshot.totalValue.toFixed(2)}</span>
                </div>
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                      <th className="pb-2">{t('orders.product')}</th>
                      <th className="pb-2 text-right">{t('inventory.costHistory.quantity')}</th>
                      <th className="pb-2 text-right">{t('inventory.unitCost')}</th>
                      <th className="pb-2 text-right">{t('inventory.valuationSnapshot.lineValue')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {snapshot.lines.map((line) => (
                      <tr key={line.productId}>
                        <td className="py-2 font-mono text-xs">{line.productId}</td>
                        <td className="py-2 text-right">{line.quantity}</td>
                        <td className="py-2 text-right">{line.unitCost.toFixed(2)}</td>
                        <td className="py-2 text-right font-medium">{line.totalValue.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              isFetched && <p className="text-xs text-gray-400">{t('inventory.valuationSnapshot.notFound')}</p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
