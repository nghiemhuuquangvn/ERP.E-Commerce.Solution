import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import { invoicesApi, INVOICE_STATUS } from '@/api/accounting/invoices.api'
import type { InvoiceDto } from '@/api/accounting/invoices.api'
import { PageTabs } from '@/components/shared/PageTabs'
import { ForeignCurrencyFields, emptyFx, isForeign, toFxRequest } from '@/components/accounting/ForeignCurrencyFields'
import type { FxFields } from '@/components/accounting/ForeignCurrencyFields'
import { PayDialog } from '@/components/accounting/PayDialog'
import { PostingGapsBar } from '@/components/accounting/PostingGapsBar'
import { VatLinesEditor, emptyVatLine, toVatLineInputs } from '@/components/accounting/VatLinesEditor'
import type { VatLineRow } from '@/components/accounting/VatLinesEditor'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      title={t('accounting.invoices.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="create-invoice-form" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form id="create-invoice-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('accounting.invoices.invoiceNumber')} required error={errors.invoiceNumber ? t('common.required') : undefined}>
          <Input maxLength={50} {...register('invoiceNumber', { required: true })} />
        </FormField>
        <FormField label={t('accounting.invoices.customerId')} required error={errors.customerId ? t('common.required') : undefined}>
          <Input {...register('customerId', { required: true })} />
        </FormField>
        <FormField label={t('accounting.invoices.orderId')} required error={errors.orderId ? t('common.required') : undefined}>
          <Input {...register('orderId', { required: true })} />
        </FormField>
        <ForeignCurrencyFields value={fx} onChange={setFx} />
        {!isForeign(fx) && <Checkbox checked={useLines} onChange={(e) => setUseLines(e.target.checked)} label={t('accounting.vat.useLines')} />}
        {isForeign(fx) ? null : useLines ? (
          <VatLinesEditor rows={lines} onChange={setLines} />
        ) : (
          <FormField label={t('accounting.invoices.amount')} required error={errors.amount ? t('accounting.amountPositive') : undefined}>
            <Input type="number" step="0.01" min="0.01" {...register('amount', { required: !useLines, valueAsNumber: true, min: useLines ? undefined : 0.01 })} />
          </FormField>
        )}
        <FormField label={t('accounting.invoices.dueDate')} required error={errors.dueDate ? t('common.required') : undefined}>
          <Input type="date" {...register('dueDate', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.invoices.failedToCreate'))}</Alert>}
      </form>
    </Modal>
  )
}

function InvoicesListTab({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [pageNumber, setPageNumber] = useState(1)
  const [customerId, setCustomerId] = useState('')
  const [status, setStatus] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [payTarget, setPayTarget] = useState<InvoiceDto | null>(null)

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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setPayTarget(null)
      toast.success(t('common.saved'))
    },
    onError: (e) => {
      setPayTarget(null)
      toast.error(serverMessage(e, t('accounting.invoices.actionFailed')))
    },
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
              <span className="ml-1 text-warning" title={i.postingWarning ?? ''}>
                <AlertTriangle size={16} strokeWidth={1.75} aria-label={t('accounting.notPosted')} />
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
              <span className="block text-xs text-ink-secondary">
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
              <Button variant="link" className="text-xs" aria-label={`${t('accounting.invoices.markPaid')} ${i.invoiceNumber}`} onClick={() => setPayTarget(i)}>
                {t('accounting.invoices.markPaid')}
              </Button>
            )}
            {canManage && i.status !== 'Cancelled' && (
              <Button variant="link" className="text-xs" aria-label={`${t('accounting.vat.adjust')} ${i.invoiceNumber}`} onClick={() => setAdjustVatOf(i)}>
                {t('accounting.vat.adjust')}
              </Button>
            )}
            <Button variant="link" className="text-xs" aria-label={`${t('accounting.vat.eInvoice')} ${i.invoiceNumber}`} onClick={() => setEInvoiceOf(i)}>
              {t('accounting.vat.eInvoice')}
            </Button>
          </span>
        ),
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <PostingGapsBar canManage={canManage} />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <FormField label={t('accounting.invoices.customerId')}>
            <Input
              value={customerId}
              onChange={(e) => {
                setCustomerId(e.target.value)
                setPageNumber(1)
              }}
              placeholder={t('common.search')}
            />
          </FormField>
          <FormField label={t('common.status')}>
            <Select
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
            </Select>
          </FormField>
        </div>
        {canManage && <Button onClick={() => setCreateOpen(true)}>{t('accounting.invoices.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('accounting.invoices.empty')} action={canManage ? <Button onClick={() => setCreateOpen(true)}>{t('accounting.invoices.add')}</Button> : undefined} />}
      />

      <div className="flex justify-end gap-2">
        <Button variant="outline" disabled={pageNumber === 1} onClick={() => setPageNumber((p) => Math.max(1, p - 1))}>
          {t('common.prev')}
        </Button>
        <Button variant="outline" disabled={items.length < PAGE_SIZE} onClick={() => setPageNumber((p) => p + 1)}>
          {t('common.next')}
        </Button>
      </div>

      {createOpen && <CreateInvoiceModal onClose={() => setCreateOpen(false)} />}
      {eInvoiceOf && <EInvoiceDialog invoice={eInvoiceOf} canManage={canManage} onClose={() => setEInvoiceOf(null)} />}
      {adjustVatOf && <AdjustVatDialog invoice={adjustVatOf} onClose={() => setAdjustVatOf(null)} />}
      <PayDialog
        open={!!payTarget}
        title={t('accounting.invoices.payTitle', { number: payTarget?.invoiceNumber ?? '' })}
        message={t('accounting.invoices.payConfirm', { amount: payTarget?.amount.toLocaleString() ?? '' })}
        currency={payTarget?.foreignCurrencyCode}
        defaultRate={payTarget?.exchangeRate}
        busy={markPaidMutation.isPending}
        onConfirm={(paymentRate) => payTarget && !markPaidMutation.isPending && markPaidMutation.mutate({ id: payTarget.id, paymentRate })}
        onCancel={() => setPayTarget(null)}
      />
    </div>
  )
}

/** Re-splits the invoice at one rate or at several VAT lines whose amounts add up to the invoice amount. */
function AdjustVatDialog({ invoice, onClose }: { invoice: InvoiceDto; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [rate, setRate] = useState(String(invoice.taxRatePercent))
  const [useLines, setUseLines] = useState(false)
  const [lines, setLines] = useState<VatLineRow[]>([{ ...emptyVatLine(), amount: String(invoice.amount) }])
  const total = lines.reduce((s, r) => s + (Number(r.amount) || 0), 0)
  const linesOk = Math.abs(total - invoice.amount) < 0.01
  const mutation = useMutation({
    mutationFn: () => invoicesApi.adjustVat(invoice.id, Number(rate) || 0, useLines ? toVatLineInputs(lines) : undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })
  return (
    <Modal
      open
      title={`${t('accounting.vat.adjust')} · ${invoice.invoiceNumber}`}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button loading={mutation.isPending} disabled={useLines && !linesOk} onClick={() => mutation.mutate()}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <p className="text-sm text-ink-secondary">{t('accounting.vat.adjustHint', { amount: invoice.amount.toLocaleString() })}</p>
        <Checkbox checked={useLines} onChange={(e) => setUseLines(e.target.checked)} label={t('accounting.vat.useLines')} />
        {useLines ? (
          <>
            <VatLinesEditor rows={lines} onChange={setLines} />
            <p role="status" className={linesOk ? 'text-xs text-success-soft-text' : 'text-xs text-warning-soft-text'}>
              {t('accounting.vat.linesTotal', { total: total.toLocaleString(), amount: invoice.amount.toLocaleString() })}
            </p>
          </>
        ) : (
          <FormField label={t('accounting.vat.adjustPrompt')}>
            <Input type="number" min="0" max="100" step="0.01" className="w-32" value={rate} onChange={(e) => setRate(e.target.value)} />
          </FormField>
        )}
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.vat.failed'))}</Alert>}
      </div>
    </Modal>
  )
}

type EInvoiceStep = 'issue' | 'cancel' | 'replace' | 'adjust' | null

/** The e-invoice of one invoice: its state, issue with the buyer's details, cancel with a reason, replace and adjust (ROADMAP P1-3); each step asks first. */
function EInvoiceDialog({ invoice, canManage, onClose }: { invoice: InvoiceDto; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [buyer, setBuyer] = useState({ buyerName: '', buyerTaxCode: '', buyerAddress: '' })
  const [step, setStep] = useState<EInvoiceStep>(null)
  const { data: all, isLoading } = useQuery({ queryKey: ['accounting', 'invoices', 'e-invoices', invoice.id], queryFn: () => invoicesApi.eInvoices(invoice.id) })
  // the e-invoice in force (original or replacement), else the last one
  const chain = (all ?? []).filter((e) => e.kind !== 'Adjustment')
  const data = chain.find((e) => e.status === 'Issued' || e.status === 'Failed') ?? chain[chain.length - 1]
  const adjustments = (all ?? []).filter((e) => e.kind === 'Adjustment')
  const [replaceReason, setReplaceReason] = useState('')
  const [replaceRate, setReplaceRate] = useState('')
  const [adjust, setAdjust] = useState({ net: '', tax: '', reason: '' })
  const done = (after?: () => void) => () => {
    queryClient.invalidateQueries({ queryKey: ['accounting', 'invoices'] })
    setStep(null)
    after?.()
    toast.success(t('common.saved'))
  }
  const failed = (e: unknown) => {
    setStep(null)
    toast.error(serverMessage(e, t('accounting.vat.failed')))
  }
  const issue = useMutation({ mutationFn: () => invoicesApi.issueEInvoice(invoice.id, buyer), onSuccess: done(), onError: failed })
  const cancel = useMutation({ mutationFn: (reason: string) => invoicesApi.cancelEInvoice(invoice.id, reason), onSuccess: done(), onError: failed })
  const issued = data?.status === 'Issued'
  const replace = useMutation({
    mutationFn: () => invoicesApi.replaceEInvoice(invoice.id, { reason: replaceReason, taxRatePercent: replaceRate === '' ? undefined : Number(replaceRate) }),
    onSuccess: done(() => {
      setReplaceReason('')
      setReplaceRate('')
    }),
    onError: failed,
  })
  const adjustMutation = useMutation({
    mutationFn: () => invoicesApi.adjustEInvoice(invoice.id, { adjustNet: Number(adjust.net) || 0, adjustTax: Number(adjust.tax) || 0, reason: adjust.reason }),
    onSuccess: done(() => setAdjust({ net: '', tax: '', reason: '' })),
    onError: failed,
  })
  const canReplace = !!replaceReason.trim()
  const canAdjust = !!adjust.reason.trim() && (!!Number(adjust.net) || !!Number(adjust.tax))

  return (
    <>
      <Modal open title={`${t('accounting.vat.eInvoice')} · ${invoice.invoiceNumber}`} onClose={onClose} footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}>
        <div className="space-y-3">
          {isLoading && <p className="text-sm text-ink-muted">{t('common.loading')}</p>}
          {data && (
            <div className="space-y-1 text-sm text-ink">
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
              {data.error && <Alert variant="danger">{data.error}</Alert>}
              {data.cancelReason && <p className="text-ink-secondary">{data.cancelReason}</p>}
            </div>
          )}
          {canManage && !issued && !isLoading && (
            <fieldset className="space-y-2">
              <legend className="mb-1 text-sm font-medium text-ink">{t('accounting.vat.buyer')}</legend>
              {(['buyerName', 'buyerTaxCode', 'buyerAddress'] as const).map((k) => (
                <FormField key={k} label={t(`accounting.vat.${k}`)}>
                  <Input value={buyer[k]} onChange={(e) => setBuyer({ ...buyer, [k]: e.target.value })} />
                </FormField>
              ))}
              <Button onClick={() => setStep('issue')}>{t('accounting.vat.issue')}</Button>
            </fieldset>
          )}
          {canManage && issued && (
            <Button variant="link-danger" onClick={() => setStep('cancel')}>
              {t('accounting.vat.cancelEInvoice')}
            </Button>
          )}
          {canManage && issued && (
            <div className="space-y-4 border-t border-line pt-3">
              <fieldset className="space-y-2">
                <legend className="mb-1 text-sm font-medium text-ink">{t('accounting.vat.replace')}</legend>
                <div className="flex flex-wrap items-end gap-2">
                  <FormField label={t('accounting.vat.reason')} required>
                    <Input className="w-48" value={replaceReason} onChange={(e) => setReplaceReason(e.target.value)} />
                  </FormField>
                  <FormField label={t('accounting.vat.newRate')}>
                    <Input type="number" min="0" max="100" step="0.01" className="w-24" value={replaceRate} onChange={(e) => setReplaceRate(e.target.value)} />
                  </FormField>
                  <Button variant="outline" disabled={!canReplace} onClick={() => setStep('replace')}>
                    {t('accounting.vat.replaceButton')}
                  </Button>
                </div>
              </fieldset>
              <fieldset className="space-y-2">
                <legend className="mb-1 text-sm font-medium text-ink">{t('accounting.vat.adjustEInvoice')}</legend>
                <div className="flex flex-wrap items-end gap-2">
                  <FormField label={t('accounting.vat.adjustNet')}>
                    <Input type="number" step="0.01" className="w-28" value={adjust.net} onChange={(e) => setAdjust({ ...adjust, net: e.target.value })} />
                  </FormField>
                  <FormField label={t('accounting.vat.adjustTax')}>
                    <Input type="number" step="0.01" className="w-28" value={adjust.tax} onChange={(e) => setAdjust({ ...adjust, tax: e.target.value })} />
                  </FormField>
                  <FormField label={t('accounting.vat.reason')} required>
                    <Input className="w-48" value={adjust.reason} onChange={(e) => setAdjust({ ...adjust, reason: e.target.value })} />
                  </FormField>
                  <Button variant="outline" disabled={!canAdjust} onClick={() => setStep('adjust')}>
                    {t('accounting.vat.adjustButton')}
                  </Button>
                </div>
              </fieldset>
            </div>
          )}
          {(all ?? []).length > 1 && (
            <ul className="space-y-1 border-t border-line pt-2 text-xs text-ink-secondary">
              {(all ?? []).map((e) => (
                <li key={e.id}>
                  {e.kind} · {e.series}-{e.number ?? '—'} · {e.status}
                  {e.kind === 'Adjustment' ? ` · ${(e.adjustNet ?? 0).toLocaleString()} / ${(e.adjustTax ?? 0).toLocaleString()}` : ''}
                  {e.reason ? ` · ${e.reason}` : ''}
                </li>
              ))}
            </ul>
          )}
          {adjustments.length > 0 && <p className="text-xs text-ink-secondary">{t('accounting.vat.adjustmentsCount', { count: adjustments.length })}</p>}
        </div>
      </Modal>

      <ConfirmDialog
        open={step === 'issue'}
        title={t('accounting.vat.issue')}
        message={t('accounting.vat.issueConfirm', { number: invoice.invoiceNumber })}
        confirmLabel={t('accounting.vat.issue')}
        onConfirm={() => !issue.isPending && issue.mutate()}
        onCancel={() => setStep(null)}
      />
      <ConfirmDialog
        open={step === 'replace'}
        title={t('accounting.vat.replaceButton')}
        message={t('accounting.vat.replaceConfirm')}
        confirmLabel={t('accounting.vat.replaceButton')}
        onConfirm={() => !replace.isPending && replace.mutate()}
        onCancel={() => setStep(null)}
      />
      <ConfirmDialog
        open={step === 'adjust'}
        title={t('accounting.vat.adjustButton')}
        message={t('accounting.vat.adjustConfirm', { net: (Number(adjust.net) || 0).toLocaleString(), tax: (Number(adjust.tax) || 0).toLocaleString() })}
        confirmLabel={t('accounting.vat.adjustButton')}
        onConfirm={() => !adjustMutation.isPending && adjustMutation.mutate()}
        onCancel={() => setStep(null)}
      />
      <ReasonDialog
        open={step === 'cancel'}
        title={t('accounting.vat.cancelEInvoice')}
        message={invoice.invoiceNumber}
        confirmLabel={t('accounting.vat.cancelEInvoice')}
        danger
        busy={cancel.isPending}
        onConfirm={(reason) => cancel.mutate(reason)}
        onCancel={() => setStep(null)}
      />
    </>
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
      <FormField label={t('accounting.invoices.asOfDate')} className="max-w-xs">
        <Input type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} />
      </FormField>
      <DataTable columns={columns} data={rows} isLoading={isLoading} />
    </div>
  )
}

export function InvoicesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canView = hasPermission('accounting:invoices:view')
  const canManage = hasPermission('accounting:invoices:manage')

  if (!canView) {
    return <EmptyState variant="forbidden" title={t('common.noPermission')} />
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.invoices.title')}</h1>
      <PageTabs
        id="invoices"
        label={t('accounting.invoices.title')}
        tabs={[
          { key: 'invoices', label: t('accounting.invoices.tabInvoices'), content: <InvoicesListTab canManage={canManage} /> },
          { key: 'aging', label: t('accounting.invoices.tabArAging'), content: <ARAgingTab /> },
        ]}
      />
    </div>
  )
}
