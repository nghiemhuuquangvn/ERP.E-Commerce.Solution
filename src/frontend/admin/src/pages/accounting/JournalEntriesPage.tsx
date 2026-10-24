import { useMemo, useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { journalEntriesApi } from '@/api/accounting/journal-entries.api'
import type { JournalEntry } from '@/api/accounting/journal-entries.api'
import { apiClient, ACC_BASE } from '@/api/accounting/accClient'
import { DataTable } from '@/components/shared/DataTable'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const PAGE_SIZE = 20

// Read-only cross-reference to the Accounts list (AccountsPage/accounts.api.ts
// owns the real entity) — just enough shape to populate the line-item AccountId
// <select>. Fetched directly here, not via a shared accounts.api.ts import, to
// avoid coupling to a file owned by a different page.
interface AccountRef {
  id: string
  accountNumber: string
  name: string
}

// Read-only cross-reference to the Fiscal Periods list — just enough shape for
// the list filter dropdown and the create form's FiscalPeriodId <select>.
interface FiscalPeriodRef {
  id: string
  periodName: string
  status: string
}

interface JournalLineFormValues {
  accountId: string
  debit: string
  credit: string
  description: string
}

interface JournalEntryFormValues {
  entryNumber: string
  fiscalPeriodId: string
  description: string
  lines: JournalLineFormValues[]
}

const emptyLine: JournalLineFormValues = { accountId: '', debit: '', credit: '', description: '' }

function JournalEntryCreateModal({
  accounts,
  fiscalPeriods,
  onClose,
}: {
  accounts: AccountRef[]
  fiscalPeriods: FiscalPeriodRef[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const {
    register,
    control,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<JournalEntryFormValues>({
    defaultValues: {
      entryNumber: '',
      fiscalPeriodId: '',
      description: '',
      lines: [emptyLine, emptyLine],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'lines' })
  const lines = watch('lines')

  const { totalDebit, totalCredit } = useMemo(() => {
    return (lines ?? []).reduce(
      (acc, l) => ({
        totalDebit: acc.totalDebit + (parseFloat(l.debit) || 0),
        totalCredit: acc.totalCredit + (parseFloat(l.credit) || 0),
      }),
      { totalDebit: 0, totalCredit: 0 },
    )
  }, [lines])

  const isBalanced = totalDebit === totalCredit && totalDebit > 0

  const mutation = useMutation({
    mutationFn: (values: JournalEntryFormValues) =>
      journalEntriesApi.create({
        entryNumber: values.entryNumber,
        fiscalPeriodId: values.fiscalPeriodId,
        description: values.description,
        lines: values.lines.map((l) => ({
          accountId: l.accountId,
          debit: parseFloat(l.debit) || 0,
          credit: parseFloat(l.credit) || 0,
          description: l.description,
        })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'journal-entries'] })
      onClose()
    },
  })

  const onSubmit = (values: JournalEntryFormValues) => {
    if (!isBalanced) return
    mutation.mutate(values)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-3xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('accounting.journalEntries.addTitle')}
        </h3>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('accounting.journalEntries.entryNumber')}
              </label>
              <input className="input" maxLength={50} {...register('entryNumber', { required: true })} />
              {errors.entryNumber && (
                <p className="mt-1 text-xs text-red-600">{t('accounting.journalEntries.entryNumber')} is required.</p>
              )}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('accounting.journalEntries.fiscalPeriod')}
              </label>
              <select className="input" {...register('fiscalPeriodId', { required: true })}>
                <option value="">{t('common.select')}</option>
                {fiscalPeriods.map((fp) => (
                  <option key={fp.id} value={fp.id}>
                    {fp.periodName}
                  </option>
                ))}
              </select>
              {errors.fiscalPeriodId && (
                <p className="mt-1 text-xs text-red-600">
                  {t('accounting.journalEntries.fiscalPeriod')} is required.
                </p>
              )}
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.journalEntries.description')}
            </label>
            <textarea className="input" rows={2} maxLength={500} {...register('description', { required: true })} />
            {errors.description && (
              <p className="mt-1 text-xs text-red-600">
                {t('accounting.journalEntries.description')} is required.
              </p>
            )}
          </div>

          <div>
            <div className="mb-2 flex items-center justify-between">
              <label className="block text-sm font-medium text-gray-700">
                {t('accounting.journalEntries.lines')}
              </label>
              <button type="button" className="btn-outline text-xs" onClick={() => append(emptyLine)}>
                {t('accounting.journalEntries.addLine')}
              </button>
            </div>
            <div className="space-y-2">
              {fields.map((field, index) => (
                <div key={field.id} className="grid grid-cols-12 items-start gap-2">
                  <div className="col-span-4">
                    <select
                      className="input"
                      {...register(`lines.${index}.accountId` as const, { required: true })}
                    >
                      <option value="">{t('accounting.journalEntries.account')}</option>
                      {accounts.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.accountNumber} — {a.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="0.01"
                      className="input"
                      placeholder={t('accounting.journalEntries.debit') ?? ''}
                      {...register(`lines.${index}.debit` as const)}
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="0.01"
                      className="input"
                      placeholder={t('accounting.journalEntries.credit') ?? ''}
                      {...register(`lines.${index}.credit` as const)}
                    />
                  </div>
                  <div className="col-span-3">
                    <input
                      className="input"
                      placeholder={t('accounting.journalEntries.lineDescription') ?? ''}
                      {...register(`lines.${index}.description` as const)}
                    />
                  </div>
                  <div className="col-span-1">
                    <button
                      type="button"
                      className="text-xs text-red-600 hover:underline disabled:opacity-30"
                      disabled={fields.length <= 1}
                      onClick={() => remove(index)}
                    >
                      {t('accounting.journalEntries.removeLine')}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div
            className={`flex items-center justify-between rounded-md border p-3 text-sm ${
              isBalanced
                ? 'border-green-200 bg-green-50 text-green-700'
                : 'border-red-200 bg-red-50 text-red-700'
            }`}
          >
            <span>
              {t('accounting.journalEntries.totalDebit')}: {totalDebit.toFixed(2)}
            </span>
            <span>
              {t('accounting.journalEntries.totalCredit')}: {totalCredit.toFixed(2)}
            </span>
            <span>
              {isBalanced
                ? t('accounting.journalEntries.balanced')
                : t('accounting.journalEntries.notBalanced')}
            </span>
          </div>

          {mutation.isError && (
            <p className="text-xs text-red-600">{t('accounting.journalEntries.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending || !isBalanced} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('accounting.journalEntries.post')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function JournalEntryViewModal({
  entry,
  accounts,
  onClose,
}: {
  entry: JournalEntry
  accounts: AccountRef[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const accountLabel = (accountId: string) => {
    const a = accounts.find((x) => x.id === accountId)
    return a ? `${a.accountNumber} — ${a.name}` : accountId
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-2xl p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('accounting.journalEntries.viewTitle')}: {entry.entryNumber}
        </h3>
        <p className="mb-4 text-sm text-gray-600">{entry.description}</p>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-3 py-2 text-left text-xs font-semibold uppercase text-gray-500">
                  {t('accounting.journalEntries.account')}
                </th>
                <th className="px-3 py-2 text-left text-xs font-semibold uppercase text-gray-500">
                  {t('accounting.journalEntries.lineDescription')}
                </th>
                <th className="px-3 py-2 text-right text-xs font-semibold uppercase text-gray-500">
                  {t('accounting.journalEntries.debit')}
                </th>
                <th className="px-3 py-2 text-right text-xs font-semibold uppercase text-gray-500">
                  {t('accounting.journalEntries.credit')}
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {entry.lines.map((l, i) => (
                <tr key={i}>
                  <td className="px-3 py-2 text-xs">{accountLabel(l.accountId)}</td>
                  <td className="px-3 py-2">{l.description}</td>
                  <td className="px-3 py-2 text-right">{l.debit.toFixed(2)}</td>
                  <td className="px-3 py-2 text-right">{l.credit.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="font-semibold">
                <td className="px-3 py-2" colSpan={2}>
                  {t('accounting.journalEntries.total')}
                </td>
                <td className="px-3 py-2 text-right">{entry.totalDebit.toFixed(2)}</td>
                <td className="px-3 py-2 text-right">{entry.totalCredit.toFixed(2)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
        <div className="flex justify-end pt-4">
          <button className="btn-outline" onClick={onClose}>
            {t('common.close')}
          </button>
        </div>
      </div>
    </div>
  )
}

export function JournalEntriesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [pageNumber, setPageNumber] = useState(1)
  const [fiscalPeriodId, setFiscalPeriodId] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [viewEntry, setViewEntry] = useState<JournalEntry | null>(null)
  const [reverseTarget, setReverseTarget] = useState<JournalEntry | null>(null)
  const [voidTarget, setVoidTarget] = useState<JournalEntry | null>(null)
  const queryClient = useQueryClient()

  const canView = hasPermission('accounting:journals:view')
  const canManage = hasPermission('accounting:journals:manage')

  // Read-only cross-reference fetches (see AccountRef/FiscalPeriodRef above) —
  // do not duplicate AccountsPage/FiscalPeriodsPage, just source dropdown data.
  const { data: fiscalPeriods } = useQuery({
    queryKey: ['accounting', 'fiscal-periods', 'ref'],
    queryFn: () => apiClient.get<FiscalPeriodRef[]>(`${ACC_BASE}/fiscal-periods`).then((r) => r.data),
    staleTime: 60_000,
    enabled: canView,
  })

  const { data: accounts } = useQuery({
    queryKey: ['accounting', 'accounts', 'ref'],
    queryFn: () => apiClient.get<AccountRef[]>(`${ACC_BASE}/accounts`).then((r) => r.data),
    staleTime: 60_000,
    enabled: canView,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'journal-entries', { pageNumber, fiscalPeriodId }],
    queryFn: () =>
      journalEntriesApi.list({
        pageNumber,
        pageSize: PAGE_SIZE,
        fiscalPeriodId: fiscalPeriodId || undefined,
      }),
    staleTime: 15_000,
    enabled: canView,
  })

  const entries = data ?? []

  const reverseMutation = useMutation({
    mutationFn: ({ entry, reason, date }: { entry: JournalEntry; reason: string; date: string }) =>
      journalEntriesApi.reverse(entry.id, reason, date),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setReverseTarget(null)
    },
  })

  const voidMutation = useMutation({
    mutationFn: ({ entry, reason }: { entry: JournalEntry; reason: string }) => journalEntriesApi.void(entry.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setVoidTarget(null)
    },
  })

  // The API explains refusals (a closed period, an entry that belongs to a document, a closing entry ...).
  const detail = (e: unknown) =>
    (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? t('accounting.journalEntries.actionFailed')

  const columns = useMemo(
    () => [
      { key: 'entryNumber', label: t('accounting.journalEntries.entryNumber'), className: 'font-mono text-xs' },
      { key: 'description', label: t('accounting.journalEntries.description') },
      {
        key: 'totalDebit',
        label: t('accounting.journalEntries.totalDebit'),
        render: (e: JournalEntry) => e.totalDebit.toFixed(2),
      },
      {
        key: 'totalCredit',
        label: t('accounting.journalEntries.totalCredit'),
        render: (e: JournalEntry) => e.totalCredit.toFixed(2),
      },
      {
        key: 'createdAt',
        label: t('accounting.journalEntries.createdAt'),
        render: (e: JournalEntry) => new Date(e.createdAt).toLocaleString(),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (e: JournalEntry) => (e.status ? <StatusBadge status={e.status} /> : null),
      },
      {
        key: 'actions',
        label: '',
        render: (e: JournalEntry) => (
          <span className="flex gap-3">
            <button className="text-xs text-primary-600 hover:underline" onClick={() => setViewEntry(e)}>
              {t('accounting.journalEntries.view')}
            </button>
            {canManage && e.status === 'Posted' && e.referenceType !== 'PeriodClosing' && (
              <button className="text-xs text-gray-600 hover:underline" onClick={() => setReverseTarget(e)}>
                {t('accounting.journalEntries.reverse')}
              </button>
            )}
            {canManage && e.status === 'Posted' && !e.referenceType && (
              <button className="text-xs text-red-600 hover:underline" onClick={() => setVoidTarget(e)}>
                {t('accounting.journalEntries.void')}
              </button>
            )}
          </span>
        ),
      },
    ],
    [canManage, t],
  )

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.journalEntries.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setCreateOpen(true)}>
            {t('accounting.journalEntries.add')}
          </button>
        )}
      </div>

      <div className="flex items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">
            {t('accounting.journalEntries.fiscalPeriod')}
          </label>
          <select
            className="input max-w-xs"
            value={fiscalPeriodId}
            onChange={(e) => {
              setFiscalPeriodId(e.target.value)
              setPageNumber(1)
            }}
          >
            <option value="">{t('common.all')}</option>
            {(fiscalPeriods ?? []).map((fp) => (
              <option key={fp.id} value={fp.id}>
                {fp.periodName}
              </option>
            ))}
          </select>
        </div>
      </div>

      <DataTable columns={columns} data={entries} isLoading={isLoading} />

      <div className="flex justify-end gap-2">
        <button
          className="btn-outline"
          disabled={pageNumber === 1}
          onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
        >
          {t('common.prev')}
        </button>
        <button
          className="btn-outline"
          disabled={entries.length < PAGE_SIZE}
          onClick={() => setPageNumber((p) => p + 1)}
        >
          {t('common.next')}
        </button>
      </div>

      {createOpen && (
        <JournalEntryCreateModal
          accounts={accounts ?? []}
          fiscalPeriods={fiscalPeriods ?? []}
          onClose={() => setCreateOpen(false)}
        />
      )}

      {viewEntry && (
        <JournalEntryViewModal entry={viewEntry} accounts={accounts ?? []} onClose={() => setViewEntry(null)} />
      )}

      <ReasonDialog
        open={!!reverseTarget}
        title={t('accounting.journalEntries.reverseTitle', { entryNumber: reverseTarget?.entryNumber })}
        message={t('accounting.journalEntries.reverseHint')}
        confirmLabel={t('accounting.journalEntries.reverse')}
        withDate
        dateLabel={t('accounting.journalEntries.reversalDate')}
        busy={reverseMutation.isPending}
        error={reverseMutation.isError ? detail(reverseMutation.error) : null}
        onConfirm={(reason, date) => reverseTarget && reverseMutation.mutate({ entry: reverseTarget, reason, date })}
        onCancel={() => {
          setReverseTarget(null)
          reverseMutation.reset()
        }}
      />

      <ReasonDialog
        open={!!voidTarget}
        title={t('accounting.journalEntries.voidTitle', { entryNumber: voidTarget?.entryNumber })}
        message={t('accounting.journalEntries.voidHint')}
        confirmLabel={t('accounting.journalEntries.void')}
        danger
        busy={voidMutation.isPending}
        error={voidMutation.isError ? detail(voidMutation.error) : null}
        onConfirm={(reason) => voidTarget && voidMutation.mutate({ entry: voidTarget, reason })}
        onCancel={() => {
          setVoidTarget(null)
          voidMutation.reset()
        }}
      />
    </div>
  )
}
