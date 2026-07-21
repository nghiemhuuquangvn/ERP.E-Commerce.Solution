import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { bankAccountsApi } from '@/api/accounting/bank-accounts.api'
import type { BankAccount, BankStatement } from '@/api/accounting/bank-accounts.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface BankAccountFormValues {
  accountName: string
  bankName: string
  accountNumber: string
  openingBalance: string
  currencyCode: string
}

function BankAccountCreateModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<BankAccountFormValues>({
    defaultValues: {
      accountName: '',
      bankName: '',
      accountNumber: '',
      openingBalance: '0',
      currencyCode: 'VND',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: BankAccountFormValues) =>
      bankAccountsApi.create({
        accountName: values.accountName,
        bankName: values.bankName,
        accountNumber: values.accountNumber,
        openingBalance: parseFloat(values.openingBalance) || 0,
        currencyCode: values.currencyCode,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'bank-accounts'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('accounting.bankAccounts.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.bankAccounts.accountName')}
            </label>
            <input className="input" maxLength={200} {...register('accountName', { required: true })} />
            {errors.accountName && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.bankAccounts.accountName')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.bankAccounts.bankName')}
            </label>
            <input className="input" maxLength={200} {...register('bankName', { required: true })} />
            {errors.bankName && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.bankAccounts.bankName')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.bankAccounts.accountNumber')}
            </label>
            <input className="input" maxLength={50} {...register('accountNumber', { required: true })} />
            {errors.accountNumber && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.bankAccounts.accountNumber')} is required.
              </p>
            )}
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('accounting.bankAccounts.openingBalance')}
              </label>
              <input
                type="number"
                step="0.01"
                min="0"
                className="input"
                {...register('openingBalance', { required: true })}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('accounting.bankAccounts.currencyCode')}
              </label>
              <input className="input" maxLength={10} {...register('currencyCode', { required: true })} />
            </div>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('accounting.bankAccounts.failedToSave')}</p>
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

interface StatementFormValues {
  statementDate: string
  openingBalance: string
  closingBalance: string
}

function StatementsModal({
  account,
  canManage,
  onClose,
}: {
  account: BankAccount
  canManage: boolean
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [reconcileTarget, setReconcileTarget] = useState<BankStatement | null>(null)

  const { data: statements, isLoading } = useQuery({
    queryKey: ['accounting', 'bank-accounts', account.id, 'statements'],
    queryFn: () => bankAccountsApi.listStatements(account.id),
    staleTime: 15_000,
  })

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<StatementFormValues>({
    defaultValues: { statementDate: '', openingBalance: '', closingBalance: '' },
  })

  const addMutation = useMutation({
    mutationFn: (values: StatementFormValues) =>
      bankAccountsApi.createStatement(account.id, {
        statementDate: values.statementDate,
        openingBalance: parseFloat(values.openingBalance) || 0,
        closingBalance: parseFloat(values.closingBalance) || 0,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['accounting', 'bank-accounts', account.id, 'statements'],
      })
      reset()
      setAddOpen(false)
    },
  })

  const reconcileMutation = useMutation({
    mutationFn: (statement: BankStatement) => bankAccountsApi.reconcileStatement(account.id, statement.id),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['accounting', 'bank-accounts', account.id, 'statements'],
      })
      setReconcileTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      {
        key: 'statementDate',
        label: t('accounting.bankAccounts.statementDate'),
        render: (s: BankStatement) => new Date(s.statementDate).toLocaleDateString(),
      },
      {
        key: 'openingBalance',
        label: t('accounting.bankAccounts.openingBalance'),
        render: (s: BankStatement) => s.openingBalance.toFixed(2),
      },
      {
        key: 'closingBalance',
        label: t('accounting.bankAccounts.closingBalance'),
        render: (s: BankStatement) => s.closingBalance.toFixed(2),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (s: BankStatement) => <StatusBadge status={s.status} />,
      },
      {
        key: 'actions',
        label: '',
        render: (s: BankStatement) =>
          canManage && s.status === 'Open' ? (
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setReconcileTarget(s)}
            >
              {t('accounting.bankAccounts.reconcile')}
            </button>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-3xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('accounting.bankAccounts.statementsTitle')}: {account.accountName}
        </h3>

        <DataTable columns={columns} data={statements ?? []} isLoading={isLoading} />

        {canManage && (
          <div className="mt-4">
            {!addOpen ? (
              <button className="btn-outline" onClick={() => setAddOpen(true)}>
                {t('accounting.bankAccounts.addStatement')}
              </button>
            ) : (
              <form
                onSubmit={handleSubmit((v) => addMutation.mutate(v))}
                className="mt-2 grid grid-cols-4 items-end gap-3 rounded-md border border-gray-200 p-3"
              >
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-700">
                    {t('accounting.bankAccounts.statementDate')}
                  </label>
                  <input type="date" className="input" {...register('statementDate', { required: true })} />
                  {errors.statementDate && <p className="text-xs text-red-600">{t('common.select')}</p>}
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-700">
                    {t('accounting.bankAccounts.openingBalance')}
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    className="input"
                    {...register('openingBalance', { required: true })}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-700">
                    {t('accounting.bankAccounts.closingBalance')}
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    className="input"
                    {...register('closingBalance', { required: true })}
                  />
                </div>
                <div className="flex gap-2">
                  <button type="submit" disabled={addMutation.isPending} className="btn-primary">
                    {addMutation.isPending ? t('common.saving') : t('common.save')}
                  </button>
                  <button type="button" className="btn-outline" onClick={() => setAddOpen(false)}>
                    {t('common.cancel')}
                  </button>
                </div>
              </form>
            )}
            {addMutation.isError && (
              <p className="mt-2 text-xs text-red-600">{t('accounting.bankAccounts.failedToSave')}</p>
            )}
          </div>
        )}

        <div className="flex justify-end pt-4">
          <button className="btn-outline" onClick={onClose}>
            {t('common.close')}
          </button>
        </div>

        <ConfirmDialog
          open={!!reconcileTarget}
          title={t('accounting.bankAccounts.reconcileTitle')}
          message={t('accounting.bankAccounts.reconcileConfirm', {
            statementDate: reconcileTarget ? new Date(reconcileTarget.statementDate).toLocaleDateString() : '',
          })}
          confirmLabel={t('accounting.bankAccounts.reconcile')}
          onConfirm={() => reconcileTarget && reconcileMutation.mutate(reconcileTarget)}
          onCancel={() => setReconcileTarget(null)}
        />
      </div>
    </div>
  )
}

export function BankAccountsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [activeOnly, setActiveOnly] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [statementsAccount, setStatementsAccount] = useState<BankAccount | null>(null)

  const canView = hasPermission('accounting:bank:view')
  const canManage = hasPermission('accounting:bank:manage')

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'bank-accounts', { activeOnly }],
    queryFn: () => bankAccountsApi.list({ activeOnly: activeOnly || undefined }),
    staleTime: 30_000,
    enabled: canView,
  })

  const accounts = data ?? []

  const columns = useMemo(
    () => [
      { key: 'accountName', label: t('accounting.bankAccounts.accountName') },
      { key: 'bankName', label: t('accounting.bankAccounts.bankName') },
      {
        key: 'accountNumber',
        label: t('accounting.bankAccounts.accountNumber'),
        className: 'font-mono text-xs',
      },
      {
        key: 'currentBalance',
        label: t('accounting.bankAccounts.currentBalance'),
        render: (a: BankAccount) => `${a.currentBalance.toFixed(2)} ${a.currencyCode}`,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (a: BankAccount) => (
          <StatusBadge
            status={a.isActive ? 'Active' : 'Inactive'}
            label={a.isActive ? t('accounting.bankAccounts.active') : t('accounting.bankAccounts.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (a: BankAccount) => (
          <button
            className="text-xs text-primary-600 hover:underline"
            onClick={() => setStatementsAccount(a)}
          >
            {t('accounting.bankAccounts.viewStatements')}
          </button>
        ),
      },
    ],
    [t],
  )

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.bankAccounts.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setCreateOpen(true)}>
            {t('accounting.bankAccounts.add')}
          </button>
        )}
      </div>

      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} />
        {t('accounting.bankAccounts.activeOnly')}
      </label>

      <DataTable columns={columns} data={accounts} isLoading={isLoading} />

      {createOpen && <BankAccountCreateModal onClose={() => setCreateOpen(false)} />}

      {statementsAccount && (
        <StatementsModal
          account={statementsAccount}
          canManage={canManage}
          onClose={() => setStatementsAccount(null)}
        />
      )}
    </div>
  )
}
