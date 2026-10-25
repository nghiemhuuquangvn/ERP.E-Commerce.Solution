import { Fragment, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
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
  const [rows, setRows] = useState<Row[]>([{ dueDate: '', principalAmount: '', interestAmount: '0' }])

  const { data, isLoading } = useQuery({ queryKey: ['accounting', 'loans'], queryFn: () => loansApi.list(), staleTime: 15_000 })

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
        schedule: rows.map((r) => ({ dueDate: r.dueDate, principalAmount: Number(r.principalAmount), interestAmount: Number(r.interestAmount) || 0 })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setForm({ loanNumber: '', lender: '', principal: '', interestRatePercent: '0', startDate: '', notes: '' })
      setRows([{ dueDate: '', principalAmount: '', interestAmount: '0' }])
    },
  })

  const repay = useMutation({
    mutationFn: ({ loan, installmentId }: { loan: Loan; installmentId: string }) => loansApi.repay(loan.id, installmentId),
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
                    <td className="p-2">{loan.status === 'Cancelled' ? `${loan.status}: ${loan.cancelReason ?? ''}` : loan.status}</td>
                    <td className="p-2 text-right">
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
                                <td className="p-1">{i.paidOn ? i.paidOn.slice(0, 10) : ''}</td>
                                <td className="p-1 text-right">
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
