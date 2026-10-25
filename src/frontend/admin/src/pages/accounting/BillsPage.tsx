import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { billsApi, BILL_STATUS } from '@/api/accounting/bills.api'
import type { BillDto } from '@/api/accounting/bills.api'
import { PostingGapsBar } from '@/components/accounting/PostingGapsBar'
import { VatLinesEditor, emptyVatLine, toVatLineInputs } from '@/components/accounting/VatLinesEditor'
import type { VatLineRow } from '@/components/accounting/VatLinesEditor'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const PAGE_SIZE = 20

// Bill status is returned as a string ("Pending"/"Approved"/"Paid") but the
// list filter's `status` query param is the underlying int enum value —
// BillStatus: Pending=0, Approved=1, Paid=2 (ERP.ACC.Domain.Enums).
function billStatusColorKey(status: string): string {
  switch (status) {
    case 'Approved':
      return 'Processing'
    case 'Paid':
      return 'Completed'
    default:
      return 'Pending'
  }
}

interface CreateBillFormValues {
  billNumber: string
  supplierId: string
  poId: string
  amount: number
  dueDate: string
}

function CreateBillModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<CreateBillFormValues>({
    defaultValues: { billNumber: '', supplierId: '', poId: '', amount: 0, dueDate: '' },
  })
  const [useLines, setUseLines] = useState(false)
  const [lines, setLines] = useState<VatLineRow[]>([emptyVatLine()])

  const mutation = useMutation({
    mutationFn: (values: CreateBillFormValues) =>
      billsApi.create({
        billNumber: values.billNumber,
        supplierId: values.supplierId,
        poId: values.poId,
        amount: useLines ? 0 : Number(values.amount),
        dueDate: values.dueDate,
        lines: useLines ? toVatLineInputs(lines) : undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'bills'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('accounting.bills.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.bills.billNumber')}
            </label>
            <input className="input" maxLength={50} {...register('billNumber', { required: true })} />
            {errors.billNumber && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.bills.billNumber')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.bills.supplierId')}
            </label>
            <input className="input" {...register('supplierId', { required: true })} />
            {errors.supplierId && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.bills.supplierId')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.bills.poId')}</label>
            <input className="input" {...register('poId', { required: true })} />
            {errors.poId && <p className="mt-1 text-xs text-red-600">{t('accounting.bills.poId')} is required.</p>}
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={useLines} onChange={(e) => setUseLines(e.target.checked)} />
            {t('accounting.vat.useLines')}
          </label>
          {useLines ? (
            <VatLinesEditor rows={lines} onChange={setLines} />
          ) : (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.bills.amount')}</label>
              <input
                type="number"
                step="0.01"
                min="0.01"
                className="input"
                {...register('amount', { required: !useLines, valueAsNumber: true, min: useLines ? undefined : 0.01 })}
              />
              {errors.amount && (
                <p className="mt-1 text-xs text-red-600">{t('accounting.bills.amount')} must be greater than 0.</p>
              )}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.bills.dueDate')}</label>
            <input type="date" className="input" {...register('dueDate', { required: true })} />
            {errors.dueDate && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.bills.dueDate')} must be in the future.</p>
            )}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('accounting.bills.failedToCreate')}</p>}
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

function BillsListTab({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [pageNumber, setPageNumber] = useState(1)
  const [supplierId, setSupplierId] = useState('')
  const [status, setStatus] = useState('')
  const [createOpen, setCreateOpen] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'bills', { supplierId, status, pageNumber }],
    queryFn: () =>
      billsApi.list({
        supplierId: supplierId.trim() || undefined,
        status: status !== '' ? Number(status) : undefined,
        pageNumber,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 15_000,
  })

  const approveMutation = useMutation({
    mutationFn: ({ id, rate }: { id: string; rate?: number }) => billsApi.approve(id, rate),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting', 'bills'] }),
  })

  const markPaidMutation = useMutation({
    mutationFn: (id: string) => billsApi.markPaid(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting', 'bills'] }),
  })

  const items = data ?? []

  const columns = useMemo(
    () => [
      {
        key: 'billNumber',
        label: t('accounting.bills.billNumber'),
        className: 'font-mono text-xs',
        render: (b: BillDto) => (
          <span>
            {b.billNumber}
            {b.postedToLedger === false && (
              <span className="ml-1 text-amber-600" title={b.postingWarning ?? ''}>
                ⚠
              </span>
            )}
          </span>
        ),
      },
      { key: 'supplierId', label: t('accounting.bills.supplierId'), className: 'font-mono text-xs' },
      { key: 'poId', label: t('accounting.bills.poId'), className: 'font-mono text-xs' },
      {
        key: 'netAmount',
        label: t('accounting.vat.net'),
        render: (b: BillDto) => b.netAmount.toLocaleString(),
      },
      {
        key: 'taxAmount',
        label: t('accounting.vat.taxWithRate'),
        render: (b: BillDto) => (b.taxAmount ? `${b.taxAmount.toLocaleString()} (${b.taxRatePercent}%)` : '—'),
      },
      {
        key: 'amount',
        label: t('accounting.bills.amount'),
        render: (b: BillDto) => b.amount.toLocaleString(),
      },
      {
        key: 'dueDate',
        label: t('accounting.bills.dueDate'),
        render: (b: BillDto) => new Date(b.dueDate).toLocaleDateString(),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (b: BillDto) => (
          <StatusBadge
            status={billStatusColorKey(b.status)}
            label={t(`accounting.bills.status${b.status}`, b.status)}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (b: BillDto) =>
          canManage ? (
            <div className="flex flex-wrap gap-3">
              {b.status === 'Pending' && (
                <button
                  className="text-xs text-primary-600 hover:underline disabled:opacity-40"
                  disabled={approveMutation.isPending}
                  onClick={() => {
                    // the supplier's invoice shows the real VAT rate: empty keeps the split made when the bill was created
                    const typed = window.prompt(t('accounting.vat.approvePrompt'), String(b.taxRatePercent))
                    if (typed === null) return
                    approveMutation.mutate({ id: b.id, rate: typed.trim() !== '' && !Number.isNaN(Number(typed)) && Number(typed) !== b.taxRatePercent ? Number(typed) : undefined })
                  }}
                >
                  {t('accounting.bills.approve')}
                </button>
              )}
              {b.status !== 'Paid' && (
                <button
                  className="text-xs text-primary-600 hover:underline disabled:opacity-40"
                  disabled={markPaidMutation.isPending}
                  onClick={() => markPaidMutation.mutate(b.id)}
                >
                  {t('accounting.bills.markPaid')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, approveMutation, markPaidMutation, t],
  )

  return (
    <div className="space-y-4">
      <PostingGapsBar canManage={canManage} />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.bills.supplierId')}
            </label>
            <input
              className="input"
              value={supplierId}
              onChange={(e) => {
                setSupplierId(e.target.value)
                setPageNumber(1)
              }}
              placeholder={t('common.search') ?? ''}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.status')}</label>
            <select
              className="input"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value)
                setPageNumber(1)
              }}
            >
              <option value="">{t('common.all')}</option>
              <option value={String(BILL_STATUS.Pending)}>{t('accounting.bills.statusPending')}</option>
              <option value={String(BILL_STATUS.Approved)}>{t('accounting.bills.statusApproved')}</option>
              <option value={String(BILL_STATUS.Paid)}>{t('accounting.bills.statusPaid')}</option>
            </select>
          </div>
        </div>
        {canManage && (
          <button className="btn-primary" onClick={() => setCreateOpen(true)}>
            {t('accounting.bills.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={items} isLoading={isLoading} />

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
          disabled={items.length < PAGE_SIZE}
          onClick={() => setPageNumber((p) => p + 1)}
        >
          {t('common.next')}
        </button>
      </div>

      {createOpen && <CreateBillModal onClose={() => setCreateOpen(false)} />}
    </div>
  )
}

function APAgingTab() {
  const { t } = useTranslation()
  const [asOfDate, setAsOfDate] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'bills', 'ap-aging', asOfDate],
    queryFn: () => billsApi.apAging(asOfDate || undefined),
    staleTime: 15_000,
  })

  const columns = useMemo(
    () => [
      { key: 'supplierId', label: t('accounting.bills.supplierId'), className: 'font-mono text-xs' },
      { key: 'current', label: t('accounting.bills.current'), render: (a: { current: number }) => a.current.toLocaleString() },
      { key: 'days30', label: t('accounting.bills.days30'), render: (a: { days30: number }) => a.days30.toLocaleString() },
      { key: 'days60', label: t('accounting.bills.days60'), render: (a: { days60: number }) => a.days60.toLocaleString() },
      {
        key: 'days90Plus',
        label: t('accounting.bills.days90Plus'),
        render: (a: { days90Plus: number }) => a.days90Plus.toLocaleString(),
      },
      {
        key: 'totalOutstanding',
        label: t('accounting.bills.totalOutstanding'),
        render: (a: { totalOutstanding: number }) => a.totalOutstanding.toLocaleString(),
      },
    ],
    [t],
  )

  const rows = (data ?? []).map((a) => ({ id: a.supplierId, ...a }))

  return (
    <div className="space-y-4">
      <div>
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.bills.asOfDate')}</label>
        <input
          type="date"
          className="input max-w-xs"
          value={asOfDate}
          onChange={(e) => setAsOfDate(e.target.value)}
        />
      </div>
      <DataTable columns={columns} data={rows} isLoading={isLoading} />
    </div>
  )
}

export function BillsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canView = hasPermission('accounting:bills:view')
  const canManage = hasPermission('accounting:bills:manage')
  const [tab, setTab] = useState<'bills' | 'aging'>('bills')

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.bills.title')}</h1>

      <div className="flex gap-2 border-b border-gray-200">
        <button
          type="button"
          onClick={() => setTab('bills')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'bills' ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          {t('accounting.bills.tabBills')}
        </button>
        <button
          type="button"
          onClick={() => setTab('aging')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'aging' ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          {t('accounting.bills.tabApAging')}
        </button>
      </div>

      {tab === 'bills' && <BillsListTab canManage={canManage} />}
      {tab === 'aging' && <APAgingTab />}
    </div>
  )
}
