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
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface AccountFormValues {
  accountNumber: string
  name: string
  accountType: number
  normalBalance: number
  parentAccountId: string
  isCash: boolean
  cashFlowCategory: number // 0 = default
}

/** The API answers refusals as ProblemDetails with a readable `detail` (duplicate number, parent rules ...). */
function errorDetail(error: unknown, fallback: string): string {
  const detail = (error as { response?: { data?: { detail?: string; message?: string } } })?.response?.data
  return detail?.detail ?? detail?.message ?? fallback
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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {editing ? t('accounting.accounts.editTitle') : t('accounting.accounts.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.accounts.accountNumber')}
            </label>
            <input className="input" disabled={editing} {...register('accountNumber', { required: true })} />
            {errors.accountNumber && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.accounts.accountNumber')} is required.
              </p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.accounts.accountType')}
            </label>
            <select className="input" disabled={editing} {...register('accountType', { valueAsNumber: true })}>
              {ACCOUNT_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.accounts.normalBalance')}
            </label>
            <select className="input" disabled={editing} {...register('normalBalance', { valueAsNumber: true })}>
              {NORMAL_BALANCE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.accounts.parent')}
            </label>
            <select className="input" {...register('parentAccountId')}>
              <option value="">{t('accounting.accounts.noParent')}</option>
              {parentChoices.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.accountNumber} — {a.name}
                </option>
              ))}
            </select>
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" {...register('isCash')} />
            {t('accounting.accounts.isCash')}
          </label>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.accounts.cashFlowCategory')}
            </label>
            <select className="input" {...register('cashFlowCategory', { valueAsNumber: true })}>
              <option value={0}>
                {t('accounting.accounts.cashFlowDefault')}
                {editing ? ` (${account?.cashFlowCategory})` : ''}
              </option>
              {CASH_FLOW_CATEGORY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{errorDetail(mutation.error, t('accounting.accounts.failedToSave'))}</p>
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

function ApplyTemplateModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [templateId, setTemplateId] = useState('')

  const { data: templates } = useQuery({
    queryKey: ['accounting', 'chart-templates'],
    queryFn: () => accountsApi.templates(),
    staleTime: 5 * 60_000,
  })

  const mutation = useMutation({
    mutationFn: () => accountsApi.applyTemplate(templateId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      onClose()
    },
  })

  const selected = templates?.find((x) => x.id === templateId)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-2 text-base font-semibold text-gray-900">{t('accounting.accounts.templateTitle')}</h3>
        <p className="mb-4 text-sm text-gray-600">{t('accounting.accounts.templateHint')}</p>
        <select className="input mb-3" value={templateId} onChange={(e) => setTemplateId(e.target.value)}>
          <option value="">{t('accounting.accounts.chooseTemplate')}</option>
          {(templates ?? []).map((x) => (
            <option key={x.id} value={x.id}>
              {x.name} ({x.accountCount})
            </option>
          ))}
        </select>
        {selected && <p className="mb-3 text-xs text-gray-500">{selected.description}</p>}
        {selected?.id === 'Vas99' && <p className="mb-3 text-xs text-amber-700">{t('accounting.accounts.templateReview')}</p>}
        {mutation.isError && (
          <p className="mb-3 text-xs text-red-600">{errorDetail(mutation.error, t('accounting.accounts.failedToSave'))}</p>
        )}
        <div className="flex justify-end gap-3">
          <button type="button" onClick={onClose} className="btn-outline">
            {t('common.cancel')}
          </button>
          <button
            type="button"
            disabled={!templateId || mutation.isPending}
            onClick={() => mutation.mutate()}
            className="btn-primary"
          >
            {mutation.isPending ? t('common.saving') : t('accounting.accounts.applyTemplate')}
          </button>
        </div>
      </div>
    </div>
  )
}

export function AccountsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:accounts:manage')
  const [modalAccount, setModalAccount] = useState<Account | null | undefined>(undefined) // undefined = closed, null = new
  const [templateOpen, setTemplateOpen] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

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
      setActionError(null)
      queryClient.invalidateQueries({ queryKey: ['accounting', 'accounts'] })
    },
    onError: (e) => setActionError(errorDetail(e, t('accounting.accounts.failedToSave'))),
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
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setModalAccount(a)}>
                {t('common.edit')}
              </button>
              <button
                className="text-xs text-gray-600 hover:underline"
                disabled={toggleActive.isPending}
                onClick={() => toggleActive.mutate(a)}
              >
                {a.isActive ? t('accounting.accounts.deactivate') : t('accounting.accounts.activate')}
              </button>
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
            <button className="btn-outline" onClick={() => setTemplateOpen(true)}>
              {t('accounting.accounts.applyTemplate')}
            </button>
            <button className="btn-primary" onClick={() => setModalAccount(null)}>
              {t('accounting.accounts.add')}
            </button>
          </div>
        )}
      </div>

      {actionError && <p className="text-sm text-red-600">{actionError}</p>}

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalAccount !== undefined && (
        <AccountModal account={modalAccount} accounts={data ?? []} onClose={() => setModalAccount(undefined)} />
      )}
      {templateOpen && <ApplyTemplateModal onClose={() => setTemplateOpen(false)} />}
    </div>
  )
}
