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
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="md"
      title={isEdit ? t('accounting.taxRates.editTitle') : t('accounting.taxRates.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="tax-rate-form" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form id="tax-rate-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField
          label={t('accounting.taxRates.rate')}
          required
          hint={t('accounting.taxRates.rateHint')}
          error={errors.rate ? t('accounting.taxRates.rateRange') : undefined}
        >
          <Input
            type="number"
            step="0.01"
            min={0}
            max={100}
            {...register('rate', { required: true, valueAsNumber: true, min: 0, max: 100 })}
          />
        </FormField>
        <FormField label={t('accounting.taxRates.description')}>
          <Textarea {...register('description')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.taxRates.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function TaxRatesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:tax:manage')
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      setConfirmTarget(null)
    },
    onError: (e) => {
      setConfirmTarget(null)
      toast.error(serverMessage(e, t('common.error')))
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
              <Button variant="link" className="text-xs" aria-label={`${t('common.edit')} ${r.name}`} onClick={() => setModalState({ open: true, taxRate: r })}>
                {t('common.edit')}
              </Button>
              {r.isActive && (
                <Button variant="link-danger" className="text-xs" aria-label={`${t('common.deactivate')} ${r.name}`} onClick={() => setConfirmTarget(r)}>
                  {t('common.deactivate')}
                </Button>
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
          <Button onClick={() => setModalState({ open: true, taxRate: null })}>{t('accounting.taxRates.add')}</Button>
        )}
      </div>

      <Checkbox checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} label={t('accounting.taxRates.activeOnly')} />

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('accounting.taxRates.empty')}
            action={canManage ? <Button onClick={() => setModalState({ open: true, taxRate: null })}>{t('accounting.taxRates.add')}</Button> : undefined}
          />
        }
      />

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
