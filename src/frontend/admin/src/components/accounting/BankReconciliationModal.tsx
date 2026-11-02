import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { bankAccountsApi } from '@/api/accounting/bank-accounts.api'
import type { BankAccount, BankReconciliation, BankStatement, BankStatementLine } from '@/api/accounting/bank-accounts.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Badge, Button, Modal, useToast } from '@/components/ui'

/** A CSV import answers with a list of rows that were refused; any other refusal has a single text. */
const detail = (e: unknown, fallback: string) => {
  const data = (e as { response?: { data?: { detail?: string; message?: string; errors?: { rowNumber: number; message: string }[] } } } | null)?.response?.data
  if (data?.errors?.length) return data.errors.map((x) => (x.rowNumber ? `#${x.rowNumber}: ` : '') + x.message).join(' · ')
  return data?.detail ?? data?.message ?? fallback
}

const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 })

/** What the person is asked to confirm: books a journal entry, removes a line, or locks the statement. */
type Step = { kind: 'post' | 'delete'; line: BankStatementLine } | { kind: 'reconcile' } | null

/** A statement against the books, line by line (ROADMAP P1-4): import the bank's CSV, match, post the fees, see the difference, reconcile. */
export function BankReconciliationModal({ account, statement, canManage, onClose }: { account: BankAccount; statement: BankStatement; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const fileInput = useRef<HTMLInputElement>(null)
  const [pickFor, setPickFor] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [step, setStep] = useState<Step>(null)

  const key = ['accounting', 'bank-accounts', account.id, 'statements', statement.id, 'reconciliation']
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: () => bankAccountsApi.reconciliation(account.id, statement.id), staleTime: 5_000 })
  const refresh = () => {
    setError(null)
    queryClient.invalidateQueries({ queryKey: ['accounting', 'bank-accounts', account.id] })
  }
  const fail = (e: unknown) => {
    setStep(null)
    setError(detail(e, t('accounting.bankRecon.failed')))
  }

  const importCsv = useMutation({
    mutationFn: (csv: string) => bankAccountsApi.importLines(account.id, statement.id, csv),
    onSuccess: () => {
      refresh()
      toast.success(t('accounting.bankRecon.imported'))
    },
    onError: fail,
  })
  const autoMatch = useMutation({
    mutationFn: () => bankAccountsApi.autoMatch(account.id, statement.id),
    onSuccess: () => {
      refresh()
      toast.success(t('accounting.bankRecon.matchedDone'))
    },
    onError: fail,
  })
  const match = useMutation({
    mutationFn: ({ lineId, journalLineId }: { lineId: string; journalLineId: string }) => bankAccountsApi.matchLine(account.id, statement.id, lineId, journalLineId),
    onSuccess: () => {
      setPickFor(null)
      refresh()
    },
    onError: fail,
  })
  const unmatch = useMutation({ mutationFn: (lineId: string) => bankAccountsApi.unmatchLine(account.id, statement.id, lineId), onSuccess: refresh, onError: fail })
  const post = useMutation({
    mutationFn: (lineId: string) => bankAccountsApi.postLine(account.id, statement.id, lineId),
    onSuccess: () => {
      setStep(null)
      refresh()
      toast.success(t('common.saved'))
    },
    onError: fail,
  })
  const remove = useMutation({
    mutationFn: (lineId: string) => bankAccountsApi.removeLine(account.id, statement.id, lineId),
    onSuccess: () => {
      setStep(null)
      refresh()
      toast.success(t('common.saved'))
    },
    onError: fail,
  })
  const reconcile = useMutation({
    mutationFn: () => bankAccountsApi.reconcileStatement(account.id, statement.id),
    onSuccess: () => {
      refresh()
      toast.success(t('accounting.bankRecon.reconciled'))
      onClose()
    },
    onError: fail,
  })

  const onFile = async (file: File | undefined) => {
    if (!file) return
    importCsv.mutate(await file.text())
    if (fileInput.current) fileInput.current.value = ''
  }

  const open = statement.status === 'Open'
  const r: BankReconciliation | undefined = data
  const candidates = (line: BankStatementLine) => (r?.unmatchedLedger ?? []).filter((l) => l.amount === line.amount)
  const ready = !!r && Math.abs(r.difference) < 0.005
  const busy = post.isPending || remove.isPending || reconcile.isPending

  const confirmText = () => {
    if (!step) return { title: '', message: '', label: '', danger: false }
    if (step.kind === 'reconcile') return { title: t('accounting.bankAccounts.reconcileTitle'), message: t('accounting.bankRecon.reconcileConfirm'), label: t('accounting.bankAccounts.reconcile'), danger: false }
    const values = { description: step.line.description, amount: money(step.line.amount) }
    if (step.kind === 'delete') return { title: t('accounting.bankRecon.deleteTitle'), message: t('accounting.bankRecon.deleteConfirm', values), label: t('common.delete'), danger: true }
    const fee = step.line.amount < 0
    return {
      title: t(fee ? 'accounting.bankRecon.postFeeTitle' : 'accounting.bankRecon.postInterestTitle'),
      message: t(fee ? 'accounting.bankRecon.postFeeConfirm' : 'accounting.bankRecon.postInterestConfirm', values),
      label: t(fee ? 'accounting.bankRecon.postFee' : 'accounting.bankRecon.postInterest'),
      danger: false,
    }
  }
  const ask = confirmText()
  const onConfirm = () => {
    if (!step || busy) return
    if (step.kind === 'post') post.mutate(step.line.id)
    else if (step.kind === 'delete') remove.mutate(step.line.id)
    else reconcile.mutate()
  }

  return (
    <>
      <Modal
        open
        size="lg"
        title={`${t('accounting.bankRecon.title')}: ${account.accountName} · ${new Date(statement.statementDate).toLocaleDateString()}`}
        onClose={onClose}
        closeOnEsc={step === null}
        footer={
          <>
            {canManage && open && (
              <Button disabled={!r?.canReconcile} loading={reconcile.isPending} onClick={() => setStep({ kind: 'reconcile' })}>
                {t('accounting.bankAccounts.reconcile')}
              </Button>
            )}
            <Button variant="outline" onClick={onClose}>
              {t('common.close')}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {canManage && open && (
            <div className="flex flex-wrap items-center gap-3">
              <label className="flex items-center gap-2 text-sm text-ink">
                <span className="font-medium">{t('accounting.bankRecon.fileLabel')}</span>
                <input ref={fileInput} type="file" accept=".csv,text/csv,text/plain" className="text-sm" onChange={(e) => onFile(e.target.files?.[0])} />
              </label>
              <Button variant="outline" loading={autoMatch.isPending} onClick={() => autoMatch.mutate()}>
                {t('accounting.bankRecon.autoMatch')}
              </Button>
            </div>
          )}
          <p className="text-xs text-ink-secondary">{t('accounting.bankRecon.csvHint')}</p>
          {error && <Alert variant="danger">{error}</Alert>}
          {isLoading && <p className="text-sm text-ink-muted">{t('common.loading')}</p>}

          {r && (
            <>
              <dl className="grid grid-cols-2 gap-2 rounded-md border border-line p-3 text-sm md:grid-cols-4">
                <div>
                  <dt className="text-xs text-ink-secondary">{t('accounting.bankRecon.closing')}</dt>
                  <dd className="font-semibold">{money(r.closingBalance)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-ink-secondary">{t('accounting.bankRecon.ledgerBalance')}</dt>
                  <dd className="font-semibold">{money(r.ledgerBalance)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-ink-secondary">{t('accounting.bankRecon.outstanding')}</dt>
                  <dd className="font-semibold">{money(r.outstandingLedgerNet)}</dd>
                </div>
                <div>
                  <dt className="text-xs text-ink-secondary">{t('accounting.bankRecon.unmatchedStatement')}</dt>
                  <dd className="font-semibold">{money(r.unmatchedStatementNet)}</dd>
                </div>
              </dl>
              <p role="status" className={ready ? 'text-sm text-success-soft-text' : 'text-sm font-semibold text-danger-soft-text'}>
                {ready ? t('accounting.bankRecon.differenceOk') : t('accounting.bankRecon.differenceOpen', { amount: money(r.difference) })}
                {r.unmatchedStatementLines > 0 && ` · ${t('accounting.bankRecon.unmatchedCount', { count: r.unmatchedStatementLines })}`}
              </p>
              {r.notes.map((n) => (
                <Alert key={n} variant="warning">
                  {n}
                </Alert>
              ))}

              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
                      <th className="py-1">{t('accounting.bankRecon.date')}</th>
                      <th>{t('accounting.bankRecon.description')}</th>
                      <th>{t('accounting.bankRecon.reference')}</th>
                      <th className="text-right">{t('accounting.bankRecon.amount')}</th>
                      <th>{t('common.status')}</th>
                      <th>
                        <span className="sr-only">{t('common.actions')}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {r.lines.map((l) => (
                      <tr key={l.id} className="border-b border-line align-top">
                        <td className="py-1">{new Date(l.lineDate).toLocaleDateString()}</td>
                        <td>{l.description}</td>
                        <td className="font-mono text-xs">{l.reference}</td>
                        <td className={`text-right ${l.amount < 0 ? 'text-danger-soft-text' : ''}`}>{money(l.amount)}</td>
                        <td>
                          <Badge variant={l.matchStatus === 'Unmatched' ? 'warning' : 'success'}>{l.matchStatus}</Badge>
                          {l.matchedEntryNumber ? ` · ${l.matchedEntryNumber}` : ''}
                        </td>
                        <td className="space-x-2 whitespace-nowrap text-xs">
                          {canManage && open && l.matchStatus === 'Unmatched' && (
                            <>
                              <Button variant="link" className="text-xs" aria-label={`${t('accounting.bankRecon.match')} ${l.description}`} aria-expanded={pickFor === l.id} onClick={() => setPickFor(pickFor === l.id ? null : l.id)}>
                                {t('accounting.bankRecon.match')}
                              </Button>
                              <Button variant="link" className="text-xs" aria-label={`${l.amount < 0 ? t('accounting.bankRecon.postFee') : t('accounting.bankRecon.postInterest')} ${l.description}`} onClick={() => setStep({ kind: 'post', line: l })}>
                                {l.amount < 0 ? t('accounting.bankRecon.postFee') : t('accounting.bankRecon.postInterest')}
                              </Button>
                              <Button variant="link-danger" className="text-xs" aria-label={`${t('common.delete')} ${l.description}`} onClick={() => setStep({ kind: 'delete', line: l })}>
                                {t('common.delete')}
                              </Button>
                            </>
                          )}
                          {canManage && open && l.matchStatus === 'Matched' && (
                            <Button variant="link" className="text-xs" aria-label={`${t('accounting.bankRecon.unmatch')} ${l.description}`} loading={unmatch.isPending && unmatch.variables === l.id} onClick={() => unmatch.mutate(l.id)}>
                              {t('accounting.bankRecon.unmatch')}
                            </Button>
                          )}
                          {pickFor === l.id && (
                            <div role="group" aria-label={t('accounting.bankRecon.candidatesFor')} className="mt-1 rounded-md border border-line bg-surface-muted p-2">
                              {candidates(l).length === 0 && <p className="text-ink-secondary">{t('accounting.bankRecon.noCandidate')}</p>}
                              {candidates(l).map((c) => (
                                <Button
                                  key={c.lineId}
                                  variant="link"
                                  className="block text-left text-xs"
                                  disabled={match.isPending}
                                  onClick={() => match.mutate({ lineId: l.id, journalLineId: c.lineId })}
                                >
                                  {c.entryNumber} · {new Date(c.entryDate).toLocaleDateString()} · {c.description}
                                </Button>
                              ))}
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                    {r.lines.length === 0 && (
                      <tr>
                        <td colSpan={6} className="py-3 text-center text-ink-muted">
                          {t('accounting.bankRecon.noLines')}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {r.unmatchedLedger.length > 0 && (
                <div>
                  <h4 className="mb-1 text-sm font-semibold text-ink">{t('accounting.bankRecon.outstandingTitle')}</h4>
                  <ul className="space-y-0.5 text-xs text-ink-secondary">
                    {r.unmatchedLedger.map((l) => (
                      <li key={l.lineId}>
                        {new Date(l.entryDate).toLocaleDateString()} · {l.entryNumber} · {l.description} · <b>{money(l.amount)}</b>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}
        </div>
      </Modal>

      <ConfirmDialog open={step !== null} title={ask.title} message={ask.message} confirmLabel={ask.label} danger={ask.danger} onConfirm={onConfirm} onCancel={() => setStep(null)} />
    </>
  )
}
