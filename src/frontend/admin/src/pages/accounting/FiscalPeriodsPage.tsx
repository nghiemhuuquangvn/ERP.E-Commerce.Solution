import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fiscalPeriodsApi } from '@/api/accounting/fiscal-periods.api'
import type { ClosingMode, FiscalPeriod } from '@/api/accounting/fiscal-periods.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

/** How the tenant closes its books: a closing entry every period, or one at the end of the fiscal year. */
function ClosingPolicyPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['accounting', 'closingPolicy'], queryFn: () => fiscalPeriodsApi.closingPolicy(), staleTime: 30_000 })
  const [mode, setMode] = useState<ClosingMode | null>(null)
  const [month, setMonth] = useState<number | null>(null)
  const [autoAccrue, setAutoAccrue] = useState<boolean | null>(null)
  const effectiveAutoAccrue = autoAccrue ?? data?.autoAccrueLoanInterest ?? false
  const effectiveMode = mode ?? data?.closingMode ?? 'Monthly'
  const effectiveMonth = month ?? data?.fiscalYearEndMonth ?? 12
  const dirty = !!data && (effectiveMode !== data.closingMode || effectiveMonth !== data.fiscalYearEndMonth || effectiveAutoAccrue !== !!data.autoAccrueLoanInterest)

  const save = useMutation({
    mutationFn: () => fiscalPeriodsApi.updateClosingPolicy({ closingMode: effectiveMode, fiscalYearEndMonth: effectiveMonth, autoAccrueLoanInterest: effectiveAutoAccrue }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'closingPolicy'] })
      setMode(null)
      setMonth(null)
      setAutoAccrue(null)
      queryClient.invalidateQueries({ queryKey: ['accounting', 'loans'] })
    },
  })
  const error = (save.error as { response?: { data?: { detail?: string } } } | null)?.response?.data?.detail

  return (
    <div className="card space-y-3 p-4">
      <h2 className="text-sm font-semibold text-gray-900">{t('accounting.fiscalPeriods.policyTitle')}</h2>
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.fiscalPeriods.policyMode')}</label>
          <select className="input" disabled={!canManage} value={effectiveMode} onChange={(e) => setMode(e.target.value as ClosingMode)}>
            <option value="Monthly">{t('accounting.fiscalPeriods.policyMonthly')}</option>
            <option value="YearEnd">{t('accounting.fiscalPeriods.policyYearEnd')}</option>
          </select>
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.fiscalPeriods.policyMonth')}</label>
          <select className="input" disabled={!canManage} value={effectiveMonth} onChange={(e) => setMonth(Number(e.target.value))}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </div>
        {canManage && (
          <button className="btn-primary" disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
            {t('common.save')}
          </button>
        )}
      </div>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} checked={effectiveAutoAccrue} onChange={(e) => setAutoAccrue(e.target.checked)} />
        {t('accounting.fiscalPeriods.autoAccrue')}
      </label>
      <p className="text-xs text-gray-500">{t('accounting.fiscalPeriods.autoAccrueHint')}</p>
      <p className="text-xs text-gray-500">
        {effectiveMode === 'YearEnd' ? t('accounting.fiscalPeriods.policyYearEndHint') : t('accounting.fiscalPeriods.policyMonthlyHint')}
      </p>
      {save.isError && <p className="text-sm text-red-600">{error ?? t('accounting.fiscalPeriods.failedToSave')}</p>}
    </div>
  )
}

interface FiscalPeriodFormValues {
  periodName: string
  startDate: string
  endDate: string
}

function OpenFiscalPeriodModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FiscalPeriodFormValues>({
    defaultValues: { periodName: '', startDate: '', endDate: '' },
  })

  const mutation = useMutation({
    mutationFn: (v: FiscalPeriodFormValues) =>
      fiscalPeriodsApi.open({
        periodName: v.periodName,
        startDate: v.startDate,
        endDate: v.endDate,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'fiscalPeriods'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('accounting.fiscalPeriods.openTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.fiscalPeriods.periodName')}
            </label>
            <input className="input" {...register('periodName', { required: true })} />
            {errors.periodName && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.fiscalPeriods.periodName')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.fiscalPeriods.startDate')}
            </label>
            <input type="date" className="input" {...register('startDate', { required: true })} />
            {errors.startDate && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.fiscalPeriods.startDate')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.fiscalPeriods.endDate')}
            </label>
            <input type="date" className="input" {...register('endDate', { required: true })} />
            {errors.endDate && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.fiscalPeriods.endDate')} is required.
              </p>
            )}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('accounting.fiscalPeriods.failedToSave')}</p>
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

export function FiscalPeriodsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:periods:manage')
  const queryClient = useQueryClient()
  const [modalOpen, setModalOpen] = useState(false)
  const [confirmTarget, setConfirmTarget] = useState<FiscalPeriod | null>(null)
  const [reopenTarget, setReopenTarget] = useState<FiscalPeriod | null>(null)

  // No pagination — FiscalPeriodsController.List (ListFiscalPeriodsQuery) returns the full List<FiscalPeriodDto>.
  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'fiscalPeriods'],
    queryFn: () => fiscalPeriodsApi.list(),
    staleTime: 30_000,
  })

  const closeMutation = useMutation({
    mutationFn: (p: FiscalPeriod) => fiscalPeriodsApi.close(p.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setConfirmTarget(null)
    },
  })

  const reopenMutation = useMutation({
    mutationFn: ({ period, reason }: { period: FiscalPeriod; reason: string }) => fiscalPeriodsApi.reopen(period.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setReopenTarget(null)
    },
  })

  // The API explains refusals (an earlier open period, draft entries, an unbalanced trial balance ...).
  const detail = (e: unknown) =>
    (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? t('accounting.fiscalPeriods.failedToSave')

  const columns = useMemo(
    () => [
      { key: 'periodName', label: t('accounting.fiscalPeriods.periodName') },
      { key: 'startDate', label: t('accounting.fiscalPeriods.startDate') },
      { key: 'endDate', label: t('accounting.fiscalPeriods.endDate') },
      {
        key: 'status',
        label: t('common.status'),
        render: (p: FiscalPeriod) => <StatusBadge status={p.status} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: FiscalPeriod) =>
          !canManage ? null : p.status === 'Open' ? (
            <button
              className="text-xs text-red-600 hover:underline"
              onClick={() => setConfirmTarget(p)}
            >
              {t('accounting.fiscalPeriods.close')}
            </button>
          ) : (
            <button className="text-xs text-primary-600 hover:underline" onClick={() => setReopenTarget(p)}>
              {t('accounting.fiscalPeriods.reopen')}
            </button>
          ),
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.fiscalPeriods.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {t('accounting.fiscalPeriods.add')}
          </button>
        )}
      </div>

      <ClosingPolicyPanel canManage={canManage} />

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalOpen && <OpenFiscalPeriodModal onClose={() => setModalOpen(false)} />}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('accounting.fiscalPeriods.closeTitle')}
        message={t('accounting.fiscalPeriods.closeConfirm', {
          periodName: confirmTarget?.periodName,
        })}
        confirmLabel={t('accounting.fiscalPeriods.close')}
        danger
        onConfirm={() => confirmTarget && closeMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
      {closeMutation.isError && <p className="text-sm text-red-600">{detail(closeMutation.error)}</p>}

      <ReasonDialog
        open={!!reopenTarget}
        title={t('accounting.fiscalPeriods.reopenTitle')}
        message={t('accounting.fiscalPeriods.reopenHint', { periodName: reopenTarget?.periodName })}
        confirmLabel={t('accounting.fiscalPeriods.reopen')}
        busy={reopenMutation.isPending}
        error={reopenMutation.isError ? detail(reopenMutation.error) : null}
        onConfirm={(reason) => reopenTarget && reopenMutation.mutate({ period: reopenTarget, reason })}
        onCancel={() => {
          setReopenTarget(null)
          reopenMutation.reset()
        }}
      />
    </div>
  )
}
