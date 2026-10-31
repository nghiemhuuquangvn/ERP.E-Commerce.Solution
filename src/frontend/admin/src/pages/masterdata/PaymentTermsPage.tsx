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
import { Alert, Button, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.paymentTerms.editTitle') : t('masterdata.paymentTerms.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="payment-term-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="payment-term-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
            <Input {...register('code', { required: true })} />
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.paymentTerms.netDays')} required error={errors.netDays ? t('common.required') : undefined}>
          <Input
            type="number"
            {...register('netDays', { required: true, valueAsNumber: true })}
          />
        </FormField>
        <FormField label={t('masterdata.paymentTerms.depositPercent')}>
          <Input
            type="number"
            step="0.01"
            {...register('depositPercent', { valueAsNumber: true })}
          />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.paymentTerms.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, paymentTerm: pt })}>
                {t('common.edit')}
              </Button>
              {pt.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(pt)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(pt)}>
                  {t('common.activate')}
                </Button>
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
          <Button onClick={() => setModalState({ open: true, paymentTerm: null })}>
            {t('masterdata.paymentTerms.add')}
          </Button>
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
