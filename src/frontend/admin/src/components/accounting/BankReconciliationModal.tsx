import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { bankAccountsApi } from '@/api/accounting/bank-accounts.api'
import type { BankAccount, BankReconciliation, BankStatement, BankStatementLine } from '@/api/accounting/bank-accounts.api'

const detail = (e: unknown, fallback: string) => {
  const data = (e as { response?: { data?: { detail?: string; message?: string; errors?: { rowNumber: number; message: string }[] } } } | null)?.response?.data
  if (data?.errors?.length) return data.errors.map((x) => (x.rowNumber ? `#${x.rowNumber}: ` : '') + x.message).join(' · ')
  return data?.detail ?? data?.message ?? fallback
}

const money = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 2 })

/** A statement against the books, line by line (ROADMAP P1-4): import the bank's CSV, match, post the fees, see the difference, reconcile. */
export function BankReconciliationModal({ account, statement, canManage, onClose }: { account: BankAccount; statement: BankStatement; canManage: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const [pickFor, setPickFor] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const key = ['accounting', 'bank-accounts', account.id, 'statements', statement.id, 'reconciliation']
  const { data, isLoading } = useQuery({ queryKey: key, queryFn: () => bankAccountsApi.reconciliation(account.id, statement.id), staleTime: 5_000 })
  const refresh = () => {
    setError(null)
    queryClient.invalidateQueries({ queryKey: ['accounting', 'bank-accounts', account.id] })
  }
  const fail = (e: unknown) => setError(detail(e, t('accounting.bankRecon.failed')))

  const importCsv = useMutation({ mutationFn: (csv: string) => bankAccountsApi.importLines(account.id, statement.id, csv), onSuccess: refresh, onError: fail })
  const autoMatch = useMutation({ mutationFn: () => bankAccountsApi.autoMatch(account.id, statement.id), onSuccess: refresh, onError: fail })
  const match = useMutation({
    mutationFn: ({ lineId, journalLineId }: { lineId: string; journalLineId: string }) => bankAccountsApi.matchLine(account.id, statement.id, lineId, journalLineId),
    onSuccess: () => {
      setPickFor(null)
      refresh()
    },
    onError: fail,
  })
  const unmatch = useMutation({ mutationFn: (lineId: string) => bankAccountsApi.unmatchLine(account.id, statement.id, lineId), onSuccess: refresh, onError: fail })
  const post = useMutation({ mutationFn: (lineId: string) => bankAccountsApi.postLine(account.id, statement.id, lineId), onSuccess: refresh, onError: fail })
  const remove = useMutation({ mutationFn: (lineId: string) => bankAccountsApi.removeLine(account.id, statement.id, lineId), onSuccess: refresh, onError: fail })
  const reconcile = useMutation({
    mutationFn: () => bankAccountsApi.reconcileStatement(account.id, statement.id),
    onSuccess: () => {
      refresh()
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

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[92vh] w-full max-w-5xl space-y-4 overflow-y-auto p-6">
        <h3 className="text-base font-semibold text-gray-900">
          {t('accounting.bankRecon.title')}: {account.accountName} · {new Date(statement.statementDate).toLocaleDateString()}
        </h3>

        {canManage && open && (
          <div className="flex flex-wrap items-center gap-3">
            <input ref={fileInput} type="file" accept=".csv,text/csv,text/plain" className="text-sm" onChange={(e) => onFile(e.target.files?.[0])} />
            <button className="btn-outline" disabled={autoMatch.isPending} onClick={() => autoMatch.mutate()}>
              {t('accounting.bankRecon.autoMatch')}
            </button>
          </div>
        )}
        <p className="text-xs text-gray-500">{t('accounting.bankRecon.csvHint')}</p>
        {error && <p className="text-xs text-red-600">{error}</p>}
        {isLoading && <p className="text-sm text-gray-400">{t('common.loading')}</p>}

        {r && (
          <>
            <div className="grid grid-cols-2 gap-2 rounded border border-gray-200 p-3 text-sm md:grid-cols-4">
              <div>
                <div className="text-xs text-gray-500">{t('accounting.bankRecon.closing')}</div>
                <b>{money(r.closingBalance)}</b>
              </div>
              <div>
                <div className="text-xs text-gray-500">{t('accounting.bankRecon.ledgerBalance')}</div>
                <b>{money(r.ledgerBalance)}</b>
              </div>
              <div>
                <div className="text-xs text-gray-500">{t('accounting.bankRecon.outstanding')}</div>
                <b>{money(r.outstandingLedgerNet)}</b>
              </div>
              <div>
                <div className="text-xs text-gray-500">{t('accounting.bankRecon.unmatchedStatement')}</div>
                <b>{money(r.unmatchedStatementNet)}</b>
              </div>
            </div>
            <p className={Math.abs(r.difference) < 0.005 ? 'text-sm text-green-700' : 'text-sm font-semibold text-red-600'}>
              {t('accounting.bankRecon.difference')}: {money(r.difference)}
              {r.unmatchedStatementLines > 0 && ` · ${t('accounting.bankRecon.unmatchedCount', { count: r.unmatchedStatementLines })}`}
            </p>
            {r.notes.map((n) => (
              <p key={n} className="text-xs text-amber-700">
                {n}
              </p>
            ))}

            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-xs uppercase text-gray-500">
                    <th className="py-1">{t('accounting.bankRecon.date')}</th>
                    <th>{t('accounting.bankRecon.description')}</th>
                    <th>{t('accounting.bankRecon.reference')}</th>
                    <th className="text-right">{t('accounting.bankRecon.amount')}</th>
                    <th>{t('common.status')}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {r.lines.map((l) => (
                    <tr key={l.id} className="border-b border-gray-100 align-top">
                      <td className="py-1">{new Date(l.lineDate).toLocaleDateString()}</td>
                      <td>{l.description}</td>
                      <td className="font-mono text-xs">{l.reference}</td>
                      <td className={`text-right ${l.amount < 0 ? 'text-red-600' : ''}`}>{money(l.amount)}</td>
                      <td>
                        {l.matchStatus}
                        {l.matchedEntryNumber ? ` · ${l.matchedEntryNumber}` : ''}
                      </td>
                      <td className="space-x-2 whitespace-nowrap text-xs">
                        {canManage && open && l.matchStatus === 'Unmatched' && (
                          <>
                            <button className="text-primary-600 hover:underline" onClick={() => setPickFor(pickFor === l.id ? null : l.id)}>
                              {t('accounting.bankRecon.match')}
                            </button>
                            <button className="text-primary-600 hover:underline" disabled={post.isPending} onClick={() => post.mutate(l.id)}>
                              {l.amount < 0 ? t('accounting.bankRecon.postFee') : t('accounting.bankRecon.postInterest')}
                            </button>
                            <button className="text-red-600 hover:underline" onClick={() => remove.mutate(l.id)}>
                              {t('common.delete')}
                            </button>
                          </>
                        )}
                        {canManage && open && l.matchStatus === 'Matched' && (
                          <button className="text-primary-600 hover:underline" onClick={() => unmatch.mutate(l.id)}>
                            {t('accounting.bankRecon.unmatch')}
                          </button>
                        )}
                        {pickFor === l.id && (
                          <div className="mt-1 rounded border border-gray-200 bg-gray-50 p-2">
                            {candidates(l).length === 0 && <p className="text-gray-500">{t('accounting.bankRecon.noCandidate')}</p>}
                            {candidates(l).map((c) => (
                              <button key={c.lineId} className="block text-left text-primary-700 hover:underline" onClick={() => match.mutate({ lineId: l.id, journalLineId: c.lineId })}>
                                {c.entryNumber} · {new Date(c.entryDate).toLocaleDateString()} · {c.description}
                              </button>
                            ))}
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                  {r.lines.length === 0 && (
                    <tr>
                      <td colSpan={6} className="py-3 text-center text-gray-400">
                        {t('accounting.bankRecon.noLines')}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {r.unmatchedLedger.length > 0 && (
              <div>
                <h4 className="mb-1 text-sm font-semibold text-gray-800">{t('accounting.bankRecon.outstandingTitle')}</h4>
                <ul className="space-y-0.5 text-xs text-gray-600">
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

        <div className="flex justify-end gap-3 pt-2">
          {canManage && open && (
            <button className="btn-primary" disabled={!r?.canReconcile || reconcile.isPending} onClick={() => reconcile.mutate()}>
              {t('accounting.bankAccounts.reconcile')}
            </button>
          )}
          <button className="btn-outline" onClick={onClose}>
            {t('common.close')}
          </button>
        </div>
      </div>
    </div>
  )
}
