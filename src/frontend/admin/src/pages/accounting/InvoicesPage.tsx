import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { invoicesApi, INVOICE_STATUS } from '@/api/accounting/invoices.api'
import type { InvoiceDto } from '@/api/accounting/invoices.api'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { ForeignCurrencyFields, emptyFx, isForeign, toFxRequest } from '@/components/accounting/ForeignCurrencyFields'
import type { FxFields } from '@/components/accounting/ForeignCurrencyFields'
import { PostingGapsBar } from '@/components/accounting/PostingGapsBar'
import { VatLinesEditor, emptyVatLine, toVatLineInputs } from '@/components/accounting/VatLinesEditor'
import type { VatLineRow } from '@/components/accounting/VatLinesEditor'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const PAGE_SIZE = 20

// Invoice status is returned as a string ("Unpaid"/"Paid"/"Overdue") but the
// list filter's `status` query param is the underlying int enum value —
// InvoiceStatus: Unpaid=0, Paid=1, Overdue=2 (ERP.ACC.Domain.Enums).
function invoiceStatusColorKey(status: string): string {
  switch (status) {
    case 'Paid':
      return 'Completed'
    case 'Overdue':
      return 'Cancelled'
    default:
      return 'Pending'
  }
}

interface CreateInvoiceFormValues {
  invoiceNumber: string
  customerId: string
  orderId: string
  amount: number
  dueDate: string
}

function CreateInvoiceModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<CreateInvoiceFormValues>({
    defaultValues: { invoiceNumber: '', customerId: '', orderId: '', amount: 0, dueDate: '' },
  })
  const [useLines, setUseLines] = useState(false)
  const [lines, setLines] = useState<VatLineRow[]>([emptyVatLine()])
  const [fx, setFx] = useState<FxFields>(emptyFx())

  const mutation = useMutation({
    mutationFn: (values: CreateInvoiceFormValues) =>
      invoicesApi.create({
        ...toFxRequest(fx),
        invoiceNumber: values.invoiceNumber,
        customerId: values.customerId,
        orderId: values.orderId,
        amount: useLines || isForeign(fx) ? 0 : Number(values.amount),
        dueDate: values.dueDate,
        lines: useLines ? toVatLineInputs(lines) : undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('accounting.invoices.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.invoices.invoiceNumber')}
            </label>
            <input className="input" maxLength={50} {...register('invoiceNumber', { required: true })} />
            {errors.invoiceNumber && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.invoices.invoiceNumber')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.invoices.customerId')}
            </label>
            <input className="input" {...register('customerId', { required: true })} />
            {errors.customerId && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.invoices.customerId')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.invoices.orderId')}
            </label>
            <input className="input" {...register('orderId', { required: true })} />
            {errors.orderId && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.invoices.orderId')} is required.</p>
            )}
          </div>
          <ForeignCurrencyFields value={fx} onChange={setFx} />
          {!isForeign(fx) && (
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input type="checkbox" checked={useLines} onChange={(e) => setUseLines(e.target.checked)} />
              {t('accounting.vat.useLines')}
            </label>
          )}
          {isForeign(fx) ? null : useLines ? (
            <VatLinesEditor rows={lines} onChange={setLines} />
          ) : (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('accounting.invoices.amount')}
              </label>
              <input
                type="number"
                step="0.01"
                min="0.01"
                className="input"
                {...register('amount', { required: !useLines, valueAsNumber: true, min: useLines ? undefined : 0.01 })}
              />
              {errors.amount && (
                <p className="mt-1 text-xs text-red-600">{t('accounting.invoices.amount')} must be greater than 0.</p>
              )}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.invoices.dueDate')}
            </label>
            <input type="date" className="input" {...register('dueDate', { required: true })} />
            {errors.dueDate && (
              <p className="mt-1 text-xs text-red-600">{t('accounting.invoices.dueDate')} must be in the future.</p>
            )}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('accounting.invoices.failedToCreate')}</p>}
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

function InvoicesListTab({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [pageNumber, setPageNumber] = useState(1)
  const [customerId, setCustomerId] = useState('')
  const [status, setStatus] = useState('')
  const [createOpen, setCreateOpen] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'invoices', { customerId, status, pageNumber }],
    queryFn: () =>
      invoicesApi.list({
        customerId: customerId.trim() || undefined,
        status: status !== '' ? Number(status) : undefined,
        pageNumber,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 15_000,
  })

  const markPaidMutation = useMutation({
    mutationFn: ({ id, paymentRate }: { id: string; paymentRate?: number }) => invoicesApi.markPaid(id, paymentRate),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting', 'invoices'] }),
  })

  const items = data ?? []
  const [eInvoiceOf, setEInvoiceOf] = useState<InvoiceDto | null>(null)
  const [adjustVatOf, setAdjustVatOf] = useState<InvoiceDto | null>(null)

  const columns = useMemo(
    () => [
      {
        key: 'invoiceNumber',
        label: t('accounting.invoices.invoiceNumber'),
        className: 'font-mono text-xs',
        render: (i: InvoiceDto) => (
          <span>
            {i.invoiceNumber}
            {i.postedToLedger === false && (
              <span className="ml-1 text-amber-600" title={i.postingWarning ?? ''}>
                ⚠
              </span>
            )}
          </span>
        ),
      },
      { key: 'customerId', label: t('accounting.invoices.customerId'), className: 'font-mono text-xs' },
      { key: 'orderId', label: t('accounting.invoices.orderId'), className: 'font-mono text-xs' },
      {
        key: 'netAmount',
        label: t('accounting.vat.net'),
        render: (i: InvoiceDto) => i.netAmount.toLocaleString(),
      },
      {
        key: 'shippingNetAmount',
        label: t('accounting.invoices.delivery'),
        render: (i: InvoiceDto) => (i.shippingNetAmount ? i.shippingNetAmount.toLocaleString() : '—'),
      },
      {
        key: 'taxAmount',
        label: t('accounting.vat.taxWithRate'),
        render: (i: InvoiceDto) => (i.taxAmount ? `${i.taxAmount.toLocaleString()} (${i.taxRatePercent}%)` : '—'),
      },
      {
        key: 'amount',
        label: t('accounting.invoices.amount'),
        render: (i: InvoiceDto) => (
          <span>
            {i.amount.toLocaleString()}
            {i.foreignCurrencyCode && (
              <span className="block text-xs text-gray-500">
                {i.foreignAmount?.toLocaleString()} {i.foreignCurrencyCode} @ {i.exchangeRate?.toLocaleString()}
              </span>
            )}
          </span>
        ),
      },
      {
        key: 'dueDate',
        label: t('accounting.invoices.dueDate'),
        render: (i: InvoiceDto) => new Date(i.dueDate).toLocaleDateString(),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (i: InvoiceDto) => (
          <StatusBadge
            status={invoiceStatusColorKey(i.status)}
            label={t(`accounting.invoices.status${i.status}`, i.status)}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (i: InvoiceDto) => (
          <span className="flex flex-wrap gap-3">
            {canManage && i.status !== 'Paid' && (
              <button
                className="text-xs text-primary-600 hover:underline disabled:opacity-40"
                disabled={markPaidMutation.isPending}
                onClick={() => {
                  if (!i.foreignCurrencyCode) return markPaidMutation.mutate({ id: i.id })
                  const rate = Number(window.prompt(t('accounting.fx.paymentRatePrompt', { currency: i.foreignCurrencyCode }), String(i.exchangeRate ?? '')))
                  if (rate > 0) markPaidMutation.mutate({ id: i.id, paymentRate: rate })
                }}
              >
                {t('accounting.invoices.markPaid')}
              </button>
            )}
            {canManage && i.status !== 'Cancelled' && (
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setAdjustVatOf(i)}>
                {t('accounting.vat.adjust')}
              </button>
            )}
            <button className="text-xs text-primary-600 hover:underline" onClick={() => setEInvoiceOf(i)}>
              {t('accounting.vat.eInvoice')}
            </button>
          </span>
        ),
      },
    ],
    [canManage, markPaidMutation, t],
  )

  return (
    <div className="space-y-4">
      <PostingGapsBar canManage={canManage} />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('accounting.invoices.customerId')}
            </label>
            <input
              className="input"
              value={customerId}
              onChange={(e) => {
                setCustomerId(e.target.value)
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
              <option value={String(INVOICE_STATUS.Unpaid)}>{t('accounting.invoices.statusUnpaid')}</option>
              <option value={String(INVOICE_STATUS.Paid)}>{t('accounting.invoices.statusPaid')}</option>
              <option value={String(INVOICE_STATUS.Overdue)}>{t('accounting.invoices.statusOverdue')}</option>
            </select>
          </div>
        </div>
        {canManage && (
          <button className="btn-primary" onClick={() => setCreateOpen(true)}>
            {t('accounting.invoices.add')}
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

      {createOpen && <CreateInvoiceModal onClose={() => setCreateOpen(false)} />}
      {eInvoiceOf && <EInvoiceDialog invoice={eInvoiceOf} canManage={canManage} onClose={() => setEInvoiceOf(null)} />}
      {adjustVatOf && <AdjustVatDialog invoice={adjustVatOf} onClose={() => setAdjustVatOf(null)} />}
    </div>
  )
}

/** Re-splits the invoice at one rate or at several VAT lines whose amounts add up to the invoice amount. */
function AdjustVatDialog({ invoice, onClose }: { invoice: InvoiceDto; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [rate, setRate] = useState(String(invoice.taxRatePercent))
  const [useLines, setUseLines] = useState(false)
  const [lines, setLines] = useState<VatLineRow[]>([{ ...emptyVatLine(), amount: String(invoice.amount) }])
  const total = lines.reduce((s, r) => s + (Number(r.amount) || 0), 0)
  const mutation = useMutation({
    mutationFn: () => invoicesApi.adjustVat(invoice.id, Number(rate) || 0, useLines ? toVatLineInputs(lines) : undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      onClose()
    },
  })
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="card w-full max-w-lg space-y-3 p-4">
        <h2 className="text-lg font-semibold">
          {t('accounting.vat.adjust')} · {invoice.invoiceNumber}
        </h2>
        <p className="text-sm text-gray-600">{t('accounting.vat.adjustHint', { amount: invoice.amount.toLocaleString() })}</p>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" checked={useLines} onChange={(e) => setUseLines(e.target.checked)} />
          {t('accounting.vat.useLines')}
        </label>
        {useLines ? (
          <>
            <VatLinesEditor rows={lines} onChange={setLines} />
            <p className={Math.abs(total - invoice.amount) < 0.01 ? 'text-xs text-green-700' : 'text-xs text-amber-700'}>
              {t('accounting.vat.linesTotal', { total: total.toLocaleString(), amount: invoice.amount.toLocaleString() })}
            </p>
          </>
        ) : (
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.vat.adjustPrompt')}</label>
            <input type="number" min="0" max="100" step="0.01" className="input w-32" value={rate} onChange={(e) => setRate(e.target.value)} />
          </div>
        )}
        {mutation.isError && <p className="text-xs text-red-600">{detail(mutation.error, t('accounting.vat.failed'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>
            {t('common.cancel')}
          </button>
          <button className="btn-primary" disabled={mutation.isPending || (useLines && Math.abs(total - invoice.amount) >= 0.01)} onClick={() => mutation.mutate()}>
            {t('common.save')}
          </button>
        </div>
      </div>
    </div>
  )
}

const detail = (e: unknown, fallback: string) => (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? fallback

/** The e-invoice of one invoice: its state, issue with the buyer's details, cancel with a reason (ROADMAP P1-3). */
function EInvoiceDialog({ invoice, canManage, onClose }: { invoice: InvoiceDto; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [buyer, setBuyer] = useState({ buyerName: '', buyerTaxCode: '', buyerAddress: '' })
  const [cancelling, setCancelling] = useState(false)
  const { data: all, isLoading } = useQuery({ queryKey: ['accounting', 'invoices', 'e-invoices', invoice.id], queryFn: () => invoicesApi.eInvoices(invoice.id) })
  // the e-invoice in force (original or replacement), else the last one
  const chain = (all ?? []).filter((e) => e.kind !== 'Adjustment')
  const data = chain.find((e) => e.status === 'Issued' || e.status === 'Failed') ?? chain[chain.length - 1]
  const adjustments = (all ?? []).filter((e) => e.kind === 'Adjustment')
  const [replaceReason, setReplaceReason] = useState('')
  const [replaceRate, setReplaceRate] = useState('')
  const [adjust, setAdjust] = useState({ net: '', tax: '', reason: '' })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['accounting', 'invoices'] })
  const issue = useMutation({ mutationFn: () => invoicesApi.issueEInvoice(invoice.id, buyer), onSuccess: refresh })
  const cancel = useMutation({
    mutationFn: (reason: string) => invoicesApi.cancelEInvoice(invoice.id, reason),
    onSuccess: () => {
      refresh()
      setCancelling(false)
    },
  })
  const issued = data?.status === 'Issued'
  const replace = useMutation({
    mutationFn: () => invoicesApi.replaceEInvoice(invoice.id, { reason: replaceReason, taxRatePercent: replaceRate === '' ? undefined : Number(replaceRate) }),
    onSuccess: () => {
      refresh()
      setReplaceReason('')
      setReplaceRate('')
    },
  })
  const adjustMutation = useMutation({
    mutationFn: () => invoicesApi.adjustEInvoice(invoice.id, { adjustNet: Number(adjust.net) || 0, adjustTax: Number(adjust.tax) || 0, reason: adjust.reason }),
    onSuccess: () => {
      refresh()
      setAdjust({ net: '', tax: '', reason: '' })
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="card w-full max-w-md space-y-3 p-4">
        <h2 className="text-lg font-semibold">
          {t('accounting.vat.eInvoice')} · {invoice.invoiceNumber}
        </h2>
        {isLoading && <p className="text-sm text-gray-400">{t('common.loading')}</p>}
        {data && (
          <div className="space-y-1 text-sm text-gray-700">
            <p>
              {t('common.status')}: <b>{data.status}</b>
            </p>
            {data.number && (
              <p>
                {data.series}-{data.number} · {t('accounting.vat.lookupCode')}: <span className="font-mono">{data.lookupCode}</span>
              </p>
            )}
            <p>
              {t('accounting.vat.buyer')}: {data.buyerName}
              {data.buyerTaxCode ? ` (${data.buyerTaxCode})` : ''}
            </p>
            {data.error && <p className="text-red-600">{data.error}</p>}
            {data.cancelReason && <p className="text-gray-500">{data.cancelReason}</p>}
          </div>
        )}
        {canManage && !issued && !isLoading && (
          <div className="space-y-2">
            <p className="text-sm font-medium text-gray-700">{t('accounting.vat.buyer')}</p>
            {(['buyerName', 'buyerTaxCode', 'buyerAddress'] as const).map((k) => (
              <input key={k} className="input w-full" placeholder={t(`accounting.vat.${k}`)} value={buyer[k]} onChange={(e) => setBuyer({ ...buyer, [k]: e.target.value })} />
            ))}
            <button className="btn-primary" disabled={issue.isPending} onClick={() => issue.mutate()}>
              {t('accounting.vat.issue')}
            </button>
            {issue.isError && <p className="text-xs text-red-600">{detail(issue.error, t('accounting.vat.failed'))}</p>}
          </div>
        )}
        {canManage && issued && (
          <button className="text-sm text-red-600 hover:underline" onClick={() => setCancelling(true)}>
            {t('accounting.vat.cancelEInvoice')}
          </button>
        )}
        {canManage && issued && (
          <div className="space-y-2 border-t pt-2">
            <p className="text-sm font-medium text-gray-700">{t('accounting.vat.replace')}</p>
            <div className="flex flex-wrap gap-2">
              <input className="input w-48" placeholder={t('accounting.vat.reason')} value={replaceReason} onChange={(e) => setReplaceReason(e.target.value)} />
              <input type="number" min="0" max="100" step="0.01" className="input w-24" placeholder={t('accounting.vat.newRate')} value={replaceRate} onChange={(e) => setReplaceRate(e.target.value)} />
              <button className="btn-outline" disabled={!replaceReason.trim() || replace.isPending} onClick={() => replace.mutate()}>
                {t('accounting.vat.replaceButton')}
              </button>
            </div>
            {replace.isError && <p className="text-xs text-red-600">{detail(replace.error, t('accounting.vat.failed'))}</p>}
            <p className="text-sm font-medium text-gray-700">{t('accounting.vat.adjustEInvoice')}</p>
            <div className="flex flex-wrap gap-2">
              <input type="number" step="0.01" className="input w-28" placeholder={t('accounting.vat.adjustNet')} value={adjust.net} onChange={(e) => setAdjust({ ...adjust, net: e.target.value })} />
              <input type="number" step="0.01" className="input w-28" placeholder={t('accounting.vat.adjustTax')} value={adjust.tax} onChange={(e) => setAdjust({ ...adjust, tax: e.target.value })} />
              <input className="input w-48" placeholder={t('accounting.vat.reason')} value={adjust.reason} onChange={(e) => setAdjust({ ...adjust, reason: e.target.value })} />
              <button className="btn-outline" disabled={!adjust.reason.trim() || (!Number(adjust.net) && !Number(adjust.tax)) || adjustMutation.isPending} onClick={() => adjustMutation.mutate()}>
                {t('accounting.vat.adjustButton')}
              </button>
            </div>
            {adjustMutation.isError && <p className="text-xs text-red-600">{detail(adjustMutation.error, t('accounting.vat.failed'))}</p>}
          </div>
        )}
        {(all ?? []).length > 1 && (
          <ul className="space-y-1 border-t pt-2 text-xs text-gray-600">
            {(all ?? []).map((e) => (
              <li key={e.id}>
                {e.kind} · {e.series}-{e.number ?? '—'} · {e.status}
                {e.kind === 'Adjustment' ? ` · ${(e.adjustNet ?? 0).toLocaleString()} / ${(e.adjustTax ?? 0).toLocaleString()}` : ''}
                {e.reason ? ` · ${e.reason}` : ''}
              </li>
            ))}
          </ul>
        )}
        {adjustments.length > 0 && <p className="text-xs text-gray-500">{t('accounting.vat.adjustmentsCount', { count: adjustments.length })}</p>}
        <div className="flex justify-end">
          <button className="btn-outline" onClick={onClose}>
            {t('common.close')}
          </button>
        </div>
      </div>
      <ReasonDialog
        open={cancelling}
        title={t('accounting.vat.cancelEInvoice')}
        message={invoice.invoiceNumber}
        confirmLabel={t('accounting.vat.cancelEInvoice')}
        danger
        busy={cancel.isPending}
        error={cancel.isError ? detail(cancel.error, t('accounting.vat.failed')) : null}
        onConfirm={(reason) => cancel.mutate(reason)}
        onCancel={() => setCancelling(false)}
      />
    </div>
  )
}

function ARAgingTab() {
  const { t } = useTranslation()
  const [asOfDate, setAsOfDate] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'invoices', 'ar-aging', asOfDate],
    queryFn: () => invoicesApi.arAging(asOfDate || undefined),
    staleTime: 15_000,
  })

  const columns = useMemo(
    () => [
      { key: 'customerId', label: t('accounting.invoices.customerId'), className: 'font-mono text-xs' },
      { key: 'current', label: t('accounting.invoices.current'), render: (a: { current: number }) => a.current.toLocaleString() },
      { key: 'days30', label: t('accounting.invoices.days30'), render: (a: { days30: number }) => a.days30.toLocaleString() },
      { key: 'days60', label: t('accounting.invoices.days60'), render: (a: { days60: number }) => a.days60.toLocaleString() },
      {
        key: 'days90Plus',
        label: t('accounting.invoices.days90Plus'),
        render: (a: { days90Plus: number }) => a.days90Plus.toLocaleString(),
      },
      {
        key: 'totalOutstanding',
        label: t('accounting.invoices.totalOutstanding'),
        render: (a: { totalOutstanding: number }) => a.totalOutstanding.toLocaleString(),
      },
    ],
    [t],
  )

  const rows = (data ?? []).map((a) => ({ id: a.customerId, ...a }))

  return (
    <div className="space-y-4">
      <div>
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.invoices.asOfDate')}</label>
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

export function InvoicesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canView = hasPermission('accounting:invoices:view')
  const canManage = hasPermission('accounting:invoices:manage')
  const [tab, setTab] = useState<'invoices' | 'aging'>('invoices')

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.invoices.title')}</h1>

      <div className="flex gap-2 border-b border-gray-200">
        <button
          type="button"
          onClick={() => setTab('invoices')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'invoices' ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          {t('accounting.invoices.tabInvoices')}
        </button>
        <button
          type="button"
          onClick={() => setTab('aging')}
          className={`px-3 py-2 text-sm font-medium ${
            tab === 'aging' ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          {t('accounting.invoices.tabArAging')}
        </button>
      </div>

      {tab === 'invoices' && <InvoicesListTab canManage={canManage} />}
      {tab === 'aging' && <ARAgingTab />}
    </div>
  )
}
