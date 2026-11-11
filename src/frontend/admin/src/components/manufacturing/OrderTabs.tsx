import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Order, type OperationReport, type OrderOperation, type OrderMaterial, type StockDocument, type TimeLog, type InspectionListItem } from '@/api/manufacturing/mfg.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { Alert, Badge, Button, Progress, Skeleton, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { ManualTimeDialog, ReportOperationDialog, StartOperationDialog } from './OperationDialogs'
import { DocStatusBadge, OperationStatusBadge } from './OrderBadges'
import { StockDocumentsTable } from './StockDocumentsTable'
import type { OrderActions } from './orderActions'
import { fmtDateTime, fmtQty, useIdempotencyKey } from './shared'

const th = 'px-3 py-2 text-left text-xs font-semibold uppercase tracking-wider text-ink-secondary'
const td = 'px-3 py-2 align-top'
const num = `${td} text-right tabular-nums`

function TableBox({ caption, children }: { caption: string; children: React.ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-line">
      <table className="min-w-full divide-y divide-line bg-surface text-sm">
        <caption className="sr-only">{caption}</caption>
        {children}
      </table>
    </div>
  )
}

// ── Materials ────────────────────────────────────────────────────────────────────────────────────────────

export function MaterialsTab({ order, can, onIssue, onReturn }: { order: Order; can: OrderActions; onIssue: (lineNo?: number) => void; onReturn: (lineNo?: number) => void }) {
  const { t } = useTranslation()
  const [check, setCheck] = useState(false)
  const availability = useQuery({ queryKey: ['mfg', 'order', order.id, 'availability'], queryFn: () => mfgApi.orderAvailability(order.id), enabled: check })
  const live = order.status !== 'Closed' && order.status !== 'Cancelled'
  const net = (m: OrderMaterial) => m.issuedQty - m.returnedQty

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        {can.issue && <Button onClick={() => onIssue()}>{t('mfg.order.issue')}</Button>}
        {can.returnMaterial && <Button variant="outline" onClick={() => onReturn()}>{t('mfg.order.return')}</Button>}
        {live && <Button variant="secondary" loading={availability.isFetching} onClick={() => (check ? availability.refetch() : setCheck(true))}>{t('mfg.order.checkAvailability')}</Button>}
      </div>
      {availability.isError && <Alert variant="danger">{serverMessage(availability.error, t('mfg.order.availabilityFailed'))}</Alert>}
      {availability.data && (
        <div className="space-y-2">
          <Alert variant={availability.data.hasShortage ? 'warning' : 'success'}>{availability.data.hasShortage ? t('mfg.order.hasShortage') : t('mfg.order.noShortage')}</Alert>
          <TableBox caption={t('mfg.order.availability')}>
            <thead className="bg-surface-muted"><tr>
              <th scope="col" className={th}>{t('mfg.order.component')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.order.required')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.order.need')}</th>
              <th scope="col" className={`${th} text-right`}>{t('mfg.order.allocatable')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.order.shortage')}</th>
            </tr></thead>
            <tbody className="divide-y divide-line">
              {availability.data.materials.map((a) => (
                <tr key={a.lineNo}>
                  <td className={td}><span className="font-mono text-xs">{a.componentCode}</span> <span className="text-ink-secondary">{a.componentName}</span></td>
                  <td className={num}>{fmtQty(a.requiredQty)}</td><td className={num}>{fmtQty(a.need)}</td><td className={num}>{fmtQty(a.allocatable)}</td>
                  <td className={num}>{a.shortage > 0 ? <span className="font-medium text-danger-soft-text">{t('mfg.order.short', { qty: fmtQty(a.shortage) })}</span> : '0'}</td>
                </tr>
              ))}
            </tbody>
          </TableBox>
        </div>
      )}
      <TableBox caption={t('mfg.order.materials')}>
        <thead className="bg-surface-muted"><tr>
          <th scope="col" className={th}>#</th><th scope="col" className={th}>{t('mfg.order.component')}</th><th scope="col" className={th}>{t('mfg.order.op')}</th><th scope="col" className={th}>{t('mfg.order.method')}</th>
          <th scope="col" className={`${th} text-right`}>{t('mfg.order.required')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.order.reserved')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.order.issued')}</th>
          <th scope="col" className={`${th} text-right`}>{t('mfg.order.returned')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.order.consumed')}</th><th scope="col" className={th}>{t('mfg.order.issuedPct')}</th>
          <th scope="col" className={th}><span className="sr-only">{t('common.actions')}</span></th>
        </tr></thead>
        <tbody className="divide-y divide-line">
          {order.materials.map((m) => {
            const rest = m.requiredQty - net(m) - m.consumedQty
            return (
              <tr key={m.lineNo}>
                <td className={`${td} font-mono text-xs`}>{m.lineNo}</td>
                <td className={td}><span className="font-mono text-xs">{m.componentCode}</span> <span className="text-ink-secondary">{m.componentName}</span>{m.isSubstitute && <Badge variant="muted" className="ml-1">{t('mfg.order.substitute')}</Badge>}</td>
                <td className={td}>{m.operationSeq ?? ''}</td>
                <td className={td}>{t(`mfg.issue.${m.issueMethod}`)}</td>
                <td className={num}>{fmtQty(m.requiredQty)}</td><td className={num}>{fmtQty(m.reservedQty)}</td><td className={num}>{fmtQty(m.issuedQty)}</td>
                <td className={num}>{fmtQty(m.returnedQty)}</td><td className={num}>{fmtQty(m.consumedQty)}</td>
                <td className={td}><div className="w-24"><Progress label={t('mfg.order.issuedOf', { code: m.componentCode })} value={m.requiredQty > 0 ? ((net(m) + m.consumedQty) / m.requiredQty) * 100 : 0} /></div></td>
                <td className={td}>{can.issue && rest > 0 && m.issueMethod !== 'Backflush' && <Button size="sm" variant="ghost" onClick={() => onIssue(m.lineNo)}>{t('mfg.order.issueRest')}</Button>}</td>
              </tr>
            )
          })}
        </tbody>
      </TableBox>
    </div>
  )
}

// ── Operations ───────────────────────────────────────────────────────────────────────────────────────────

type Dialog = { kind: 'start' | 'manual' | 'report'; op: OrderOperation } | null

export function OperationsTab({ order, can, reload }: { order: Order; can: OrderActions; reload: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [dialog, setDialog] = useState<Dialog>(null)
  const [confirm, setConfirm] = useState<{ kind: 'complete' | 'reopen'; op: OrderOperation } | null>(null)
  const [reversing, setReversing] = useState<OperationReport | null>(null)
  const [reverseError, setReverseError] = useState<string | null>(null)
  const reverseKey = useIdempotencyKey()
  const details = useQuery({ queryKey: ['mfg', 'order', order.id, 'operations'], queryFn: () => mfgApi.orderOperations(order.id) })
  const reports = useQuery({ queryKey: ['mfg', 'order', order.id, 'reports'], queryFn: () => mfgApi.operationReports(order.id) })
  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'order', order.id] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'orders'] }); reload() }

  const log = useMutation({
    mutationFn: ({ op, id, action }: { op: number; id: string; action: 'pause' | 'resume' | 'stop' }) =>
      action === 'pause' ? mfgApi.pauseLog(order.id, op, id) : action === 'resume' ? mfgApi.resumeLog(order.id, op, id) : mfgApi.stopLog(order.id, op, id),
    onSuccess: refresh,
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  const reverse = useMutation({
    mutationFn: ({ report, note }: { report: OperationReport; note: string }) => mfgApi.reverseOperationReport(order.id, report.id, note, reverseKey({ report: report.id, note })),
    onSuccess: (r) => { setReversing(null); setReverseError(null); refresh(); r.isPending ? toast.info(t('mfg.rev.pending')) : toast.success(t('mfg.rev.reportReversed')) },
    onError: (e) => setReverseError(serverMessage(e, t('common.failedToSave'))),
  })
  const manual = useMutation({
    mutationFn: ({ kind, op }: { kind: 'complete' | 'reopen'; op: OrderOperation }) => (kind === 'complete' ? mfgApi.completeOperation(order.id, op.operationSeq) : mfgApi.reopenOperation(order.id, op.operationSeq)),
    onSuccess: () => { setConfirm(null); refresh(); toast.success(t('common.saved')) },
    onError: (e) => { setConfirm(null); toast.error(serverMessage(e, t('common.failedToSave'))) },
  })

  if (details.isLoading) return <Skeleton className="h-40 w-full" />
  if (details.isError) return <Alert variant="danger">{t('mfg.order.loadFailed')}</Alert>
  const list = details.data ?? []
  if (list.length === 0) return <p className="text-sm text-ink-secondary">{t('mfg.opx.none')}</p>

  return (
    <div className="space-y-6">
      {list.map(({ operation: op, timeLogs }) => {
        const planned = op.status === 'Completed' ? op.goodQty + op.scrapQty + op.reworkQty : order.quantity
        const canStart = can.work && (op.status === 'Ready' || op.status === 'Paused' || op.status === 'Running')
        return (
          <section key={op.operationSeq} aria-label={t('mfg.opx.operationLabel', { seq: op.operationSeq, name: op.name })} className="rounded-lg border border-line bg-surface p-4">
            <div className="mb-3 flex flex-wrap items-center gap-3">
              <h2 className="text-sm font-semibold text-ink">{op.operationSeq} · {op.name}</h2>
              <OperationStatusBadge status={op.status} />
              <span className="text-xs text-ink-secondary">{op.workCenterCode}{op.isSubcontract ? ` · ${t('mfg.opx.subcontract')}` : ''}</span>
              <span className="text-xs text-ink-secondary">{fmtDateTime(op.plannedStart)} → {fmtDateTime(op.plannedEnd)}</span>
              <div className="ml-auto flex flex-wrap gap-2">
                {canStart && <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: 'start', op })}>{t('mfg.opx.start')}</Button>}
                {can.work && op.status !== 'Pending' && op.status !== 'Completed' && <Button size="sm" variant="outline" onClick={() => setDialog({ kind: 'manual', op })}>{t('mfg.opx.addTime')}</Button>}
                {can.work && op.status !== 'Pending' && op.status !== 'Completed' && <Button size="sm" onClick={() => setDialog({ kind: 'report', op })}>{t('mfg.opx.report')}</Button>}
                {can.operationManual && (op.status === 'Ready' || op.status === 'Running') && <Button size="sm" variant="outline" onClick={() => setConfirm({ kind: 'complete', op })}>{t('mfg.opx.complete')}</Button>}
                {can.operationManual && op.status === 'Completed' && <Button size="sm" variant="outline" onClick={() => setConfirm({ kind: 'reopen', op })}>{t('mfg.opx.reopen')}</Button>}
              </div>
            </div>
            <dl className="mb-3 grid grid-cols-3 gap-3 text-sm sm:max-w-md">
              <div><dt className="text-xs text-ink-secondary">{t('mfg.opx.good')}</dt><dd className="tabular-nums">{fmtQty(op.goodQty)}</dd></div>
              <div><dt className="text-xs text-ink-secondary">{t('mfg.opx.scrap')}</dt><dd className="tabular-nums">{fmtQty(op.scrapQty)}</dd></div>
              <div><dt className="text-xs text-ink-secondary">{t('mfg.opx.rework')}</dt><dd className="tabular-nums">{fmtQty(op.reworkQty)}</dd></div>
            </dl>
            <div className="mb-3 sm:max-w-md"><Progress label={t('mfg.opx.progressOf', { seq: op.operationSeq })} value={planned > 0 ? ((op.goodQty + op.scrapQty) / planned) * 100 : 0} /></div>
            {timeLogs.length > 0 && <TimeLogsTable logs={timeLogs} canWork={can.work} busy={log.isPending} onAction={(l, action) => log.mutate({ op: op.operationSeq, id: l.id, action })} />}
          </section>
        )
      })}

      <div>
        <h2 className="mb-2 text-sm font-semibold text-ink">{t('mfg.opx.reports')}</h2>
        {(reports.data ?? []).length === 0 ? <p className="text-sm text-ink-secondary">{t('mfg.opx.noReports')}</p> : (
          <TableBox caption={t('mfg.opx.reports')}>
            <thead className="bg-surface-muted"><tr>
              <th scope="col" className={th}>{t('mfg.doc.number')}</th><th scope="col" className={th}>{t('mfg.order.op')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.opx.good')}</th>
              <th scope="col" className={`${th} text-right`}>{t('mfg.opx.scrap')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.opx.rework')}</th><th scope="col" className={th}>{t('common.status')}</th>
              <th scope="col" className={th}>{t('mfg.opx.reportedAt')}</th><th scope="col" className={th}>{t('mfg.doc.problem')}</th>
              <th scope="col" className={th}><span className="sr-only">{t('common.actions')}</span></th>
            </tr></thead>
            <tbody className="divide-y divide-line">
              {(reports.data ?? []).map((r) => (
                <tr key={r.id}>
                  <td className={`${td} font-mono text-xs`}>
                    {r.docNo}
                    {r.reversalOfId && <span className="ml-2 font-sans text-ink-secondary">{t('mfg.rev.reversalOf', { doc: (reports.data ?? []).find((x) => x.id === r.reversalOfId)?.docNo ?? '' })}</span>}
                    {r.isReversed && <Badge variant="muted" className="ml-2">{t('mfg.rev.reversed')}</Badge>}
                  </td><td className={td}>{r.operationSeq}</td><td className={num}>{fmtQty(r.goodQty)}</td><td className={num}>{fmtQty(r.scrapQty)}</td><td className={num}>{fmtQty(r.reworkQty)}</td>
                  <td className={td}><DocStatusBadge status={r.status} /></td><td className={td}>{fmtDateTime(r.reportedAt)}</td><td className={`${td} text-danger-soft-text`}>{r.failureMessage ?? ''}</td>
                  <td className={td}>
                    {can.reverseReport && r.status === 'Posted' && !r.reversalOfId && !r.isReversed && (
                      <Button size="sm" variant="outline" onClick={() => { setReverseError(null); setReversing(r) }}>{t('mfg.rev.reverseReport')}</Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </TableBox>
        )}
      </div>

      {dialog?.kind === 'start' && <StartOperationDialog order={order} op={dialog.op} onClose={() => setDialog(null)} onDone={() => { setDialog(null); refresh() }} />}
      {dialog?.kind === 'manual' && <ManualTimeDialog order={order} op={dialog.op} onClose={() => setDialog(null)} onDone={() => { setDialog(null); refresh() }} />}
      {dialog?.kind === 'report' && <ReportOperationDialog order={order} op={dialog.op} onClose={() => setDialog(null)} onDone={() => { setDialog(null); refresh() }} />}
      <ReasonDialog
        open={!!reversing}
        danger
        title={t('mfg.rev.reportTitle', { doc: reversing?.docNo ?? '' })}
        message={reversing ? t('mfg.rev.reportMessage', { doc: reversing.docNo, seq: reversing.operationSeq, good: fmtQty(reversing.goodQty), scrap: fmtQty(reversing.scrapQty), rework: fmtQty(reversing.reworkQty) }) : ''}
        confirmLabel={t('mfg.rev.reverseReport')}
        busy={reverse.isPending}
        error={reverseError}
        onCancel={() => { setReversing(null); setReverseError(null) }}
        onConfirm={(note) => reversing && reverse.mutate({ report: reversing, note })}
      />
      <ConfirmDialog
        open={!!confirm}
        title={confirm?.kind === 'complete' ? t('mfg.opx.completeTitle') : t('mfg.opx.reopenTitle')}
        message={confirm ? t(confirm.kind === 'complete' ? 'mfg.opx.completeMessage' : 'mfg.opx.reopenMessage', { seq: confirm.op.operationSeq, name: confirm.op.name }) : ''}
        confirmLabel={confirm?.kind === 'complete' ? t('mfg.opx.complete') : t('mfg.opx.reopen')}
        onCancel={() => setConfirm(null)}
        onConfirm={() => confirm && manual.mutate(confirm)}
      />
    </div>
  )
}

function TimeLogsTable({ logs, canWork, busy, onAction }: { logs: TimeLog[]; canWork: boolean; busy: boolean; onAction: (l: TimeLog, a: 'pause' | 'resume' | 'stop') => void }) {
  const { t } = useTranslation()
  return (
    <TableBox caption={t('mfg.opx.timeLogs')}>
      <thead className="bg-surface-muted"><tr>
        <th scope="col" className={th}>{t('mfg.opx.employee')}</th><th scope="col" className={th}>{t('common.status')}</th><th scope="col" className={th}>{t('mfg.opx.from')}</th><th scope="col" className={th}>{t('mfg.opx.to')}</th>
        <th scope="col" className={`${th} text-right`}>{t('mfg.opx.minutes')}</th><th scope="col" className={th}><span className="sr-only">{t('common.actions')}</span></th>
      </tr></thead>
      <tbody className="divide-y divide-line">
        {logs.map((l) => (
          <tr key={l.id}>
            <td className={td}>{l.employeeNumber} {l.employeeName}{l.isManual && <Badge variant="muted" className="ml-1">{t('mfg.opx.manual')}</Badge>}</td>
            <td className={td}><DocStatusBadge status={l.state} /></td>
            <td className={td}>{fmtDateTime(l.startAt)}</td><td className={td}>{fmtDateTime(l.endAt)}</td>
            <td className={num}>{l.durationMinutes != null ? fmtQty(l.durationMinutes, 1) : ''}</td>
            <td className={td}>
              {canWork && l.state === 'Running' && <span className="flex gap-2"><Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(l, 'pause')}>{t('mfg.opx.pause')}</Button><Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(l, 'stop')}>{t('mfg.opx.stop')}</Button></span>}
              {canWork && l.state === 'Paused' && <span className="flex gap-2"><Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(l, 'resume')}>{t('mfg.opx.resume')}</Button><Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(l, 'stop')}>{t('mfg.opx.stop')}</Button></span>}
            </td>
          </tr>
        ))}
      </tbody>
    </TableBox>
  )
}

// ── Receipts ─────────────────────────────────────────────────────────────────────────────────────────────

export function ReceiptsTab({ order, can, documents, inspections = [], onReceive, reload }: { order: Order; can: OrderActions; documents: StockDocument[]; inspections?: InspectionListItem[]; onReceive: () => void; reload: () => void }) {
  const { t } = useTranslation()
  const receiptIds = new Set(documents.filter((d) => d.type === 'Receipt').map((d) => d.id))
  const receipts = documents.filter((d) => d.type === 'Receipt' || (d.type === 'Reversal' && d.reversalOfId != null && receiptIds.has(d.reversalOfId)))   // a reversal of a receipt sits with it
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        {can.receive && <Button onClick={onReceive}>{t('mfg.receipt.receive')}</Button>}
      </div>
      <TableBox caption={t('mfg.receipt.outputs')}>
        <thead className="bg-surface-muted"><tr>
          <th scope="col" className={th}>{t('mfg.order.item')}</th><th scope="col" className={th}>{t('mfg.receipt.kind')}</th>
          <th scope="col" className={`${th} text-right`}>{t('mfg.receipt.planned')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.receipt.received')}</th>
        </tr></thead>
        <tbody className="divide-y divide-line">
          <tr><td className={td}><span className="font-mono text-xs">{order.itemCode}</span> <span className="text-ink-secondary">{order.itemName}</span></td><td className={td}>{t('mfg.doc.mainProduct')}</td><td className={num}>{fmtQty(order.quantity)}</td><td className={num}>{fmtQty(order.completedQty)}</td></tr>
          {order.outputs.filter((o) => o.lineType !== 'Component').map((o) => (
            <tr key={o.lineNo}><td className={td}><span className="font-mono text-xs">{o.itemCode}</span> <span className="text-ink-secondary">{o.itemName}</span></td><td className={td}>{t(`mfg.receipt.lineType.${o.lineType}`)}</td><td className={num}>{fmtQty(o.plannedQty)}</td><td className={num}>{fmtQty(o.receivedQty)}</td></tr>
          ))}
        </tbody>
      </TableBox>
      <h2 className="text-sm font-semibold text-ink">{t('mfg.receipt.documents')}</h2>
      <StockDocumentsTable headingLevel={3} documents={receipts} inspections={inspections} canRetry={can.retryDocuments} canQcRelease={can.qcRelease} canReverse={can.reverseReceipt} onChanged={reload} />
    </div>
  )
}

// ── Documents ────────────────────────────────────────────────────────────────────────────────────────────

export function DocumentsTab({ documents, can, reload }: { documents: StockDocument[]; can: OrderActions; reload: () => void }) {
  return <StockDocumentsTable documents={documents} canRetry={can.retryDocuments} canQcRelease={can.qcRelease} canReverse={can.reverseReceipt} onChanged={reload} />
}

// ── History ──────────────────────────────────────────────────────────────────────────────────────────────

export function HistoryTab({ order }: { order: Order }) {
  const { t } = useTranslation()
  const history = useQuery({ queryKey: ['mfg', 'order', order.id, 'history'], queryFn: () => mfgApi.orderHistory(order.id) })
  const { data: reasons = [] } = useQuery({ queryKey: ['mfg', 'reasons'], queryFn: () => mfgApi.listReasons(), staleTime: 30_000 })
  if (history.isLoading) return <Skeleton className="h-24 w-full" />
  const rows = history.data ?? []
  if (rows.length === 0) return <p className="text-sm text-ink-secondary">{t('mfg.order.noHistory')}</p>
  return (
    <TableBox caption={t('mfg.order.history')}>
      <thead className="bg-surface-muted"><tr>
        <th scope="col" className={th}>{t('mfg.order.when')}</th><th scope="col" className={th}>{t('mfg.order.from')}</th><th scope="col" className={th}>{t('mfg.order.to')}</th>
        <th scope="col" className={th}>{t('mfg.reasonDialog.reason')}</th><th scope="col" className={th}>{t('mfg.reasonDialog.note')}</th><th scope="col" className={th}>{t('mfg.order.by')}</th>
      </tr></thead>
      <tbody className="divide-y divide-line">
        {rows.map((h, i) => (
          <tr key={`${h.changedAt}-${i}`}>
            <td className={td}>{fmtDateTime(h.changedAt)}</td><td className={td}>{t(`mfg.orderStatus.${h.fromStatus}`)}</td><td className={td}>{t(`mfg.orderStatus.${h.toStatus}`)}</td>
            <td className={td}>{reasons.find((r) => r.id === h.reasonCodeId)?.name ?? ''}</td><td className={td}>{h.note ?? ''}</td><td className={td}>{h.changedBy}</td>
          </tr>
        ))}
      </tbody>
    </TableBox>
  )
}

// ── Quality (P4-3) ───────────────────────────────────────────────────────────────────────────────────────

const inspectionVariant = { Pending: 'warning', Passed: 'success', Failed: 'danger', Cancelled: 'muted' } as const

/** The IPQC / FQC inspections of the order: what waits for a result, what passed and what failed (a failed one keeps its lot on hold or its output out of the next operation). */
export function QualityTab({ inspections }: { inspections: InspectionListItem[] }) {
  const { t } = useTranslation()
  const open = inspections.filter((i) => i.status === 'Pending' || (i.status === 'Failed' && !i.reinspectedById)).length
  if (inspections.length === 0) return <p className="text-sm text-ink-secondary">{t('mfg.qc.insp.noneForOrder')}</p>
  return (
    <div className="space-y-3">
      <p className="text-sm text-ink-secondary" role="status">{t('mfg.qc.insp.openCount', { count: open })}</p>
      <TableBox caption={t('mfg.qc.insp.forOrder')}>
        <thead className="bg-surface-muted"><tr>
          <th scope="col" className={th}>{t('mfg.qc.insp.docNo')}</th><th scope="col" className={th}>{t('mfg.qc.plan.type')}</th><th scope="col" className={th}>{t('mfg.qc.insp.lotOrOperation')}</th>
          <th scope="col" className={`${th} text-right`}>{t('mfg.qc.insp.lotQty')}</th><th scope="col" className={`${th} text-right`}>{t('mfg.qc.insp.sampleQty')}</th><th scope="col" className={th}>{t('common.status')}</th>
        </tr></thead>
        <tbody className="divide-y divide-line">
          {inspections.map((i) => (
            <tr key={i.id}>
              <td className={td}><Link className="font-mono text-xs text-brand-action hover:underline" to={`/manufacturing/inspections/${i.id}`}>{i.docNo}</Link></td>
              <td className={td}>{t(`mfg.qc.type.${i.type}`)}</td>
              <td className={td}><span className="font-mono text-xs">{i.lotNumber ?? (i.operationSeq != null ? t('mfg.qc.insp.operation', { seq: i.operationSeq }) : '')}</span></td>
              <td className={num}>{fmtQty(i.lotQty)}</td>
              <td className={num}>{fmtQty(i.sampleQty)}</td>
              <td className={td}><span className="flex items-center gap-2"><Badge variant={inspectionVariant[i.status]}>{t(`mfg.qc.insp.status.${i.status}`)}</Badge>{i.ncrRequired && <Badge variant="danger">{t('mfg.qc.insp.ncr')}</Badge>}</span></td>
            </tr>
          ))}
        </tbody>
      </TableBox>
    </div>
  )
}
