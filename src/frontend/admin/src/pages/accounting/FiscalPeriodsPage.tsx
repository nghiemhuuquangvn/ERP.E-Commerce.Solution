import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fiscalPeriodsApi } from '@/api/accounting/fiscal-periods.api'
import type { ClosingMode, FiscalPeriod } from '@/api/accounting/fiscal-periods.api'
import { FxRevaluationPanel } from '@/components/accounting/FxRevaluationPanel'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

/** How the tenant closes its books: a closing entry every period, or one at the end of the fiscal year. */
function ClosingPolicyPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
    },
  })

  return (
    <div className="card space-y-3 p-4">
      <h2 className="text-sm font-semibold text-ink">{t('accounting.fiscalPeriods.policyTitle')}</h2>
      <div className="flex flex-wrap items-end gap-3">
        <FormField label={t('accounting.fiscalPeriods.policyMode')}>
          <Select disabled={!canManage} value={effectiveMode} onChange={(e) => setMode(e.target.value as ClosingMode)}>
            <option value="Monthly">{t('accounting.fiscalPeriods.policyMonthly')}</option>
            <option value="YearEnd">{t('accounting.fiscalPeriods.policyYearEnd')}</option>
          </Select>
        </FormField>
        <FormField label={t('accounting.fiscalPeriods.policyMonth')}>
          <Select disabled={!canManage} value={effectiveMonth} onChange={(e) => setMonth(Number(e.target.value))}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </Select>
        </FormField>
        {canManage && (
          <Button disabled={!dirty} loading={save.isPending} onClick={() => save.mutate()}>
            {t('common.save')}
          </Button>
        )}
      </div>
      <Checkbox disabled={!canManage} checked={effectiveAutoAccrue} onChange={(e) => setAutoAccrue(e.target.checked)} label={t('accounting.fiscalPeriods.autoAccrue')} />
      <p className="text-xs text-ink-secondary">{t('accounting.fiscalPeriods.autoAccrueHint')}</p>
      <p className="text-xs text-ink-secondary">
        {effectiveMode === 'YearEnd' ? t('accounting.fiscalPeriods.policyYearEndHint') : t('accounting.fiscalPeriods.policyMonthlyHint')}
      </p>
      {save.isError && <Alert variant="danger">{serverMessage(save.error, t('common.failedToSave'))}</Alert>}
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
  const toast = useToast()
  const {
    register,
    handleSubmit,
    getValues,
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('accounting.fiscalPeriods.openTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="fiscal-period-form" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form id="fiscal-period-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('accounting.fiscalPeriods.periodName')} required error={errors.periodName ? t('common.required') : undefined}>
          <Input {...register('periodName', { required: true })} />
        </FormField>
        <FormField label={t('accounting.fiscalPeriods.startDate')} required error={errors.startDate ? t('common.required') : undefined}>
          <Input type="date" {...register('startDate', { required: true })} />
        </FormField>
        <FormField
          label={t('accounting.fiscalPeriods.endDate')}
          required
          error={errors.endDate ? (errors.endDate.type === 'required' ? t('common.required') : t('accounting.fiscalPeriods.endBeforeStart')) : undefined}
        >
          <Input type="date" {...register('endDate', { required: true, validate: (v) => !getValues('startDate') || !v || v >= getValues('startDate') })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.fiscalPeriods.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function FiscalPeriodsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:periods:manage')
  const queryClient = useQueryClient()
  const toast = useToast()
  const [modalOpen, setModalOpen] = useState(false)
  const [confirmTarget, setConfirmTarget] = useState<FiscalPeriod | null>(null)
  const [reopenTarget, setReopenTarget] = useState<FiscalPeriod | null>(null)

  // No pagination — FiscalPeriodsController.List (ListFiscalPeriodsQuery) returns the full List<FiscalPeriodDto>.
  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'fiscalPeriods'],
    queryFn: () => fiscalPeriodsApi.list(),
    staleTime: 30_000,
  })

  // The API explains refusals (an earlier open period, draft entries, an unbalanced trial balance ...).
  const closeMutation = useMutation({
    mutationFn: (p: FiscalPeriod) => fiscalPeriodsApi.close(p.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      toast.success(t('common.saved'))
      setConfirmTarget(null)
    },
    onError: (e) => {
      setConfirmTarget(null)
      toast.error(serverMessage(e, t('common.error')))
    },
  })

  const reopenMutation = useMutation({
    mutationFn: ({ period, reason }: { period: FiscalPeriod; reason: string }) => fiscalPeriodsApi.reopen(period.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      toast.success(t('common.saved'))
      setReopenTarget(null)
    },
  })

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
            <Button variant="link-danger" className="text-xs" aria-label={`${t('accounting.fiscalPeriods.close')} ${p.periodName}`} onClick={() => setConfirmTarget(p)}>
              {t('accounting.fiscalPeriods.close')}
            </Button>
          ) : (
            <Button variant="link" className="text-xs" aria-label={`${t('accounting.fiscalPeriods.reopen')} ${p.periodName}`} onClick={() => setReopenTarget(p)}>
              {t('accounting.fiscalPeriods.reopen')}
            </Button>
          ),
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.fiscalPeriods.title')}</h1>
        {canManage && <Button onClick={() => setModalOpen(true)}>{t('accounting.fiscalPeriods.add')}</Button>}
      </div>

      <ClosingPolicyPanel canManage={canManage} />

      <FxRevaluationPanel canManage={canManage} />

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('accounting.fiscalPeriods.empty')}
            action={canManage ? <Button onClick={() => setModalOpen(true)}>{t('accounting.fiscalPeriods.add')}</Button> : undefined}
          />
        }
      />

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

      <ReasonDialog
        open={!!reopenTarget}
        title={t('accounting.fiscalPeriods.reopenTitle')}
        message={t('accounting.fiscalPeriods.reopenHint', { periodName: reopenTarget?.periodName })}
        confirmLabel={t('accounting.fiscalPeriods.reopen')}
        busy={reopenMutation.isPending}
        error={reopenMutation.isError ? serverMessage(reopenMutation.error, t('common.error')) : null}
        onConfirm={(reason) => reopenTarget && reopenMutation.mutate({ period: reopenTarget, reason })}
        onCancel={() => {
          setReopenTarget(null)
          reopenMutation.reset()
        }}
      />
    </div>
  )
}
