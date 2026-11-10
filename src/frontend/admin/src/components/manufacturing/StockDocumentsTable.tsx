import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type StockDocument, type StockDocumentLine } from '@/api/manufacturing/mfg.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Badge, Button, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { DocStatusBadge, DocTypeLabel } from './OrderBadges'
import { fmtDate, fmtDateTime, fmtQty } from './shared'

function LineTable({ doc }: { doc: StockDocument }) {
  const { t } = useTranslation()
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-xs">
        <caption className="sr-only">{t('mfg.doc.linesOf', { doc: doc.docNo })}</caption>
        <thead>
          <tr className="border-b border-line text-ink-secondary">
            <th scope="col" className="py-1 pr-3">#</th>
            <th scope="col" className="py-1 pr-3">{t('mfg.doc.item')}</th>
            <th scope="col" className="py-1 pr-3 text-right">{t('mfg.doc.qty')}</th>
            <th scope="col" className="py-1 pr-3">{t('mfg.doc.lot')}</th>
            <th scope="col" className="py-1 pr-3">{t('mfg.doc.expiry')}</th>
            <th scope="col" className="py-1 pr-3 text-right">{t('mfg.doc.cost')}</th>
            <th scope="col" className="py-1 pr-3">{t('common.status')}</th>
            <th scope="col" className="py-1">{t('mfg.doc.problem')}</th>
          </tr>
        </thead>
        <tbody>
          {doc.lines.map((l: StockDocumentLine) => (
            <tr key={l.lineNo} className="border-b border-line align-top">
              <td className="py-1 pr-3 font-mono">{l.lineNo}</td>
              <td className="py-1 pr-3 font-mono">
                {l.itemCode}
                {l.kind === 'Receipt' && <span className="ml-1 text-ink-secondary">({l.outputLineNo == null ? t('mfg.doc.mainProduct') : t('mfg.doc.output', { n: l.outputLineNo })})</span>}
              </td>
              <td className="py-1 pr-3 text-right tabular-nums">{fmtQty(l.qty)}</td>
              <td className="py-1 pr-3 font-mono">
                {l.lotNumber ?? ''}
                {l.isQcHold && <Badge variant="warning" className="ml-1">{t('mfg.doc.onHold')}</Badge>}
                {l.qcReleasedAt && <Badge variant="success" className="ml-1">{t('mfg.doc.released')}</Badge>}
                {l.isNegativeStock && <Badge variant="danger" className="ml-1">{t('mfg.doc.negative')}</Badge>}
              </td>
              <td className="py-1 pr-3">{fmtDate(l.expiryDate)}</td>
              <td className="py-1 pr-3 text-right tabular-nums">{l.unitCost != null ? fmtQty(l.unitCost, 4) : ''}</td>
              <td className="py-1 pr-3"><DocStatusBadge status={l.status} /></td>
              <td className="py-1 text-danger-soft-text">{l.error ?? ''}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

interface StockDocumentsTableProps {
  documents: StockDocument[]
  canRetry: boolean
  /** Shows the order number as a link (the plant-wide list); on the order's own tab it is redundant. */
  showOrder?: boolean
  /** The quality-release button of a posted receipt that still has held lots (needs `qc:disposition`). */
  canQcRelease?: boolean
  /** Level of each document heading (2 under the page title; 3 when the table sits under a section heading). */
  headingLevel?: 2 | 3
  onChanged?: () => void
}

/** Stock documents with their lines. A Pending document is still on its way to the Warehouse (not an error); a Failed one shows why. */
export function StockDocumentsTable({ documents, canRetry, showOrder = false, canQcRelease = false, headingLevel = 2, onChanged }: StockDocumentsTableProps) {
  const Heading = `h${headingLevel}` as 'h2' | 'h3'
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [abandon, setAbandon] = useState<StockDocument | null>(null)
  const [release, setRelease] = useState<StockDocument | null>(null)
  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'order'] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'documents'] }); onChanged?.() }
  const retry = useMutation({
    mutationFn: (id: string) => mfgApi.retryDocument(id),
    onSuccess: (r) => { refresh(); r.pending ? toast.info(t('mfg.doc.stillPending')) : toast.success(t('mfg.doc.posted')) },
    onError: (e) => toast.error(serverMessage(e, t('mfg.doc.retryFailed'))),
  })
  const abandonIt = useMutation({
    mutationFn: (id: string) => mfgApi.abandonDocument(id),
    onSuccess: () => { setAbandon(null); refresh(); toast.success(t('mfg.doc.abandoned')) },
    onError: (e) => { setAbandon(null); toast.error(serverMessage(e, t('mfg.doc.retryFailed'))) },
  })
  const qc = useMutation({
    mutationFn: (id: string) => mfgApi.qcRelease(id),
    onSuccess: () => { setRelease(null); refresh(); toast.success(t('mfg.doc.releasedOk')) },
    onError: (e) => { setRelease(null); toast.error(serverMessage(e, t('mfg.doc.retryFailed'))) },
  })

  if (documents.length === 0) return <p className="text-sm text-ink-secondary">{t('mfg.doc.empty')}</p>
  return (
    <div className="space-y-4">
      {documents.map((d) => {
        const heldLines = d.type === 'Receipt' && d.status === 'Posted' && d.lines.some((l) => l.kind === 'Receipt' && l.isQcHold && !l.qcReleasedAt)
        return (
          <section key={d.id} aria-label={d.docNo} className="rounded-lg border border-line bg-surface p-3">
            <div className="mb-2 flex flex-wrap items-center gap-3">
              <Heading className="font-mono text-sm font-semibold text-ink">{d.docNo}</Heading>
              <DocTypeLabel type={d.type} />
              <DocStatusBadge status={d.status} />
              {d.mode === 'Rollback' && <Badge variant="warning">{t('mfg.doc.rollback')}</Badge>}
              {showOrder && <Link className="text-sm text-brand-action hover:underline" to={`/manufacturing/orders/${d.orderId}`}>{d.orderNo}</Link>}
              <span className="text-xs text-ink-secondary">{fmtDateTime(d.postedAt ?? d.createdAt)}{d.createdBy ? ` · ${d.createdBy}` : ''}</span>
              <div className="ml-auto flex flex-wrap gap-2">
                {d.status === 'Pending' && canRetry && (
                  <>
                    <Button size="sm" variant="secondary" loading={retry.isPending && retry.variables === d.id} onClick={() => retry.mutate(d.id)}>{t('mfg.doc.retry')}</Button>
                    <Button size="sm" variant="outline" onClick={() => setAbandon(d)}>{t('mfg.doc.abandon')}</Button>
                  </>
                )}
                {heldLines && canQcRelease && <Button size="sm" variant="secondary" onClick={() => setRelease(d)}>{t('mfg.doc.qcRelease')}</Button>}
              </div>
            </div>
            {d.status === 'Pending' && (
              <Alert variant="info" className="mb-2">
                {t('mfg.doc.pendingHint', { attempts: d.attempts })} {d.nextAttemptAt ? t('mfg.doc.nextAttempt', { at: fmtDateTime(d.nextAttemptAt) }) : ''} {d.lastError ?? ''}
              </Alert>
            )}
            {d.status === 'Failed' && <Alert variant="danger" className="mb-2">{d.lastErrorCode ? `${d.lastErrorCode}: ` : ''}{d.lastError ?? t('mfg.doc.failed')}</Alert>}
            <LineTable doc={d} />
          </section>
        )
      })}
      <ConfirmDialog open={!!abandon} danger title={t('mfg.doc.abandonTitle')} message={t('mfg.doc.abandonMessage', { doc: abandon?.docNo ?? '' })} confirmLabel={t('mfg.doc.abandon')} onCancel={() => setAbandon(null)} onConfirm={() => abandon && abandonIt.mutate(abandon.id)} />
      <ConfirmDialog open={!!release} title={t('mfg.doc.qcReleaseTitle')} message={t('mfg.doc.qcReleaseMessage', { doc: release?.docNo ?? '' })} confirmLabel={t('mfg.doc.qcRelease')} onCancel={() => setRelease(null)} onConfirm={() => release && qc.mutate(release.id)} />
    </div>
  )
}
