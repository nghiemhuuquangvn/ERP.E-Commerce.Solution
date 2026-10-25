import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { accountsApi } from '@/api/accounting/accounts.api'
import { offBalanceApi } from '@/api/accounting/off-balance.api'
import type { OffBalanceDirection, OffBalanceEntry } from '@/api/accounting/off-balance.api'
import { DataTable } from '@/components/shared/DataTable'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { usePermission } from '@/hooks/usePermission'

const detail = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? fallback

/** The off-balance register: movements of the class 0 accounts (001 – 008), outside the double entry. */
export function OffBalancePage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:journals:manage')
  const queryClient = useQueryClient()

  const [accountId, setAccountId] = useState('')
  const [form, setForm] = useState({ accountId: '', direction: 'In' as OffBalanceDirection, amount: '', entryDate: '', description: '', reference: '' })
  const [voidTarget, setVoidTarget] = useState<OffBalanceEntry | null>(null)

  const { data: accounts } = useQuery({ queryKey: ['accounting', 'accounts'], queryFn: () => accountsApi.list(), staleTime: 60_000 })
  const offBalanceAccounts = useMemo(() => (accounts ?? []).filter((a) => a.accountType === 'OffBalance'), [accounts])

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'offBalance', accountId],
    queryFn: () => offBalanceApi.list({ accountId: accountId || undefined }),
    staleTime: 15_000,
  })

  const post = useMutation({
    mutationFn: () =>
      offBalanceApi.post({
        accountId: form.accountId,
        direction: form.direction,
        amount: Number(form.amount),
        entryDate: form.entryDate || undefined,
        description: form.description || undefined,
        reference: form.reference || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'offBalance'] })
      setForm({ ...form, amount: '', description: '', reference: '' })
    },
  })

  const voidMutation = useMutation({
    mutationFn: ({ entry, reason }: { entry: OffBalanceEntry; reason: string }) => offBalanceApi.void(entry.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'offBalance'] })
      setVoidTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'entryDate', label: t('accounting.offBalance.date'), render: (e: OffBalanceEntry) => e.entryDate.slice(0, 10) },
      { key: 'accountNumber', label: t('accounting.offBalance.account'), render: (e: OffBalanceEntry) => `${e.accountNumber} ${e.accountName}` },
      { key: 'direction', label: t('accounting.offBalance.direction'), render: (e: OffBalanceEntry) => t(`accounting.offBalance.dir${e.direction}`) },
      { key: 'amount', label: t('accounting.offBalance.amount'), render: (e: OffBalanceEntry) => e.amount.toLocaleString() },
      { key: 'description', label: t('accounting.offBalance.description'), render: (e: OffBalanceEntry) => e.description ?? '' },
      { key: 'reference', label: t('accounting.offBalance.reference'), render: (e: OffBalanceEntry) => e.reference ?? '' },
      { key: 'status', label: t('common.status'), render: (e: OffBalanceEntry) => (e.status === 'Voided' ? `${e.status}: ${e.voidReason ?? ''}` : e.status) },
      {
        key: 'actions',
        label: '',
        render: (e: OffBalanceEntry) =>
          canManage && e.status === 'Posted' ? (
            <button className="text-xs text-red-600 hover:underline" onClick={() => setVoidTarget(e)}>
              {t('accounting.offBalance.void')}
            </button>
          ) : null,
      },
    ],
    [t, canManage],
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.offBalance.title')}</h1>
      <p className="text-sm text-gray-500">{t('accounting.offBalance.hint')}</p>

      {canManage && (
        <div className="card flex flex-wrap items-end gap-3 p-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.account')}</label>
            <select className="input" value={form.accountId} onChange={(e) => setForm({ ...form, accountId: e.target.value })}>
              <option value="">—</option>
              {offBalanceAccounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.accountNumber} {a.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.direction')}</label>
            <select className="input" value={form.direction} onChange={(e) => setForm({ ...form, direction: e.target.value as OffBalanceDirection })}>
              <option value="In">{t('accounting.offBalance.dirIn')}</option>
              <option value="Out">{t('accounting.offBalance.dirOut')}</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.amount')}</label>
            <input type="number" min="0" className="input" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.date')}</label>
            <input type="date" className="input" value={form.entryDate} onChange={(e) => setForm({ ...form, entryDate: e.target.value })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.description')}</label>
            <input className="input" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.reference')}</label>
            <input className="input" value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} />
          </div>
          <button className="btn-primary" disabled={!form.accountId || !(Number(form.amount) > 0) || post.isPending} onClick={() => post.mutate()}>
            {t('accounting.offBalance.add')}
          </button>
          {post.isError && <span className="text-sm text-red-600">{detail(post.error, t('accounting.offBalance.failed'))}</span>}
        </div>
      )}

      <div>
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.offBalance.filterAccount')}</label>
        <select className="input" value={accountId} onChange={(e) => setAccountId(e.target.value)}>
          <option value="">{t('accounting.offBalance.allAccounts')}</option>
          {offBalanceAccounts.map((a) => (
            <option key={a.id} value={a.id}>
              {a.accountNumber} {a.name}
            </option>
          ))}
        </select>
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      <ReasonDialog
        open={!!voidTarget}
        title={t('accounting.offBalance.voidTitle')}
        message={voidTarget ? `${voidTarget.accountNumber} · ${voidTarget.amount.toLocaleString()}` : undefined}
        confirmLabel={t('accounting.offBalance.void')}
        danger
        busy={voidMutation.isPending}
        error={voidMutation.isError ? detail(voidMutation.error, t('accounting.offBalance.failed')) : null}
        onConfirm={(reason) => voidTarget && voidMutation.mutate({ entry: voidTarget, reason })}
        onCancel={() => setVoidTarget(null)}
      />
    </div>
  )
}
