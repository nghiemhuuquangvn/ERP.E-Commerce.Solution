import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  accountsApi,
  ACCOUNT_TYPE_OPTIONS,
  NORMAL_BALANCE_OPTIONS,
} from '@/api/accounting/accounts.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'

interface AccountFormValues {
  accountNumber: string
  name: string
  accountType: number
  normalBalance: number
}

function CreateAccountModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<AccountFormValues>({
    defaultValues: { accountNumber: '', name: '', accountType: 1, normalBalance: 1 },
  })

  const mutation = useMutation({
    mutationFn: (v: AccountFormValues) =>
      accountsApi.create({
        accountNumber: v.accountNumber,
        name: v.name,
        accountType: Number(v.accountType),
        normalBalance: Number(v.normalBalance),
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
          {t('accounting.accounts.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.accounts.accountNumber')}
            </label>
            <input className="input" {...register('accountNumber', { required: true })} />
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
            <select className="input" {...register('accountType', { valueAsNumber: true })}>
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
            <select className="input" {...register('normalBalance', { valueAsNumber: true })}>
              {NORMAL_BALANCE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('accounting.accounts.failedToSave')}</p>
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

export function AccountsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:accounts:manage')
  const [modalOpen, setModalOpen] = useState(false)

  // No pagination — AccountsController.List (ListAccountsQuery) returns the full List<AccountDto>.
  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'accounts'],
    queryFn: () => accountsApi.list(),
    staleTime: 30_000,
  })

  const columns = useMemo(
    () => [
      {
        key: 'accountNumber',
        label: t('accounting.accounts.accountNumber'),
        className: 'font-mono text-xs',
      },
      { key: 'name', label: t('common.name') },
      { key: 'accountType', label: t('accounting.accounts.accountType') },
      { key: 'normalBalance', label: t('accounting.accounts.normalBalance') },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.accounts.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {t('accounting.accounts.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalOpen && <CreateAccountModal onClose={() => setModalOpen(false)} />}
    </div>
  )
}
