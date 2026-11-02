import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  accountsApi,
  ACCOUNT_TYPE_OPTIONS,
  NORMAL_BALANCE_OPTIONS,
  CASH_FLOW_CATEGORY_OPTIONS,
} from '@/api/accounting/accounts.api'
import type { Account } from '@/api/accounting/accounts.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface AccountFormValues {
  accountNumber: string
  name: string
  accountType: number
  normalBalance: number
  parentAccountId: string
  isCash: boolean
  cashFlowCategory: number // 0 = default
}

function AccountModal({
  account,
  accounts,
  onClose,
}: {
  /** null = create a new account; otherwise rename / move this one (number, type and normal balance never change). */
  account: Account | null
  accounts: Account[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const editing = account !== null
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<AccountFormValues>({
    defaultValues: {
      accountNumber: account?.accountNumber ?? '',
      name: account?.name ?? '',
      accountType: ACCOUNT_TYPE_OPTIONS.find((o) => o.label === account?.accountType)?.value ?? 1,
      normalBalance: NORMAL_BALANCE_OPTIONS.find((o) => o.label === account?.normalBalance)?.value ?? 1,
      parentAccountId: account?.parentAccountId ?? '',
      isCash: account?.isCash ?? false,
      cashFlowCategory: 0,
    },
  })

  const type = Number(watch('accountType'))
  // A sub-account has the same type as its parent; an account cannot sit under itself.
  const parentChoices = accounts.filter(
    (a) => a.isActive && a.id !== account?.id && ACCOUNT_TYPE_OPTIONS.find((o) => o.label === a.accountType)?.value === type,
  )

  const mutation = useMutation({
    mutationFn: (v: AccountFormValues) =>
      editing
        ? accountsApi.update(account.id, {
            name: v.name,
            parentAccountId: v.parentAccountId || null,
            isCash: v.isCash,
            cashFlowCategory: Number(v.cashFlowCategory),
          })
        : accountsApi.create({
            accountNumber: v.accountNumber,
            name: v.name,
            accountType: Number(v.accountType),
            normalBalance: Number(v.normalBalance),
            parentAccountId: v.parentAccountId || null,
            isCash: v.isCash,
            cashFlowCategory: Number(v.cashFlowCategory) || null,
          }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'accounts'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="md"
      title={editing ? t('accounting.accounts.editTitle') : t('accounting.accounts.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="account-form" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form id="account-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField
          label={t('accounting.accounts.accountNumber')}
          required
          hint={editing ? t('accounting.accounts.lockedHint') : undefined}
          error={errors.accountNumber ? t('common.required') : undefined}
        >
          <Input disabled={editing} {...register('accountNumber', { required: true })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('accounting.accounts.accountType')} hint={editing ? t('accounting.accounts.lockedHint') : undefined}>
          <Select disabled={editing} {...register('accountType', { valueAsNumber: true })}>
            {ACCOUNT_TYPE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('accounting.accounts.normalBalance')} hint={editing ? t('accounting.accounts.lockedHint') : undefined}>
          <Select disabled={editing} {...register('normalBalance', { valueAsNumber: true })}>
            {NORMAL_BALANCE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('accounting.accounts.parent')} hint={t('accounting.accounts.parentHint')}>
          <Select {...register('parentAccountId')}>
            <option value="">{t('accounting.accounts.noParent')}</option>
            {parentChoices.map((a) => (
              <option key={a.id} value={a.id}>
                {a.accountNumber} — {a.name}
              </option>
            ))}
          </Select>
        </FormField>
        <Checkbox label={t('accounting.accounts.isCash')} {...register('isCash')} />
        <FormField label={t('accounting.accounts.cashFlowCategory')}>
          <Select {...register('cashFlowCategory', { valueAsNumber: true })}>
            <option value={0}>
              {t('accounting.accounts.cashFlowDefault')}
              {editing ? ` (${account?.cashFlowCategory})` : ''}
            </option>
            {CASH_FLOW_CATEGORY_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.accounts.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

function ApplyTemplateModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [templateId, setTemplateId] = useState('')
  const [confirming, setConfirming] = useState(false)

  const { data: templates } = useQuery({
    queryKey: ['accounting', 'chart-templates'],
    queryFn: () => accountsApi.templates(),
    staleTime: 5 * 60_000,
  })

  const mutation = useMutation({
    mutationFn: () => accountsApi.applyTemplate(templateId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      toast.success(t('common.saved'))
      onClose()
    },
    onError: () => setConfirming(false),
  })

  const selected = templates?.find((x) => x.id === templateId)

  return (
    <>
      <Modal
        open
        size="sm"
        title={t('accounting.accounts.templateTitle')}
        onClose={onClose}
        closeOnEsc={!confirming && !mutation.isPending}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button disabled={!templateId} loading={mutation.isPending} onClick={() => setConfirming(true)}>
              {t('accounting.accounts.applyTemplate')}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <p className="text-sm text-ink-secondary">{t('accounting.accounts.templateHint')}</p>
          <FormField label={t('accounting.accounts.applyTemplate')}>
            <Select value={templateId} onChange={(e) => setTemplateId(e.target.value)}>
              <option value="">{t('accounting.accounts.chooseTemplate')}</option>
              {(templates ?? []).map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name} ({x.accountCount})
                </option>
              ))}
            </Select>
          </FormField>
          {selected && <p className="text-xs text-ink-secondary">{selected.description}</p>}
          {selected?.id === 'Vas99' && <Alert variant="warning">{t('accounting.accounts.templateReview')}</Alert>}
          {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.accounts.failedToSave'))}</Alert>}
        </div>
      </Modal>
      <ConfirmDialog
        open={confirming}
        title={t('accounting.accounts.templateConfirmTitle')}
        message={t('accounting.accounts.templateConfirm', { name: selected?.name, count: selected?.accountCount })}
        confirmLabel={t('accounting.accounts.applyTemplate')}
        onConfirm={() => !mutation.isPending && mutation.mutate()}
        onCancel={() => setConfirming(false)}
      />
    </>
  )
}

export function AccountsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:accounts:manage')
  const [modalAccount, setModalAccount] = useState<Account | null | undefined>(undefined) // undefined = closed, null = new
  const [templateOpen, setTemplateOpen] = useState(false)
  const [deactivateTarget, setDeactivateTarget] = useState<Account | null>(null)
  const toast = useToast()

  // No pagination — AccountsController.List (ListAccountsQuery) returns the full List<AccountDto>, ordered by number,
  // which already lists a parent right before its sub-accounts for the numbering schemes we ship.
  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'accounts'],
    queryFn: () => accountsApi.list(),
    staleTime: 30_000,
  })

  const toggleActive = useMutation({
    mutationFn: (a: Account) => (a.isActive ? accountsApi.deactivate(a.id) : accountsApi.activate(a.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'accounts'] })
      toast.success(t('common.saved'))
      setDeactivateTarget(null)
    },
    onError: (e) => {
      setDeactivateTarget(null)
      toast.error(serverMessage(e, t('accounting.accounts.failedToSave')))
    },
  })

  const columns = useMemo(
    () => [
      {
        key: 'accountNumber',
        label: t('accounting.accounts.accountNumber'),
        className: 'font-mono text-xs',
        render: (a: Account) => (
          <span style={{ paddingLeft: `${(a.level - 1) * 16}px` }} className={a.isPostable ? '' : 'font-semibold'}>
            {a.accountNumber}
          </span>
        ),
      },
      { key: 'name', label: t('common.name') },
      { key: 'accountType', label: t('accounting.accounts.accountType') },
      { key: 'normalBalance', label: t('accounting.accounts.normalBalance') },
      {
        key: 'isPostable',
        label: t('accounting.accounts.postable'),
        render: (a: Account) => (a.isPostable ? t('common.yes') : t('accounting.accounts.groupsOnly')),
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (a: Account) => (
          <StatusBadge status={a.isActive ? 'Active' : 'Inactive'} />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (a: Account) =>
          canManage ? (
            <span className="flex gap-3">
              <Button variant="link" className="text-xs" aria-label={`${t('common.edit')} ${a.accountNumber}`} onClick={() => setModalAccount(a)}>
                {t('common.edit')}
              </Button>
              <Button
                variant={a.isActive ? 'link-danger' : 'link'}
                className="text-xs"
                aria-label={`${a.isActive ? t('accounting.accounts.deactivate') : t('accounting.accounts.activate')} ${a.accountNumber}`}
                disabled={toggleActive.isPending}
                onClick={() => (a.isActive ? setDeactivateTarget(a) : toggleActive.mutate(a))}
              >
                {a.isActive ? t('accounting.accounts.deactivate') : t('accounting.accounts.activate')}
              </Button>
            </span>
          ) : null,
      },
    ],
    [canManage, t, toggleActive],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.accounts.title')}</h1>
        {canManage && (
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => setTemplateOpen(true)}>
              {t('accounting.accounts.applyTemplate')}
            </Button>
            <Button onClick={() => setModalAccount(null)}>{t('accounting.accounts.add')}</Button>
          </div>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('accounting.accounts.empty')}
            action={canManage ? <Button onClick={() => setModalAccount(null)}>{t('accounting.accounts.add')}</Button> : undefined}
          />
        }
      />

      {modalAccount !== undefined && (
        <AccountModal account={modalAccount} accounts={data ?? []} onClose={() => setModalAccount(undefined)} />
      )}
      {templateOpen && <ApplyTemplateModal onClose={() => setTemplateOpen(false)} />}

      <ConfirmDialog
        open={!!deactivateTarget}
        title={t('accounting.accounts.deactivateTitle')}
        message={t('accounting.accounts.deactivateConfirm', { number: deactivateTarget?.accountNumber, name: deactivateTarget?.name })}
        confirmLabel={t('accounting.accounts.deactivate')}
        danger
        onConfirm={() => deactivateTarget && toggleActive.mutate(deactivateTarget)}
        onCancel={() => setDeactivateTarget(null)}
      />
    </div>
  )
}
