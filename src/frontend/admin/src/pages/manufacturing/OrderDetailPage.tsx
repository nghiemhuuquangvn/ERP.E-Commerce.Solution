import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type MaterialAvailability, type Order, type OrderActionResult } from '@/api/manufacturing/mfg.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { TabList } from '@/components/shared/PageTabs'
import { Alert, Button, FormField, Input, Modal, Skeleton, Textarea, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'
import { CloseOrderDialog } from '@/components/manufacturing/CloseOrderDialog'
import { IssueReturnDialog } from '@/components/manufacturing/IssueReturnDialog'
import { LateFlag, OrderStatusBadge } from '@/components/manufacturing/OrderBadges'
import { OrderFormModal } from '@/components/manufacturing/OrderFormModal'
import { DocumentsTab, HistoryTab, MaterialsTab, OperationsTab, QualityTab, ReceiptsTab } from '@/components/manufacturing/OrderTabs'
import { ReasonCodeDialog } from '@/components/manufacturing/ReasonCodeDialog'
import { ReceiptDialog } from '@/components/manufacturing/ReceiptDialog'
import { SHORT_CLOSE_REFUSAL, errorCodeOf, orderActions } from '@/components/manufacturing/orderActions'
import { PageHeader, fmtDate, fmtDateTime, fmtQty, usePlantContext } from '@/components/manufacturing/shared'

type TabKey = 'overview' | 'materials' | 'operations' | 'receipts' | 'quality' | 'documents' | 'history'
type Dialog =
  | { kind: 'issue' | 'return'; lineNo?: number }
  | { kind: 'receive' | 'edit' | 'close' | 'quantity' | 'unrelease' | 'resume' | 'reopen' | 'cancel' | 'hold' | 'shortClose' }
  | null

/** S16 - one production order: overview, materials, operations, receipts, stock documents and history, with the actions the status and the user's permissions allow. */
export function ProductionOrderDetailPage() {
  const { id = '' } = useParams()
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const { plants } = usePlantContext()
  const [tab, setTab] = useState<TabKey>('overview')
  const [dialog, setDialog] = useState<Dialog>(null)
  const [busy, setBusy] = useState(false)
  const [dialogError, setDialogError] = useState<string | null>(null)
  const [shortages, setShortages] = useState<MaterialAvailability[]>([])
  const [newQuantity, setNewQuantity] = useState('')
  const [quantityNote, setQuantityNote] = useState('')

  const order = useQuery({
    queryKey: ['mfg', 'order', id],
    queryFn: () => mfgApi.getOrder(id),
    // while documents are on their way to the Warehouse (or the reservation is being brought in line) the page looks again every 5 s
    refetchInterval: (q) => (q.state.data && (q.state.data.pendingStockDocuments > 0 || q.state.data.reservationSyncPending) ? 5000 : false),
  })
  const documents = useQuery({
    queryKey: ['mfg', 'order', id, 'documents'],
    queryFn: () => mfgApi.orderDocuments(id),
    refetchInterval: () => (order.data && order.data.pendingStockDocuments > 0 ? 5000 : false),
  })

  // P4-3: the inspections of the order (the Quality tab, and the receipt lines that have one)
  const inspections = useQuery({ queryKey: ['mfg', 'order', id, 'inspections'], queryFn: () => mfgApi.orderInspections(id), enabled: hasPermission('manufacturing:qc:view') })

  const reload = () => {
    queryClient.invalidateQueries({ queryKey: ['mfg', 'order', id] })
    queryClient.invalidateQueries({ queryKey: ['mfg', 'orders'] })
  }

  /** The answer of a transition replaces the cached order (only when it is an order). */
  const cache = (next: unknown) => { if (next && typeof next === 'object' && 'id' in next) queryClient.setQueryData(['mfg', 'order', id], next) }

  if (order.isLoading) return <Skeleton className="h-64 w-full" />
  if (order.isError || !order.data) return <Alert variant="danger">{serverMessage(order.error, t('mfg.order.notFound'))}</Alert>
  const o = order.data
  const can = orderActions(o.status, hasPermission)
  const plant = plants.find((p) => p.id === o.plantId) ?? null
  const docs = documents.data ?? []

  /** Runs one transition; the order in the answer replaces the cached one. A refusal stays in the open dialog or a toast, never swallowed. */
  const run = async (fn: () => Promise<OrderActionResult | Order>, ok: string, onRefused?: (code: string | null) => boolean) => {
    setBusy(true)
    setDialogError(null)
    try {
      const answer = await fn()
      const result = 'order' in answer ? answer : null
      cache(result ? result.order : answer)
      setShortages(result?.shortages ?? [])
      if (result?.reservationPending) toast.info(t('mfg.order.reservationPending'))
      else toast.success(ok)
      setDialog(null)
      reload()
    } catch (e) {
      if (onRefused?.(errorCodeOf(e))) return
      const message = serverMessage(e, t('common.failedToSave'))
      // dialogs with a form show the refusal inside; a plain confirmation closes and the refusal goes to a toast
      if (dialog && ['cancel', 'hold', 'shortClose', 'quantity'].includes(dialog.kind)) setDialogError(message)
      else { setDialog(null); toast.error(message) }
    } finally {
      setBusy(false)
    }
  }

  const tabs: { key: TabKey; label: string }[] = [
    { key: 'overview', label: t('mfg.order.tabs.overview') }, { key: 'materials', label: t('mfg.order.tabs.materials') }, { key: 'operations', label: t('mfg.order.tabs.operations') },
    { key: 'receipts', label: t('mfg.order.tabs.receipts') }, ...(can.viewQuality ? [{ key: 'quality' as const, label: t('mfg.order.tabs.quality') }] : []),
    { key: 'documents', label: t('mfg.order.tabs.documents') }, { key: 'history', label: t('mfg.order.tabs.history') },
  ]

  const actions = (
    <>
      {can.edit && <Button variant="outline" onClick={() => setDialog({ kind: 'edit' })}>{t('mfg.order.edit')}</Button>}
      {can.release && <Button loading={busy} onClick={() => run(() => mfgApi.releaseOrder(o.id), t('mfg.order.released'))}>{t('mfg.order.release')}</Button>}
      {can.unrelease && <Button variant="outline" onClick={() => setDialog({ kind: 'unrelease' })}>{t('mfg.order.unrelease')}</Button>}
      {can.resume && <Button onClick={() => setDialog({ kind: 'resume' })}>{t('mfg.order.resume')}</Button>}
      {can.receive && <Button onClick={() => setDialog({ kind: 'receive' })}>{t('mfg.receipt.receive')}</Button>}
      {can.complete && <Button variant="secondary" loading={busy} onClick={() => run(() => mfgApi.completeOrder(o.id), t('mfg.order.completed'), (code) => { if (code === SHORT_CLOSE_REFUSAL) { setDialog({ kind: 'shortClose' }); return true } return false })}>{t('mfg.order.complete')}</Button>}
      {can.reopen && <Button variant="outline" onClick={() => setDialog({ kind: 'reopen' })}>{t('mfg.order.reopen')}</Button>}
      {can.close && <Button variant="danger" onClick={() => setDialog({ kind: 'close' })}>{t('mfg.order.close')}</Button>}
      {can.changeQuantity && <Button variant="outline" onClick={() => { setNewQuantity(String(o.quantity)); setQuantityNote(''); setDialogError(null); setDialog({ kind: 'quantity' }) }}>{t('mfg.order.changeQuantity')}</Button>}
      {can.hold && <Button variant="outline" onClick={() => { setDialogError(null); setDialog({ kind: 'hold' }) }}>{t('mfg.order.hold')}</Button>}
      {can.cancel && <Button variant="outline" onClick={() => { setDialogError(null); setDialog({ kind: 'cancel' }) }}>{t('mfg.order.cancel')}</Button>}
    </>
  )

  const closeDialog = () => { setDialog(null); setDialogError(null) }
  const reasonDone = (kind: 'cancel' | 'hold' | 'shortClose') => (reasonId: string, note: string) =>
    run(
      () => (kind === 'cancel' ? mfgApi.cancelOrder(o.id, reasonId, note || null) : kind === 'hold' ? mfgApi.holdOrder(o.id, reasonId, note || null) : mfgApi.completeOrder(o.id, reasonId, note || null)),
      t(kind === 'cancel' ? 'mfg.order.cancelled' : kind === 'hold' ? 'mfg.order.held' : 'mfg.order.completed'),
    )

  return (
    <div>
      <PageHeader
        title={`${o.orderNo} · ${o.itemCode}`}
        hint={o.itemName}
        actions={<Link className="text-sm text-brand-action hover:underline" to="/manufacturing/orders">{t('mfg.order.backToList')}</Link>}
      />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <OrderStatusBadge status={o.status} />
        {o.status === 'OnHold' && o.statusBeforeHold && <span className="text-xs text-ink-secondary">{t('mfg.order.heldFrom', { status: t(`mfg.orderStatus.${o.statusBeforeHold}`) })}</span>}
        <LateFlag late={o.isLate} />
        <div className="ml-auto flex flex-wrap gap-2">{actions}</div>
      </div>

      <div className="mb-4 space-y-2">
        {o.pendingStockDocuments > 0 && <Alert variant="info">{t('mfg.order.pendingDocs', { count: o.pendingStockDocuments })}</Alert>}
        {o.reservationSyncPending && <Alert variant="info">{t('mfg.order.syncPending')}</Alert>}
        {o.reservationSyncError && <Alert variant="warning">{t('mfg.order.syncError', { error: o.reservationSyncError })}</Alert>}
        {shortages.length > 0 && (
          <Alert variant="warning" title={t('mfg.order.shortagesTitle')} onClose={() => setShortages([])}>
            {shortages.map((s) => `${s.componentCode}: ${t('mfg.order.short', { qty: fmtQty(s.shortage) })}`).join(' · ')}
          </Alert>
        )}
        {o.warnings.length > 0 && <Alert variant="warning" title={t('mfg.order.warnings')}><ul className="list-disc pl-5">{o.warnings.map((w) => <li key={w}>{w}</li>)}</ul></Alert>}
      </div>

      <TabList id="mfg-order" label={t('mfg.order.tabsLabel')} tabs={tabs} current={tab} onSelect={setTab} />
      <div role="tabpanel" id={`mfg-order-panel-${tab}`} aria-labelledby={`mfg-order-tab-${tab}`} className="pt-4">
        {tab === 'overview' && <Overview order={o} />}
        {tab === 'materials' && <MaterialsTab order={o} can={can} onIssue={(lineNo) => setDialog({ kind: 'issue', lineNo })} onReturn={(lineNo) => setDialog({ kind: 'return', lineNo })} />}
        {tab === 'operations' && <OperationsTab order={o} can={can} reload={reload} />}
        {tab === 'receipts' && <ReceiptsTab order={o} can={can} documents={docs} inspections={inspections.data ?? []} onReceive={() => setDialog({ kind: 'receive' })} reload={reload} />}
        {tab === 'quality' && <QualityTab inspections={inspections.data ?? []} />}
        {tab === 'documents' && <DocumentsTab documents={docs} can={can} reload={reload} />}
        {tab === 'history' && <HistoryTab order={o} />}
      </div>

      {(dialog?.kind === 'issue' || dialog?.kind === 'return') && (
        <IssueReturnDialog order={o} mode={dialog.kind} initialLineNo={dialog.lineNo} onClose={closeDialog} onDone={() => { closeDialog(); reload(); setTab('documents') }} />
      )}
      {dialog?.kind === 'receive' && <ReceiptDialog order={o} onClose={closeDialog} onDone={() => { closeDialog(); reload(); setTab('receipts') }} />}
      {dialog?.kind === 'edit' && plant && <OrderFormModal plant={plant} order={o} onClose={closeDialog} onSaved={(saved) => { cache(saved); closeDialog(); reload() }} />}
      {dialog?.kind === 'close' && <CloseOrderDialog order={o} onClose={closeDialog} onClosed={(closed) => { cache(closed); closeDialog(); reload() }} />}

      <ConfirmDialog open={dialog?.kind === 'unrelease'} title={t('mfg.order.unreleaseTitle')} message={t('mfg.order.unreleaseMessage', { no: o.orderNo })} confirmLabel={t('mfg.order.unrelease')} onCancel={closeDialog} onConfirm={() => run(() => mfgApi.unreleaseOrder(o.id), t('mfg.order.unreleased'))} />
      <ConfirmDialog open={dialog?.kind === 'resume'} title={t('mfg.order.resumeTitle')} message={t('mfg.order.resumeMessage', { no: o.orderNo })} confirmLabel={t('mfg.order.resume')} onCancel={closeDialog} onConfirm={() => run(() => mfgApi.resumeOrder(o.id), t('mfg.order.resumed'))} />
      <ConfirmDialog open={dialog?.kind === 'reopen'} title={t('mfg.order.reopenTitle')} message={t('mfg.order.reopenMessage', { no: o.orderNo })} confirmLabel={t('mfg.order.reopen')} onCancel={closeDialog} onConfirm={() => run(() => mfgApi.reopenOrder(o.id), t('mfg.order.reopened'))} />

      <ReasonCodeDialog open={dialog?.kind === 'cancel'} type="Cancel" danger title={t('mfg.order.cancelTitle', { no: o.orderNo })} message={t('mfg.order.cancelMessage')} confirmLabel={t('mfg.order.cancel')} busy={busy} error={dialogError} onCancel={closeDialog} onConfirm={reasonDone('cancel')} />
      <ReasonCodeDialog open={dialog?.kind === 'hold'} type="Hold" title={t('mfg.order.holdTitle', { no: o.orderNo })} message={t('mfg.order.holdMessage')} confirmLabel={t('mfg.order.hold')} busy={busy} error={dialogError} onCancel={closeDialog} onConfirm={reasonDone('hold')} />
      <ReasonCodeDialog open={dialog?.kind === 'shortClose'} type="ShortClose" title={t('mfg.order.shortCloseTitle', { no: o.orderNo })} message={t('mfg.order.shortCloseMessage', { done: fmtQty(o.completedQty), planned: fmtQty(o.quantity) })} confirmLabel={t('mfg.order.complete')} busy={busy} error={dialogError} onCancel={closeDialog} onConfirm={reasonDone('shortClose')} />

      <Modal
        open={dialog?.kind === 'quantity'}
        size="sm"
        title={t('mfg.order.quantityTitle', { no: o.orderNo })}
        onClose={closeDialog}
        closeOnEsc={!busy}
        footer={<><Button variant="outline" onClick={closeDialog}>{t('common.cancel')}</Button><Button disabled={!(Number(newQuantity) > 0)} loading={busy} onClick={() => run(() => mfgApi.changeQuantity(o.id, Number(newQuantity), quantityNote.trim() || null), t('mfg.order.quantityChanged'))}>{t('common.save')}</Button></>}
      >
        <div className="space-y-4">
          <p className="text-sm text-ink-secondary">{t('mfg.order.quantityHint', { done: fmtQty(o.completedQty) })}</p>
          {dialogError && <Alert variant="danger">{dialogError}</Alert>}
          <FormField label={t('mfg.order.quantity')} required><Input type="number" step="any" min="0" value={newQuantity} onChange={(e) => setNewQuantity(e.target.value)} /></FormField>
          <FormField label={t('mfg.reasonDialog.note')}><Textarea rows={2} maxLength={500} value={quantityNote} onChange={(e) => setQuantityNote(e.target.value)} /></FormField>
        </div>
      </Modal>
    </div>
  )
}

function Overview({ order: o }: { order: Order }) {
  const { t } = useTranslation()
  const facts: [string, React.ReactNode][] = [
    [t('mfg.order.item'), `${o.itemCode} - ${o.itemName}`],
    [t('mfg.order.quantity'), fmtQty(o.quantity)],
    [t('mfg.order.completedQty'), fmtQty(o.completedQty)],
    [t('mfg.order.scrappedQty'), fmtQty(o.scrappedQty)],
    [t('mfg.order.priority'), String(o.priority)],
    [t('mfg.order.dueDate'), fmtDate(o.dueDate)],
    [t('mfg.order.plannedStart'), fmtDateTime(o.plannedStart)],
    [t('mfg.order.plannedEnd'), fmtDateTime(o.plannedEnd)],
    [t('mfg.order.actualStart'), fmtDateTime(o.actualStart)],
    [t('mfg.order.actualEnd'), fmtDateTime(o.actualEnd)],
    [t('mfg.order.operations'), String(o.operations.length)],
    [t('mfg.order.materials'), String(o.materials.length)],
    [t('mfg.order.notes'), o.notes ?? ''],
  ]
  return (
    <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
      {facts.map(([label, value]) => (
        <div key={label}><dt className="text-xs font-medium uppercase tracking-wider text-ink-secondary">{label}</dt><dd className="mt-0.5 text-sm text-ink">{value || '—'}</dd></div>
      ))}
    </dl>
  )
}
