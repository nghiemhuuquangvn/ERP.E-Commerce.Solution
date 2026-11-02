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
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()

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

  // computed on every render: watch() can hand back the same array object after an edit, which a useMemo would keep serving
  const totalDebit = (lines ?? []).reduce((sum, l) => sum + (parseFloat(l.debit) || 0), 0)
  const totalCredit = (lines ?? []).reduce((sum, l) => sum + (parseFloat(l.credit) || 0), 0)

  const isBalanced = totalDebit === totalCredit && totalDebit > 0
  const lineAccountMissing = Array.isArray(errors.lines) && errors.lines.some((l) => l?.accountId)

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
      toast.success(t('accounting.journalEntries.postedToast'))
      onClose()
    },
  })

  const [blocked, setBlocked] = useState(false)
  const onSubmit = (values: JournalEntryFormValues) => {
    setBlocked(!isBalanced)
    if (!isBalanced) return
    mutation.mutate(values)
  }

  return (
    <Modal
      open
      size="lg"
      title={t('accounting.journalEntries.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="create-journal-form" loading={mutation.isPending}>
            {t('accounting.journalEntries.post')}
          </Button>
        </>
      }
    >
      <form id="create-journal-form" noValidate onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <p className="text-xs text-ink-secondary">{t('accounting.journalEntries.postsNow')}</p>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <FormField label={t('accounting.journalEntries.entryNumber')} required error={errors.entryNumber ? t('common.required') : undefined}>
            <Input maxLength={50} {...register('entryNumber', { required: true })} />
          </FormField>
          <FormField label={t('accounting.journalEntries.fiscalPeriod')} required error={errors.fiscalPeriodId ? t('common.required') : undefined}>
            <Select {...register('fiscalPeriodId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {fiscalPeriods.map((fp) => (
                <option key={fp.id} value={fp.id}>
                  {fp.periodName}
                </option>
              ))}
            </Select>
          </FormField>
        </div>
        <FormField label={t('accounting.journalEntries.description')} required error={errors.description ? t('common.required') : undefined}>
          <Textarea rows={2} maxLength={500} {...register('description', { required: true })} />
        </FormField>

        <fieldset className="space-y-2">
          <div className="flex items-center justify-between">
            <legend className="text-sm font-medium text-ink-secondary">{t('accounting.journalEntries.lines')}</legend>
            <Button variant="outline" size="sm" onClick={() => append(emptyLine)}>
              {t('accounting.journalEntries.addLine')}
            </Button>
          </div>
          <div className="space-y-2">
            {fields.map((field, index) => {
              const n = index + 1
              return (
                <div key={field.id} className="grid grid-cols-12 items-start gap-2">
                  <div className="col-span-12 sm:col-span-4">
                    <Select
                      aria-label={t('accounting.journalEntries.lineAria', { n, field: t('accounting.journalEntries.account') })}
                      error={!!errors.lines?.[index]?.accountId}
                      aria-describedby={errors.lines?.[index]?.accountId ? 'journal-line-error' : undefined}
                      {...register(`lines.${index}.accountId` as const, { required: true })}
                    >
                      <option value="">{t('accounting.journalEntries.account')}</option>
                      {accounts.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.accountNumber} — {a.name}
                        </option>
                      ))}
                    </Select>
                  </div>
                  <div className="col-span-6 sm:col-span-2">
                    <Input
                      type="number"
                      step="0.01"
                      aria-label={t('accounting.journalEntries.lineAria', { n, field: t('accounting.journalEntries.debit') })}
                      placeholder={t('accounting.journalEntries.debit')}
                      {...register(`lines.${index}.debit` as const)}
                    />
                  </div>
                  <div className="col-span-6 sm:col-span-2">
                    <Input
                      type="number"
                      step="0.01"
                      aria-label={t('accounting.journalEntries.lineAria', { n, field: t('accounting.journalEntries.credit') })}
                      placeholder={t('accounting.journalEntries.credit')}
                      {...register(`lines.${index}.credit` as const)}
                    />
                  </div>
                  <div className="col-span-9 sm:col-span-3">
                    <Input
                      aria-label={t('accounting.journalEntries.lineAria', { n, field: t('accounting.journalEntries.lineDescription') })}
                      placeholder={t('accounting.journalEntries.lineDescription')}
                      {...register(`lines.${index}.description` as const)}
                    />
                  </div>
                  <div className="col-span-3 sm:col-span-1">
                    <Button
                      variant="link-danger"
                      className="text-xs"
                      disabled={fields.length <= 1}
                      aria-label={t('accounting.journalEntries.removeLineN', { n })}
                      onClick={() => remove(index)}
                    >
                      {t('accounting.journalEntries.removeLine')}
                    </Button>
                  </div>
                </div>
              )
            })}
          </div>
          {lineAccountMissing && (
            <p id="journal-line-error" role="alert" className="text-xs text-danger-soft-text">
              {t('accounting.journalEntries.lineAccountRequired')}
            </p>
          )}
        </fieldset>

        <div
          role="status"
          className={`flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm ${
            isBalanced ? 'border-success/30 bg-success-soft text-success-soft-text' : 'border-danger/30 bg-danger-soft text-danger-soft-text'
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
              : `${t('accounting.journalEntries.notBalanced')} (${t('accounting.journalEntries.difference', { diff: Math.abs(totalDebit - totalCredit).toFixed(2) })})`}
          </span>
        </div>

        {blocked && !isBalanced && <Alert variant="danger">{t('accounting.journalEntries.notBalanced')}</Alert>}
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.journalEntries.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
    <Modal
      open
      size="lg"
      title={`${t('accounting.journalEntries.viewTitle')}: ${entry.entryNumber}`}
      onClose={onClose}
      footer={
        <Button variant="outline" onClick={onClose}>
          {t('common.close')}
        </Button>
      }
    >
      <p className="mb-4 text-sm text-ink-secondary">{entry.description}</p>
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-line text-sm">
          <thead className="bg-surface-muted">
            <tr>
              <th scope="col" className="px-3 py-2 text-left text-xs font-semibold uppercase text-ink-secondary">
                {t('accounting.journalEntries.account')}
              </th>
              <th scope="col" className="px-3 py-2 text-left text-xs font-semibold uppercase text-ink-secondary">
                {t('accounting.journalEntries.lineDescription')}
              </th>
              <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase text-ink-secondary">
                {t('accounting.journalEntries.debit')}
              </th>
              <th scope="col" className="px-3 py-2 text-right text-xs font-semibold uppercase text-ink-secondary">
                {t('accounting.journalEntries.credit')}
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
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
    </Modal>
  )
}

export function JournalEntriesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const toast = useToast()
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
      toast.success(t('accounting.journalEntries.reversedToast'))
    },
  })

  const voidMutation = useMutation({
    mutationFn: ({ entry, reason }: { entry: JournalEntry; reason: string }) => journalEntriesApi.void(entry.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setVoidTarget(null)
      toast.success(t('accounting.journalEntries.voidedToast'))
    },
  })

  // The API explains refusals (a closed period, an entry that belongs to a document, a closing entry ...).
  const detail = (e: unknown) => serverMessage(e, t('accounting.journalEntries.actionFailed'))

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
            <Button variant="link" className="text-xs" aria-label={`${t('accounting.journalEntries.view')} ${e.entryNumber}`} onClick={() => setViewEntry(e)}>
              {t('accounting.journalEntries.view')}
            </Button>
            {canManage && e.status === 'Posted' && e.referenceType !== 'PeriodClosing' && (
              <Button variant="link" className="text-xs" aria-label={`${t('accounting.journalEntries.reverse')} ${e.entryNumber}`} onClick={() => setReverseTarget(e)}>
                {t('accounting.journalEntries.reverse')}
              </Button>
            )}
            {canManage && e.status === 'Posted' && !e.referenceType && (
              <Button variant="link-danger" className="text-xs" aria-label={`${t('accounting.journalEntries.void')} ${e.entryNumber}`} onClick={() => setVoidTarget(e)}>
                {t('accounting.journalEntries.void')}
              </Button>
            )}
          </span>
        ),
      },
    ],
    [canManage, t],
  )

  if (!canView) {
    return <EmptyState variant="forbidden" title={t('common.noPermission')} />
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('accounting.journalEntries.title')}</h1>
        {canManage && <Button onClick={() => setCreateOpen(true)}>{t('accounting.journalEntries.add')}</Button>}
      </div>

      <div className="flex items-end gap-3">
        <FormField label={t('accounting.journalEntries.fiscalPeriod')} className="max-w-xs">
          <Select
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
          </Select>
        </FormField>
      </div>

      <DataTable
        columns={columns}
        data={entries}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('accounting.journalEntries.empty')} action={canManage ? <Button onClick={() => setCreateOpen(true)}>{t('accounting.journalEntries.add')}</Button> : undefined} />}
      />

      <div className="flex justify-end gap-2">
        <Button variant="outline" disabled={pageNumber === 1} onClick={() => setPageNumber((p) => Math.max(1, p - 1))}>
          {t('common.prev')}
        </Button>
        <Button variant="outline" disabled={entries.length < PAGE_SIZE} onClick={() => setPageNumber((p) => p + 1)}>
          {t('common.next')}
        </Button>
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
