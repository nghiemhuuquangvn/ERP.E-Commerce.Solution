import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { taxRatesApi } from '@/api/accounting/tax-rates.api'
import type { TaxRate } from '@/api/accounting/tax-rates.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface TaxRateFormValues {
  name: string
  code: string
  rate: number
  description: string
}

function TaxRateModal({
  taxRate,
  onClose,
}: {
  taxRate: TaxRate | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!taxRate

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<TaxRateFormValues>({
    defaultValues: {
      name: taxRate?.name ?? '',
      code: taxRate?.code ?? '',
      rate: taxRate?.rate ?? 0,
      description: taxRate?.description ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: TaxRateFormValues) => {
      const body = {
        name: values.name,
        code: values.code,
        rate: Number(values.rate),
        description: values.description.trim() || null,
      }
      return isEdit ? taxRatesApi.update(taxRate!.id, body) : taxRatesApi.create(body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'taxRates'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('accounting.taxRates.editTitle') : t('accounting.taxRates.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
            {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.taxRates.rate')}
            </label>
            <input
              type="number"
              step="0.01"
              className="input"
              {...register('rate', { required: true, valueAsNumber: true })}
            />
            {errors.rate && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.taxRates.rate')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.taxRates.description')}
            </label>
            <textarea className="input" rows={3} {...register('description')} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('accounting.taxRates.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function TaxRatesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:tax:manage')
  const queryClient = useQueryClient()
  const [activeOnly, setActiveOnly] = useState(false)
  const [modalState, setModalState] = useState<{ open: boolean; taxRate: TaxRate | null }>({
    open: false,
    taxRate: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<TaxRate | null>(null)

  // No pagination — TaxRatesController.List (ListTaxRatesQuery) returns the full List<TaxRateDto>,
  // filtered server-side by the optional activeOnly query param.
  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'taxRates', { activeOnly }],
    queryFn: () => taxRatesApi.list({ activeOnly: activeOnly || undefined }),
    staleTime: 30_000,
  })

  const deactivateMutation = useMutation({
    mutationFn: (r: TaxRate) => taxRatesApi.deactivate(r.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'taxRates'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'rate',
        label: t('accounting.taxRates.rate'),
        className: 'text-right',
        render: (r: TaxRate) => `${r.rate}%`,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (r: TaxRate) => (
          <StatusBadge
            status={r.isActive ? 'Active' : 'Inactive'}
            label={r.isActive ? t('accounting.taxRates.active') : t('accounting.taxRates.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (r: TaxRate) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, taxRate: r })}
              >
                {t('common.edit')}
              </button>
              {r.isActive && (
                <button
                  className="text-xs text-red-600 hover:underline"
                  onClick={() => setConfirmTarget(r)}
                >
                  {t('common.deactivate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.taxRates.title')}</h1>
        {canManage && (
          <button
            className="btn-primary"
            onClick={() => setModalState({ open: true, taxRate: null })}
          >
            {t('accounting.taxRates.add')}
          </button>
        )}
      </div>

      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input
          type="checkbox"
          checked={activeOnly}
          onChange={(e) => setActiveOnly(e.target.checked)}
        />
        {t('accounting.taxRates.activeOnly')}
      </label>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalState.open && (
        <TaxRateModal
          taxRate={modalState.taxRate}
          onClose={() => setModalState({ open: false, taxRate: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('accounting.taxRates.deactivateTitle')}
        message={t('accounting.taxRates.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && deactivateMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
