import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fiscalPeriodsApi } from '@/api/accounting/fiscal-periods.api'
import { fxApi } from '@/api/accounting/fx.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Button, FormField, Input, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

/** The period-end revaluation of open foreign receivables and payables: the figures at the closing rates and the button that posts them (ROADMAP P1-4). */
export function FxRevaluationPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [periodId, setPeriodId] = useState('')
  const [typed, setTyped] = useState<Record<string, string>>({})
  const [confirming, setConfirming] = useState(false)
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
    onSuccess: (posted) => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setConfirming(false)
      toast.success(t('accounting.fx.postedToast', { entry: posted.entryNumber ?? '—' }))
    },
    onError: (e) => {
      setConfirming(false)
      toast.error(serverMessage(e, t('accounting.fx.failed')))
    },
  })
  const r = preview.data
  const currencies = r ? Array.from(new Set([...Object.keys(r.rates), ...r.missingRates])) : []

  return (
    <div className="card space-y-3 p-4">
      <h2 className="text-sm font-semibold text-ink">{t('accounting.fx.revaluationTitle')}</h2>
      <p className="text-xs text-ink-secondary">{t('accounting.fx.revaluationHint')}</p>
      <FormField label={t('accounting.fiscalPeriods.periodName')} className="w-60">
        <Select
          value={periodId}
          onChange={(e) => {
            setPeriodId(e.target.value)
            setTyped({})
          }}
        >
          <option value="">{t('accounting.vat.pickPeriod')}</option>
          {(periods ?? []).map((p) => (
            <option key={p.id} value={p.id}>
              {p.periodName}
            </option>
          ))}
        </Select>
      </FormField>
      {r && (
        <div className="space-y-2 text-sm text-ink">
          {currencies.length > 0 && (
            <div className="flex flex-wrap items-end gap-3">
              {currencies.map((c) => (
                <FormField key={c} label={t('accounting.fx.closingRate', { currency: c, base: r.baseCurrency })}>
                  <Input
                    type="number"
                    step="0.0001"
                    className="w-32"
                    placeholder={r.rates[c] ? String(r.rates[c]) : ''}
                    value={typed[c] ?? ''}
                    onChange={(e) => setTyped({ ...typed, [c]: e.target.value })}
                  />
                </FormField>
              ))}
            </div>
          )}
          {r.lines.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <caption className="sr-only">{t('accounting.fx.revaluationTitle')}</caption>
                <thead>
                  <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
                    <th scope="col" className="py-1">{t('accounting.fx.document')}</th>
                    <th scope="col">{t('accounting.fx.currency')}</th>
                    <th scope="col" className="text-right">{t('accounting.fx.foreignOutstanding')}</th>
                    <th scope="col" className="text-right">{t('accounting.fx.carrying')}</th>
                    <th scope="col" className="text-right">{t('accounting.fx.revalued')}</th>
                    <th scope="col" className="text-right">{t('accounting.fx.difference')}</th>
                  </tr>
                </thead>
                <tbody>
                  {r.lines.map((l) => (
                    <tr key={l.documentId} className="border-b border-line">
                      <td className="py-1">
                        {l.documentType} {l.number}
                      </td>
                      <td>{l.currency}</td>
                      <td className="text-right">{l.foreignOutstanding.toLocaleString()}</td>
                      <td className="text-right">{l.carryingAmount.toLocaleString()}</td>
                      <td className="text-right">{l.revaluedAmount.toLocaleString()}</td>
                      <td className={`text-right ${l.difference < 0 ? 'text-danger-soft-text' : 'text-success-soft-text'}`}>{l.difference.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p>
            {t('accounting.fx.receivables')}: <b>{r.receivablesDifference.toLocaleString()}</b> · {t('accounting.fx.payables')}: <b>{r.payablesDifference.toLocaleString()}</b> · {t('accounting.fx.net')}: <b>{r.netDifference.toLocaleString()}</b>
          </p>
          {r.posted && <Alert variant="success">{t('accounting.fx.postedBy', { entry: r.entryNumber, reversal: r.reversalEntryNumber ?? '—' })}</Alert>}
          {r.notes.map((n) => (
            <Alert key={n} variant="warning">
              {n}
            </Alert>
          ))}
          {canManage && !r.posted && r.lines.length > 0 && r.missingRates.length === 0 && (
            <Button onClick={() => setConfirming(true)}>{t('accounting.fx.revalue')}</Button>
          )}
        </div>
      )}
      <ConfirmDialog
        open={confirming}
        title={t('accounting.fx.confirmTitle')}
        message={t('accounting.fx.confirmMessage', { period: r?.periodName ?? '', net: (r?.netDifference ?? 0).toLocaleString(), date: r?.asOfDate ? new Date(r.asOfDate).toLocaleDateString() : '' })}
        confirmLabel={revalue.isPending ? t('common.saving') : t('accounting.fx.revalue')}
        onConfirm={() => !revalue.isPending && revalue.mutate()}
        onCancel={() => setConfirming(false)}
      />
    </div>
  )
}
