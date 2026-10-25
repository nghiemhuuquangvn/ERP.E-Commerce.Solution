import { Fragment, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { accountsApi } from '@/api/accounting/accounts.api'
import { fiscalPeriodsApi } from '@/api/accounting/fiscal-periods.api'
import { loansApi } from '@/api/accounting/loans.api'
import type { Loan } from '@/api/accounting/loans.api'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { usePermission } from '@/hooks/usePermission'

const detail = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ?? fallback

interface Row { dueDate: string; principalAmount: string; interestAmount: string }

/** The loan ledger: borrowings with their repayment schedule (B01 splits them into short- and long-term by the due dates). */
export function LoansPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('accounting:bank:manage')
  const queryClient = useQueryClient()

  const [open, setOpen] = useState<string | null>(null)
  const [cancelTarget, setCancelTarget] = useState<Loan | null>(null)
  const [form, setForm] = useState({ loanNumber: '', lender: '', principal: '', interestRatePercent: '0', startDate: '', notes: '' })
  const [postDrawdown, setPostDrawdown] = useState(true)
  const [receivingAccountId, setReceivingAccountId] = useState('')
  const [dayCountBasis, setDayCountBasis] = useState('0')
  const [interestPaid, setInterestPaid] = useState<Record<string, string>>({})
  const [accrueDate, setAccrueDate] = useState(new Date().toISOString().slice(0, 10))
  const [asOf, setAsOf] = useState(new Date().toISOString().slice(0, 10))
  const [rows, setRows] = useState<Row[]>([{ dueDate: '', principalAmount: '', interestAmount: '0' }])

  const { data, isLoading } = useQuery({ queryKey: ['accounting', 'loans'], queryFn: () => loansApi.list(), staleTime: 15_000 })

  const { data: policy } = useQuery({ queryKey: ['accounting', 'closingPolicy'], queryFn: () => fiscalPeriodsApi.closingPolicy(), staleTime: 30_000 })
  const { data: allAccounts } = useQuery({ queryKey: ['accounting', 'accounts', 'list'], queryFn: () => accountsApi.list(), staleTime: 60_000 })
  // bank and cash accounts: the postable assets (leaf, active) — a parent groups sub-accounts and takes no postings
  const assetAccounts = (allAccounts ?? []).filter((a) => a.accountType === 'Asset' && a.isActive && !(allAccounts ?? []).some((c) => c.parentAccountId === a.id))
  const { data: recon } = useQuery({ queryKey: ['accounting', 'loans', 'reconciliation', asOf], queryFn: () => loansApi.reconciliation(asOf), enabled: !!asOf, staleTime: 15_000 })

  const scheduleTotal = rows.reduce((sum, r) => sum + (Number(r.principalAmount) || 0), 0)
  const scheduleOk = rows.every((r) => r.dueDate && Number(r.principalAmount) > 0) && Math.abs(scheduleTotal - Number(form.principal)) < 0.01

  const create = useMutation({
    mutationFn: () =>
      loansApi.create({
        loanNumber: form.loanNumber,
        lender: form.lender,
        principal: Number(form.principal),
        interestRatePercent: Number(form.interestRatePercent) || 0,
        startDate: form.startDate,
        notes: form.notes || undefined,
        postDrawdown,
        receivingAccountId: receivingAccountId || undefined,
        dayCountBasis: Number(dayCountBasis),
        schedule: rows.map((r) => ({ dueDate: r.dueDate, principalAmount: Number(r.principalAmount), interestAmount: Number(r.interestAmount) || 0 })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setForm({ loanNumber: '', lender: '', principal: '', interestRatePercent: '0', startDate: '', notes: '' })
      setRows([{ dueDate: '', principalAmount: '', interestAmount: '0' }])
      setDayCountBasis('0')
    },
  })

  const post = useMutation({
    mutationFn: (loan: Loan) => loansApi.postDrawdown(loan.id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting'] }),
  })

  const repay = useMutation({
    mutationFn: ({ loan, installmentId }: { loan: Loan; installmentId: string }) => {
      const paid = interestPaid[installmentId]
      return loansApi.repay(loan.id, installmentId, undefined, paid !== undefined && paid !== '' ? Number(paid) : undefined)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting'] }),
  })

  const accrue = useMutation({
    mutationFn: () => loansApi.accrueInterest(accrueDate),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting'] }),
  })

  const cancel = useMutation({
    mutationFn: ({ loan, reason }: { loan: Loan; reason: string }) => loansApi.cancel(loan.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setCancelTarget(null)
    },
  })

  const setRow = (i: number, patch: Partial<Row>) => setRows(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.loans.title')}</h1>
      <p className="text-sm text-gray-500">{t('accounting.loans.hint')}</p>

      {canManage && (
        <div className="card space-y-3 p-4">
          <h2 className="text-sm font-semibold text-gray-900">{t('accounting.loans.add')}</h2>
          <div className="grid gap-3 md:grid-cols-3">
            {(['loanNumber', 'lender'] as const).map((k) => (
              <div key={k}>
                <label className="mb-1 block text-sm font-medium text-gray-700">{t(`accounting.loans.${k}`)}</label>
                <input className="input" value={form[k]} onChange={(e) => setForm({ ...form, [k]: e.target.value })} />
              </div>
            ))}
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.startDate')}</label>
              <input type="date" className="input" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.principal')}</label>
              <input type="number" min="0" className="input" value={form.principal} onChange={(e) => setForm({ ...form, principal: e.target.value })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.rate')}</label>
              <input type="number" min="0" className="input" value={form.interestRatePercent} onChange={(e) => setForm({ ...form, interestRatePercent: e.target.value })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.notes')}</label>
              <input className="input" value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
            </div>
          </div>

          <p className="text-sm font-medium text-gray-700">{t('accounting.loans.schedule')}</p>
          {rows.map((r, i) => (
            <div key={i} className="flex flex-wrap items-end gap-3">
              <input type="date" className="input" value={r.dueDate} onChange={(e) => setRow(i, { dueDate: e.target.value })} />
              <input type="number" min="0" className="input" placeholder={t('accounting.loans.principal')} value={r.principalAmount} onChange={(e) => setRow(i, { principalAmount: e.target.value })} />
              <input type="number" min="0" className="input" placeholder={t('accounting.loans.interest')} value={r.interestAmount} onChange={(e) => setRow(i, { interestAmount: e.target.value })} />
              {rows.length > 1 && (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setRows(rows.filter((_, j) => j !== i))}>
                  {t('common.delete')}
                </button>
              )}
            </div>
          ))}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.receivingAccount')}</label>
            <select className="input" value={receivingAccountId} onChange={(e) => setReceivingAccountId(e.target.value)}>
              <option value="">{t('accounting.loans.receivingDefault')}</option>
              {assetAccounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.accountNumber} — {a.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.dayCount')}</label>
            <select className="input" value={dayCountBasis} onChange={(e) => setDayCountBasis(e.target.value)}>
              <option value="0">{t('accounting.loans.dayCountSchedule')}</option>
              <option value="1">{t('accounting.loans.dayCount365')}</option>
              <option value="2">{t('accounting.loans.dayCount360')}</option>
            </select>
            <p className="mt-1 text-xs text-gray-500">{t('accounting.loans.dayCountHint')}</p>
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" checked={postDrawdown} onChange={(e) => setPostDrawdown(e.target.checked)} />
            {t('accounting.loans.postDrawdown')}
          </label>
          <div className="flex flex-wrap items-center gap-3">
            <button className="btn-outline" onClick={() => setRows([...rows, { dueDate: '', principalAmount: '', interestAmount: '0' }])}>
              {t('accounting.loans.addInstallment')}
            </button>
            <span className={scheduleOk ? 'text-sm text-green-700' : 'text-sm text-amber-700'}>
              {t('accounting.loans.scheduleTotal', { total: scheduleTotal.toLocaleString(), principal: (Number(form.principal) || 0).toLocaleString() })}
            </span>
            <button className="btn-primary" disabled={!form.loanNumber || !form.lender || !form.startDate || !scheduleOk || create.isPending} onClick={() => create.mutate()}>
              {t('common.save')}
            </button>
            {create.isError && <span className="text-sm text-red-600">{detail(create.error, t('accounting.loans.failed'))}</span>}
          </div>
        </div>
      )}

      <div className="card space-y-2 p-4">
        <div className="flex flex-wrap items-end gap-3">
          <h2 className="text-sm font-semibold text-gray-900">{t('accounting.loans.reconciliation')}</h2>
          <input type="date" className="input" value={asOf} onChange={(e) => setAsOf(e.target.value)} />
        </div>
        {recon && (
          <div className="text-sm text-gray-700">
            <p>
              {t('accounting.loans.ledgerTotal')}: <b>{recon.ledgerTotal.toLocaleString()}</b> · {t('accounting.loans.bookBalance')} ({recon.accountNumber || '—'}):{' '}
              <b>{recon.bookBalance.toLocaleString()}</b> · {t('accounting.loans.difference')}:{' '}
              <b className={recon.isReconciled ? 'text-green-700' : 'text-red-600'}>{recon.difference.toLocaleString()}</b>
            </p>
            {recon.interestAccountNumber && (
              <p>
                {t('accounting.loans.interestCheck')} ({recon.interestAccountNumber}): {recon.interestLedger.toLocaleString()} / {recon.interestBook.toLocaleString()} ·{' '}
                <b className={recon.interestReconciled ? 'text-green-700' : 'text-red-600'}>{recon.interestDifference.toLocaleString()}</b>
              </p>
            )}
            {recon.notes.map((n) => (
              <p key={n} className="text-xs text-amber-700">
                {n}
              </p>
            ))}
          </div>
        )}
      </div>

      {canManage && (
        <div className="card flex flex-wrap items-end gap-3 p-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.loans.accrueUpTo')}</label>
            <input type="date" className="input" value={accrueDate} onChange={(e) => setAccrueDate(e.target.value)} />
          </div>
          <button className="btn-outline" disabled={!accrueDate || accrue.isPending} onClick={() => accrue.mutate()}>
            {t('accounting.loans.accrue')}
          </button>
          {accrue.isSuccess && (
            <span className="text-sm text-green-700">{t('accounting.loans.accrued', { total: accrue.data.totalAccrued.toLocaleString(), count: accrue.data.lines.filter((l) => l.posted).length })}</span>
          )}
          {accrue.isError && <span className="text-sm text-red-600">{detail(accrue.error, t('accounting.loans.failed'))}</span>}
          <span className="text-xs text-gray-500">{policy?.autoAccrueLoanInterest ? t('accounting.loans.autoAccrueOn') : t('accounting.loans.autoAccrueOff')}</span>
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-gray-400">{t('common.loading')}</p>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-left text-xs uppercase text-gray-500">
                <th className="p-2">{t('accounting.loans.loanNumber')}</th>
                <th className="p-2">{t('accounting.loans.lender')}</th>
                <th className="p-2 text-right">{t('accounting.loans.principal')}</th>
                <th className="p-2 text-right">{t('accounting.loans.outstanding')}</th>
                <th className="p-2 text-right">{t('accounting.loans.within12')}</th>
                <th className="p-2 text-right">{t('accounting.loans.later')}</th>
                <th className="p-2">{t('common.status')}</th>
                <th className="p-2" />
              </tr>
            </thead>
            <tbody>
              {(data ?? []).map((loan) => (
                <Fragment key={loan.id}>
                  <tr className="border-b hover:bg-gray-50">
                    <td className="p-2">
                      <button className="text-primary-700 hover:underline" onClick={() => setOpen(open === loan.id ? null : loan.id)}>
                        {loan.loanNumber}
                      </button>
                    </td>
                    <td className="p-2">{loan.lender}</td>
                    <td className="p-2 text-right">{loan.principal.toLocaleString()}</td>
                    <td className="p-2 text-right">{loan.outstanding.toLocaleString()}</td>
                    <td className="p-2 text-right">{loan.dueWithinTwelveMonths.toLocaleString()}</td>
                    <td className="p-2 text-right">{loan.dueLater.toLocaleString()}</td>
                    <td className="p-2">
                      {loan.status === 'Cancelled' ? `${loan.status}: ${loan.cancelReason ?? ''}` : loan.status}
                      {loan.receivingAccountNumber && <span className="ml-2 text-xs text-gray-500">{loan.receivingAccountNumber}</span>}
                      {loan.dayCountBasis !== 0 && <span className="ml-2 text-xs text-gray-500">{loan.dayCountBasis === 1 ? '365' : '360'}</span>}
                      {loan.status !== 'Cancelled' && (
                        <span className={loan.postedToLedger ? 'ml-2 text-xs text-green-700' : 'ml-2 text-xs text-gray-400'}>
                          {loan.postedToLedger ? t('accounting.loans.posted') : t('accounting.loans.notPosted')}
                        </span>
                      )}
                    </td>
                    <td className="p-2 text-right">
                      {canManage && loan.status === 'Active' && !loan.postedToLedger && !loan.installments.some((i) => i.paidOn) && (
                        <button className="mr-3 text-xs text-blue-600 hover:underline" disabled={post.isPending} onClick={() => post.mutate(loan)}>
                          {t('accounting.loans.postNow')}
                        </button>
                      )}
                      {canManage && loan.status === 'Active' && !loan.installments.some((i) => i.paidOn) && (
                        <button className="text-xs text-red-600 hover:underline" onClick={() => setCancelTarget(loan)}>
                          {t('accounting.loans.cancel')}
                        </button>
                      )}
                    </td>
                  </tr>
                  {open === loan.id && (
                    <tr className="border-b bg-gray-50">
                      <td colSpan={8} className="p-3">
                        <table className="w-full text-xs">
                          <thead>
                            <tr className="text-left text-gray-500">
                              <th className="p-1">{t('accounting.loans.dueDate')}</th>
                              <th className="p-1 text-right">{t('accounting.loans.principal')}</th>
                              <th className="p-1 text-right">{t('accounting.loans.interest')}</th>
                              <th className="p-1 text-right">{t('accounting.loans.accruedInterest')}</th>
                              <th className="p-1">{t('accounting.loans.paidOn')}</th>
                              <th className="p-1" />
                            </tr>
                          </thead>
                          <tbody>
                            {loan.installments.map((i) => (
                              <tr key={i.id}>
                                <td className="p-1">{i.dueDate.slice(0, 10)}</td>
                                <td className="p-1 text-right">{i.principalAmount.toLocaleString()}</td>
                                <td className="p-1 text-right">{i.interestAmount.toLocaleString()}</td>
                                <td className="p-1 text-right">{i.accruedInterest ? i.accruedInterest.toLocaleString() : ''}</td>
                                <td className="p-1">{i.paidOn ? i.paidOn.slice(0, 10) : ''}</td>
                                <td className="p-1 text-right">
                                  {canManage && loan.status === 'Active' && !i.paidOn && (
                                    <input
                                      type="number"
                                      min="0"
                                      className="input mr-2 w-28"
                                      placeholder={loan.dayCountBasis !== 0 ? i.interestDueToday.toLocaleString() : t('accounting.loans.interestPaid')}
                                      value={interestPaid[i.id] ?? ''}
                                      onChange={(e) => setInterestPaid({ ...interestPaid, [i.id]: e.target.value })}
                                    />
                                  )}
                                  {canManage && loan.status === 'Active' && !i.paidOn && (
                                    <button className="text-primary-700 hover:underline" disabled={repay.isPending} onClick={() => repay.mutate({ loan, installmentId: i.id })}>
                                      {t('accounting.loans.repay')}
                                    </button>
                                  )}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                        {repay.isError && <p className="mt-2 text-xs text-red-600">{detail(repay.error, t('accounting.loans.failed'))}</p>}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ReasonDialog
        open={!!cancelTarget}
        title={t('accounting.loans.cancelTitle')}
        message={cancelTarget ? `${cancelTarget.loanNumber} · ${cancelTarget.lender}` : undefined}
        confirmLabel={t('accounting.loans.cancel')}
        danger
        busy={cancel.isPending}
        error={cancel.isError ? detail(cancel.error, t('accounting.loans.failed')) : null}
        onConfirm={(reason) => cancelTarget && cancel.mutate({ loan: cancelTarget, reason })}
        onCancel={() => setCancelTarget(null)}
      />
    </div>
  )
}
