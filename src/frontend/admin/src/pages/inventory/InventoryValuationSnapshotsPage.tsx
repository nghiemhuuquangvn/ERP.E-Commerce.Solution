import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { valuationSnapshotsApi } from '@/api/valuationSnapshots.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'
import { Alert, Button, FormField, Input, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface CloseFormValues {
  periodCode: string
  warehouseId: string
}

export function InventoryValuationSnapshotsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [lookupPeriod, setLookupPeriod] = useState('')
  const [searchedPeriod, setSearchedPeriod] = useState<string | null>(null)
  const [pending, setPending] = useState<CloseFormValues | null>(null)

  const { register, handleSubmit, reset, formState: { errors } } = useForm<CloseFormValues>({
    defaultValues: { periodCode: '', warehouseId: '' },
  })

  const closeMutation = useMutation({
    mutationFn: (v: CloseFormValues) =>
      valuationSnapshotsApi.close({ periodCode: v.periodCode, warehouseId: v.warehouseId || undefined }),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['valuationSnapshot'] })
      toast.success(t('common.saved'))
      reset()
      setPending(null)
      setSearchedPeriod(result.periodCode)
    },
    onError: () => setPending(null),
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
      <p className="max-w-2xl text-sm text-ink-secondary">{t('inventory.valuationSnapshot.description')}</p>

      {hasPermission('inventory:valuation:close') && (
        <div className="card max-w-xl p-5">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">
            {t('inventory.valuationSnapshot.close')}
          </h2>
          <form onSubmit={handleSubmit((v) => setPending(v))} className="flex flex-wrap items-start gap-3">
            <FormField label={t('inventory.valuationSnapshot.periodCode')} required error={errors.periodCode ? t('common.required') : undefined}>
              <Input placeholder="2026-07" {...register('periodCode', { required: true })} />
            </FormField>
            <FormField label={t('inventory.valuationSnapshot.warehouseIdOptional')}>
              <Input {...register('warehouseId')} />
            </FormField>
            <Button type="submit" className="mt-6" loading={closeMutation.isPending}>
              {t('inventory.valuationSnapshot.close')}
            </Button>
          </form>
          {closeMutation.isError && (
            <Alert variant="danger" className="mt-3">{serverMessage(closeMutation.error, t('inventory.valuationSnapshot.closeFailed'))}</Alert>
          )}
        </div>
      )}

      <div className="card max-w-3xl p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">
          {t('inventory.valuationSnapshot.viewByPeriod')}
        </h2>
        <form
          className="flex items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            setSearchedPeriod(lookupPeriod.trim())
          }}
        >
          <FormField className="flex-1" label={t('inventory.valuationSnapshot.periodCode')}>
            <Input placeholder="2026-07" value={lookupPeriod} onChange={(e) => setLookupPeriod(e.target.value)} />
          </FormField>
          <Button type="submit" disabled={!lookupPeriod.trim()}>
            {t('common.search')}
          </Button>
        </form>

        {searchedPeriod && (
          <div className="mt-4 border-t border-line pt-4" aria-live="polite">
            {isLoading ? (
              <p className="text-xs text-ink-secondary">{t('common.loading')}</p>
            ) : snapshot ? (
              <div className="space-y-3">
                <div className="flex justify-between text-sm">
                  <span className="text-ink-secondary">{t('inventory.valuationSnapshot.snapshotDate')}</span>
                  <span>{format(new Date(snapshot.snapshotDate), 'dd MMM yyyy HH:mm')}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-ink-secondary">{t('inventory.valuationSnapshot.totalValue')}</span>
                  <span className="font-semibold tabular-nums">{snapshot.totalValue.toFixed(2)}</span>
                </div>
                <DataTable
                  columns={[
                    { key: 'productId', label: t('orders.product'), className: 'font-mono text-xs' },
                    { key: 'quantity', label: t('inventory.costHistory.quantity'), align: 'right' as const },
                    { key: 'unitCost', label: t('inventory.unitCost'), align: 'right' as const, render: (l) => l.unitCost.toFixed(2) },
                    { key: 'totalValue', label: t('inventory.valuationSnapshot.lineValue'), align: 'right' as const, render: (l) => <span className="font-medium">{l.totalValue.toFixed(2)}</span> },
                  ]}
                  data={snapshot.lines.map((l) => ({ ...l, id: l.productId }))}
                  isLoading={false}
                />
              </div>
            ) : (
              isFetched && <Alert variant="warning">{t('inventory.valuationSnapshot.notFound')}</Alert>
            )}
          </div>
        )}
      </div>

      <ConfirmDialog
        open={!!pending}
        title={t('inventory.valuationSnapshot.closeTitle')}
        message={t('inventory.valuationSnapshot.closeConfirm', { period: pending?.periodCode })}
        confirmLabel={t('inventory.valuationSnapshot.close')}
        danger
        onConfirm={() => pending && closeMutation.mutate(pending)}
        onCancel={() => setPending(null)}
      />
    </div>
  )
}
