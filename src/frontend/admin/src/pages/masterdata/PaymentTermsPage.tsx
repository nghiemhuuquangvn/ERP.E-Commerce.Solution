import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { paymentTermsApi } from '@/api/masterdata/paymentTerms.api'
import type { PaymentTerm } from '@/api/masterdata/paymentTerms.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface PaymentTermFormValues {
  code: string
  name: string
  netDays: number
  depositPercent: number | ''
}

function PaymentTermModal({
  paymentTerm,
  onClose,
}: {
  paymentTerm: PaymentTerm | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!paymentTerm

  const { register, handleSubmit, formState: { errors } } = useForm<PaymentTermFormValues>({
    defaultValues: {
      code: paymentTerm?.code ?? '',
      name: paymentTerm?.name ?? '',
      netDays: paymentTerm?.netDays ?? 0,
      depositPercent: paymentTerm?.depositPercent ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: PaymentTermFormValues) => {
      const depositPercent = values.depositPercent === '' ? null : Number(values.depositPercent)
      if (isEdit) {
        return paymentTermsApi.update(paymentTerm!.id, {
          name: values.name,
          netDays: Number(values.netDays),
          depositPercent,
        })
      }
      return paymentTermsApi.create({
        code: values.code,
        name: values.name,
        netDays: Number(values.netDays),
        depositPercent,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'paymentTerms'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.paymentTerms.editTitle') : t('masterdata.paymentTerms.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
              <input className="input" {...register('code', { required: true })} />
              {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.paymentTerms.netDays')}
            </label>
            <input
              type="number"
              className="input"
              {...register('netDays', { required: true, valueAsNumber: true })}
            />
            {errors.netDays && (
              <p className="mt-1 text-xs text-red-600">{t('masterdata.paymentTerms.netDays')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.paymentTerms.depositPercent')}
            </label>
            <input
              type="number"
              step="0.01"
              className="input"
              {...register('depositPercent', { valueAsNumber: true })}
            />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.paymentTerms.failedToSave')}</p>
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

export function PaymentTermsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [modalState, setModalState] = useState<{ open: boolean; paymentTerm: PaymentTerm | null }>({
    open: false,
    paymentTerm: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<PaymentTerm | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'paymentTerms', { page, pageSize }],
    queryFn: () => paymentTermsApi.list({ page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:payment-terms:manage')

  const toggleMutation = useMutation({
    mutationFn: (pt: PaymentTerm) =>
      pt.isActive ? paymentTermsApi.deactivate(pt.id) : paymentTermsApi.activate(pt.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'paymentTerms'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'netDays', label: t('masterdata.paymentTerms.netDays') },
      {
        key: 'depositPercent',
        label: t('masterdata.paymentTerms.depositPercent'),
        render: (pt: PaymentTerm) => (pt.depositPercent != null ? `${pt.depositPercent}%` : '—'),
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (pt: PaymentTerm) => (
          <StatusBadge
            status={pt.isActive ? 'Active' : 'Inactive'}
            label={pt.isActive ? t('masterdata.paymentTerms.active') : t('masterdata.paymentTerms.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (pt: PaymentTerm) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, paymentTerm: pt })}
              >
                {t('common.edit')}
              </button>
              {pt.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(pt)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(pt)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.paymentTerms.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, paymentTerm: null })}>
            {t('masterdata.paymentTerms.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <PaymentTermModal
          paymentTerm={modalState.paymentTerm}
          onClose={() => setModalState({ open: false, paymentTerm: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.paymentTerms.deactivateTitle')}
        message={t('masterdata.paymentTerms.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
