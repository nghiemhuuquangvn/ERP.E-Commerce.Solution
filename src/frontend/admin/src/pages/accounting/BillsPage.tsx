import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import { billsApi, BILL_STATUS } from '@/api/accounting/bills.api'
import type { BillDto } from '@/api/accounting/bills.api'
import { PageTabs } from '@/components/shared/PageTabs'
import { ForeignCurrencyFields, emptyFx, isForeign, toFxRequest } from '@/components/accounting/ForeignCurrencyFields'
import type { FxFields } from '@/components/accounting/ForeignCurrencyFields'
import { PayDialog } from '@/components/accounting/PayDialog'
import { PostingGapsBar } from '@/components/accounting/PostingGapsBar'
import { VatLinesEditor, emptyVatLine, toVatLineInputs } from '@/components/accounting/VatLinesEditor'
import type { VatLineRow } from '@/components/accounting/VatLinesEditor'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<CreateBillFormValues>({
    defaultValues: { billNumber: '', supplierId: '', poId: '', amount: 0, dueDate: '' },
  })
  const [useLines, setUseLines] = useState(false)
  const [lines, setLines] = useState<VatLineRow[]>([emptyVatLine()])
  const [fx, setFx] = useState<FxFields>(emptyFx())

  const mutation = useMutation({
    mutationFn: (values: CreateBillFormValues) =>
      billsApi.create({
        billNumber: values.billNumber,
        supplierId: values.supplierId,
        poId: values.poId,
        ...toFxRequest(fx),
        amount: useLines || isForeign(fx) ? 0 : Number(values.amount),
        dueDate: values.dueDate,
        lines: useLines ? toVatLineInputs(lines) : undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'bills'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      title={t('accounting.bills.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" form="create-bill-form" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </>
      }
    >
      <form id="create-bill-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('accounting.bills.billNumber')} required error={errors.billNumber ? t('common.required') : undefined}>
          <Input maxLength={50} {...register('billNumber', { required: true })} />
        </FormField>
        <FormField label={t('accounting.bills.supplierId')} required error={errors.supplierId ? t('common.required') : undefined}>
          <Input {...register('supplierId', { required: true })} />
        </FormField>
        <FormField label={t('accounting.bills.poId')} required error={errors.poId ? t('common.required') : undefined}>
          <Input {...register('poId', { required: true })} />
        </FormField>
        <ForeignCurrencyFields value={fx} onChange={setFx} />
        {!isForeign(fx) && <Checkbox checked={useLines} onChange={(e) => setUseLines(e.target.checked)} label={t('accounting.vat.useLines')} />}
        {isForeign(fx) ? null : useLines ? (
          <VatLinesEditor rows={lines} onChange={setLines} />
        ) : (
          <FormField label={t('accounting.bills.amount')} required error={errors.amount ? t('accounting.amountPositive') : undefined}>
            <Input type="number" step="0.01" min="0.01" {...register('amount', { required: !useLines, valueAsNumber: true, min: useLines ? undefined : 0.01 })} />
          </FormField>
        )}
        <FormField label={t('accounting.bills.dueDate')} required error={errors.dueDate ? t('common.required') : undefined}>
          <Input type="date" {...register('dueDate', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('accounting.bills.failedToCreate'))}</Alert>}
      </form>
    </Modal>
  )
}

/** Approving books the bill to the ledger; the supplier's invoice shows the real VAT rate, empty / unchanged keeps the split made when the bill was created. */
function ApproveBillDialog({ bill, busy, onConfirm, onCancel }: { bill: BillDto; busy: boolean; onConfirm: (rate?: number) => void; onCancel: () => void }) {
  const { t } = useTranslation()
  const [typed, setTyped] = useState(String(bill.taxRatePercent))
  const changed = typed.trim() !== '' && !Number.isNaN(Number(typed)) && Number(typed) !== bill.taxRatePercent
  return (
    <Modal
      open
      title={t('accounting.bills.approveTitle', { number: bill.billNumber })}
      onClose={onCancel}
      size="sm"
      closeOnEsc={!busy}
      footer={
        <>
          <Button variant="outline" onClick={onCancel}>
            {t('common.cancel')}
          </Button>
          <Button loading={busy} onClick={() => onConfirm(changed ? Number(typed) : undefined)}>
            {t('accounting.bills.approve')}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <p className="text-sm text-ink-secondary">{t('accounting.bills.approveConfirm', { amount: bill.amount.toLocaleString() })}</p>
        <FormField label={t('accounting.vat.approvePrompt')}>
          <Input type="number" min="0" max="100" step="0.01" value={typed} onChange={(e) => setTyped(e.target.value)} />
        </FormField>
      </div>
    </Modal>
  )
}

type BillStep = { kind: 'approve'; bill: BillDto } | { kind: 'pay'; bill: BillDto }

function BillsListTab({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [pageNumber, setPageNumber] = useState(1)
  const [supplierId, setSupplierId] = useState('')
  const [status, setStatus] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [step, setStep] = useState<BillStep | null>(null)

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

  const done = () => {
    queryClient.invalidateQueries({ queryKey: ['accounting'] })
    setStep(null)
    toast.success(t('common.saved'))
  }
  const failed = (e: unknown) => {
    setStep(null)
    toast.error(serverMessage(e, t('accounting.bills.actionFailed')))
  }
  const approveMutation = useMutation({
    mutationFn: ({ id, rate }: { id: string; rate?: number }) => billsApi.approve(id, rate),
    onSuccess: done,
    onError: failed,
  })
  const markPaidMutation = useMutation({
    mutationFn: ({ id, paymentRate }: { id: string; paymentRate?: number }) => billsApi.markPaid(id, paymentRate),
    onSuccess: done,
    onError: failed,
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
              <span className="ml-1 text-warning" title={b.postingWarning ?? ''}>
                <AlertTriangle size={16} strokeWidth={1.75} aria-label={t('accounting.notPosted')} />
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
        render: (b: BillDto) => (
          <span>
            {b.amount.toLocaleString()}
            {b.foreignCurrencyCode && (
              <span className="block text-xs text-ink-secondary">
                {b.foreignAmount?.toLocaleString()} {b.foreignCurrencyCode} @ {b.exchangeRate?.toLocaleString()}
              </span>
            )}
          </span>
        ),
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
                <Button variant="link" className="text-xs" aria-label={`${t('accounting.bills.approve')} ${b.billNumber}`} onClick={() => setStep({ kind: 'approve', bill: b })}>
                  {t('accounting.bills.approve')}
                </Button>
              )}
              {b.status !== 'Paid' && (
                <Button variant="link" className="text-xs" aria-label={`${t('accounting.bills.markPaid')} ${b.billNumber}`} onClick={() => setStep({ kind: 'pay', bill: b })}>
                  {t('accounting.bills.markPaid')}
                </Button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <PostingGapsBar canManage={canManage} />
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <FormField label={t('accounting.bills.supplierId')}>
            <Input
              value={supplierId}
              onChange={(e) => {
                setSupplierId(e.target.value)
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
              <option value={String(BILL_STATUS.Pending)}>{t('accounting.bills.statusPending')}</option>
              <option value={String(BILL_STATUS.Approved)}>{t('accounting.bills.statusApproved')}</option>
              <option value={String(BILL_STATUS.Paid)}>{t('accounting.bills.statusPaid')}</option>
            </Select>
          </FormField>
        </div>
        {canManage && <Button onClick={() => setCreateOpen(true)}>{t('accounting.bills.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={items}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('accounting.bills.empty')} action={canManage ? <Button onClick={() => setCreateOpen(true)}>{t('accounting.bills.add')}</Button> : undefined} />}
      />

      <div className="flex justify-end gap-2">
        <Button variant="outline" disabled={pageNumber === 1} onClick={() => setPageNumber((p) => Math.max(1, p - 1))}>
          {t('common.prev')}
        </Button>
        <Button variant="outline" disabled={items.length < PAGE_SIZE} onClick={() => setPageNumber((p) => p + 1)}>
          {t('common.next')}
        </Button>
      </div>

      {createOpen && <CreateBillModal onClose={() => setCreateOpen(false)} />}
      {step?.kind === 'approve' && (
        <ApproveBillDialog
          bill={step.bill}
          busy={approveMutation.isPending}
          onConfirm={(rate) => !approveMutation.isPending && approveMutation.mutate({ id: step.bill.id, rate })}
          onCancel={() => setStep(null)}
        />
      )}
      <PayDialog
        open={step?.kind === 'pay'}
        title={t('accounting.bills.payTitle', { number: step?.bill.billNumber ?? '' })}
        message={t('accounting.bills.payConfirm', { amount: step?.bill.amount.toLocaleString() ?? '' })}
        currency={step?.bill.foreignCurrencyCode}
        defaultRate={step?.bill.exchangeRate}
        busy={markPaidMutation.isPending}
        onConfirm={(paymentRate) => step && !markPaidMutation.isPending && markPaidMutation.mutate({ id: step.bill.id, paymentRate })}
        onCancel={() => setStep(null)}
      />
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
      <FormField label={t('accounting.bills.asOfDate')} className="max-w-xs">
        <Input type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} />
      </FormField>
      <DataTable columns={columns} data={rows} isLoading={isLoading} />
    </div>
  )
}

export function BillsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canView = hasPermission('accounting:bills:view')
  const canManage = hasPermission('accounting:bills:manage')

  if (!canView) {
    return <EmptyState variant="forbidden" title={t('common.noPermission')} />
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.bills.title')}</h1>
      <PageTabs
        id="bills"
        label={t('accounting.bills.title')}
        tabs={[
          { key: 'bills', label: t('accounting.bills.tabBills'), content: <BillsListTab canManage={canManage} /> },
          { key: 'aging', label: t('accounting.bills.tabApAging'), content: <APAgingTab /> },
        ]}
      />
    </div>
  )
}
