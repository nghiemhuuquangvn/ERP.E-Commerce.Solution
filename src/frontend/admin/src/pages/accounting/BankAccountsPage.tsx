import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { bankAccountsApi } from '@/api/accounting/bank-accounts.api'
import type { BankAccount, BankStatement } from '@/api/accounting/bank-accounts.api'
import { BankReconciliationModal } from '@/components/accounting/BankReconciliationModal'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="md"
      title={t('accounting.bankAccounts.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="bank-account-form" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form id="bank-account-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('accounting.bankAccounts.accountName')} required error={errors.accountName ? t('common.required') : undefined}>
          <Input maxLength={200} {...register('accountName', { required: true })} />
        </FormField>
        <FormField label={t('accounting.bankAccounts.bankName')} required error={errors.bankName ? t('common.required') : undefined}>
          <Input maxLength={200} {...register('bankName', { required: true })} />
        </FormField>
        <FormField label={t('accounting.bankAccounts.accountNumber')} required error={errors.accountNumber ? t('common.required') : undefined}>
          <Input maxLength={50} {...register('accountNumber', { required: true })} />
        </FormField>
        <div className="grid grid-cols-2 gap-4">
          <FormField label={t('accounting.bankAccounts.openingBalance')} required error={errors.openingBalance ? t('common.required') : undefined}>
            <Input type="number" step="0.01" min="0" {...register('openingBalance', { required: true })} />
          </FormField>
          <FormField label={t('accounting.bankAccounts.currencyCode')} required error={errors.currencyCode ? t('common.required') : undefined}>
            <Input maxLength={10} {...register('currencyCode', { required: true })} />
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.bankAccounts.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
  const [addOpen, setAddOpen] = useState(false)
  const [reconcileTarget, setReconcileTarget] = useState<BankStatement | null>(null)
  const [linesOf, setLinesOf] = useState<BankStatement | null>(null)

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
      toast.success(t('common.saved'))
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
      toast.success(t('accounting.bankRecon.reconciled'))
      setReconcileTarget(null)
    },
    onError: (e) => {
      setReconcileTarget(null)
      toast.error(serverMessage(e, t('accounting.bankRecon.failed')))
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
        align: 'right' as const,
        render: (s: BankStatement) => s.openingBalance.toFixed(2),
      },
      {
        key: 'closingBalance',
        label: t('accounting.bankAccounts.closingBalance'),
        align: 'right' as const,
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
        render: (s: BankStatement) => {
          const date = new Date(s.statementDate).toLocaleDateString()
          return (
            <span className="space-x-3">
              <Button variant="link" className="text-xs" aria-label={t('accounting.bankAccounts.linesOf', { date })} onClick={() => setLinesOf(s)}>
                {t('accounting.bankRecon.lines')}
              </Button>
              {canManage && s.status === 'Open' && (
                <Button variant="link" className="text-xs" aria-label={t('accounting.bankAccounts.reconcileOf', { date })} onClick={() => setReconcileTarget(s)}>
                  {t('accounting.bankAccounts.reconcile')}
                </Button>
              )}
            </span>
          )
        },
      },
    ],
    [canManage, t],
  )

  return (
    <>
      <Modal
        open
        size="lg"
        title={`${t('accounting.bankAccounts.statementsTitle')}: ${account.accountName}`}
        onClose={onClose}
        closeOnEsc={!linesOf && !reconcileTarget}
        footer={
          <Button variant="outline" onClick={onClose}>
            {t('common.close')}
          </Button>
        }
      >
        <div className="space-y-4">
          <DataTable
            columns={columns}
            data={statements ?? []}
            isLoading={isLoading}
            emptyState={
              <EmptyState
                title={t('accounting.bankAccounts.statementsEmpty')}
                action={canManage ? <Button onClick={() => setAddOpen(true)}>{t('accounting.bankAccounts.addStatement')}</Button> : undefined}
              />
            }
          />

          {canManage &&
            (!addOpen ? (
              <Button variant="outline" onClick={() => setAddOpen(true)}>
                {t('accounting.bankAccounts.addStatement')}
              </Button>
            ) : (
              <form
                noValidate
                onSubmit={handleSubmit((v) => addMutation.mutate(v))}
                className="space-y-3 rounded-md border border-line p-3"
              >
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                  <FormField label={t('accounting.bankAccounts.statementDate')} required error={errors.statementDate ? t('common.required') : undefined}>
                    <Input type="date" {...register('statementDate', { required: true })} />
                  </FormField>
                  <FormField label={t('accounting.bankAccounts.openingBalance')} required error={errors.openingBalance ? t('common.required') : undefined}>
                    <Input type="number" step="0.01" {...register('openingBalance', { required: true })} />
                  </FormField>
                  <FormField label={t('accounting.bankAccounts.closingBalance')} required error={errors.closingBalance ? t('common.required') : undefined}>
                    <Input type="number" step="0.01" {...register('closingBalance', { required: true })} />
                  </FormField>
                </div>
                {addMutation.isError && <Alert variant="danger">{serverMessage(addMutation.error, t('accounting.bankAccounts.failedToSave'))}</Alert>}
                <div className="flex gap-2">
                  <Button type="submit" loading={addMutation.isPending}>
                    {t('common.save')}
                  </Button>
                  <Button variant="outline" onClick={() => setAddOpen(false)}>
                    {t('common.cancel')}
                  </Button>
                </div>
              </form>
            ))}
        </div>
      </Modal>

      {linesOf && <BankReconciliationModal account={account} statement={linesOf} canManage={canManage} onClose={() => setLinesOf(null)} />}

      <ConfirmDialog
        open={!!reconcileTarget}
        title={t('accounting.bankAccounts.reconcileTitle')}
        message={t('accounting.bankAccounts.reconcileConfirm', {
          statementDate: reconcileTarget ? new Date(reconcileTarget.statementDate).toLocaleDateString() : '',
        })}
        confirmLabel={t('accounting.bankAccounts.reconcile')}
        onConfirm={() => reconcileTarget && !reconcileMutation.isPending && reconcileMutation.mutate(reconcileTarget)}
        onCancel={() => setReconcileTarget(null)}
      />
    </>
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
        align: 'right' as const,
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
          <Button variant="link" className="text-xs" aria-label={`${t('accounting.bankAccounts.viewStatements')}: ${a.accountName}`} onClick={() => setStatementsAccount(a)}>
            {t('accounting.bankAccounts.viewStatements')}
          </Button>
        ),
      },
    ],
    [t],
  )

  if (!canView) {
    return <p className="text-sm text-ink-muted">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.bankAccounts.title')}</h1>
        {canManage && <Button onClick={() => setCreateOpen(true)}>{t('accounting.bankAccounts.add')}</Button>}
      </div>

      <Checkbox checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} label={t('accounting.bankAccounts.activeOnly')} />

      <DataTable
        columns={columns}
        data={accounts}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('accounting.bankAccounts.empty')}
            action={canManage ? <Button onClick={() => setCreateOpen(true)}>{t('accounting.bankAccounts.add')}</Button> : undefined}
          />
        }
      />

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
