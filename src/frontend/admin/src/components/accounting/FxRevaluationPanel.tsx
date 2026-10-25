import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fiscalPeriodsApi } from '@/api/accounting/fiscal-periods.api'
import { fxApi } from '@/api/accounting/fx.api'

const detail = (e: unknown, fallback: string) => (e as { response?: { data?: { detail?: string; message?: string } } } | null)?.response?.data?.detail ?? fallback

/** The period-end revaluation of open foreign receivables and payables: the figures at the closing rates and the button that posts them (ROADMAP P1-4). */
export function FxRevaluationPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [periodId, setPeriodId] = useState('')
  const [typed, setTyped] = useState<Record<string, string>>({})
  const rates = Object.fromEntries(Object.entries(typed).filter(([, v]) => Number(v) > 0).map(([k, v]) => [k, Number(v)]))

  const { data: periods } = useQuery({ queryKey: ['accounting', 'fiscal-periods'], queryFn: () => fiscalPeriodsApi.list(), staleTime: 30_000 })
  const preview = useQuery({
    queryKey: ['accounting', 'fx', 'revaluation', periodId, JSON.stringify(rates)],
    queryFn: () => fxApi.previewRevaluation(periodId, rates),
    enabled: !!periodId,
    staleTime: 10_000,
  })
  const revalue = useMutation({
    mutationFn: () => fxApi.revalue(periodId, rates),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting'] }),
  })
  const r = preview.data
  const currencies = r ? Array.from(new Set([...Object.keys(r.rates), ...r.missingRates])) : []

  return (
    <div className="card space-y-3 p-3">
      <h3 className="text-sm font-semibold text-gray-900">{t('accounting.fx.revaluationTitle')}</h3>
      <p className="text-xs text-gray-500">{t('accounting.fx.revaluationHint')}</p>
      <select className="input w-60" value={periodId} onChange={(e) => { setPeriodId(e.target.value); setTyped({}) }}>
        <option value="">{t('accounting.vat.pickPeriod')}</option>
        {(periods ?? []).map((p) => (
          <option key={p.id} value={p.id}>
            {p.periodName}
          </option>
        ))}
      </select>
      {r && (
        <div className="space-y-2 text-sm text-gray-700">
          {currencies.length > 0 && (
            <div className="flex flex-wrap items-end gap-3">
              {currencies.map((c) => (
                <label key={c} className="text-xs text-gray-600">
                  {t('accounting.fx.closingRate', { currency: c, base: r.baseCurrency })}
                  <input
                    type="number"
                    step="0.0001"
                    className="input mt-1 w-32"
                    placeholder={r.rates[c] ? String(r.rates[c]) : ''}
                    value={typed[c] ?? ''}
                    onChange={(e) => setTyped({ ...typed, [c]: e.target.value })}
                  />
                </label>
              ))}
            </div>
          )}
          {r.lines.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-xs uppercase text-gray-500">
                    <th className="py-1">{t('accounting.fx.document')}</th>
                    <th>{t('accounting.fx.currency')}</th>
                    <th className="text-right">{t('accounting.fx.foreignOutstanding')}</th>
                    <th className="text-right">{t('accounting.fx.carrying')}</th>
                    <th className="text-right">{t('accounting.fx.revalued')}</th>
                    <th className="text-right">{t('accounting.fx.difference')}</th>
                  </tr>
                </thead>
                <tbody>
                  {r.lines.map((l) => (
                    <tr key={l.documentId} className="border-b border-gray-100">
                      <td className="py-1">
                        {l.documentType} {l.number}
                      </td>
                      <td>{l.currency}</td>
                      <td className="text-right">{l.foreignOutstanding.toLocaleString()}</td>
                      <td className="text-right">{l.carryingAmount.toLocaleString()}</td>
                      <td className="text-right">{l.revaluedAmount.toLocaleString()}</td>
                      <td className={`text-right ${l.difference < 0 ? 'text-red-600' : 'text-green-700'}`}>{l.difference.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p>
            {t('accounting.fx.receivables')}: <b>{r.receivablesDifference.toLocaleString()}</b> · {t('accounting.fx.payables')}: <b>{r.payablesDifference.toLocaleString()}</b> · {t('accounting.fx.net')}: <b>{r.netDifference.toLocaleString()}</b>
          </p>
          {r.posted && <p className="text-green-700">{t('accounting.fx.postedBy', { entry: r.entryNumber, reversal: r.reversalEntryNumber ?? '—' })}</p>}
          {r.notes.map((n) => (
            <p key={n} className="text-xs text-amber-700">
              {n}
            </p>
          ))}
          {canManage && !r.posted && r.lines.length > 0 && r.missingRates.length === 0 && (
            <button className="btn-primary" disabled={revalue.isPending} onClick={() => revalue.mutate()}>
              {t('accounting.fx.revalue')}
            </button>
          )}
          {revalue.isError && <p className="text-xs text-red-600">{detail(revalue.error, t('accounting.fx.failed'))}</p>}
        </div>
      )}
    </div>
  )
}
