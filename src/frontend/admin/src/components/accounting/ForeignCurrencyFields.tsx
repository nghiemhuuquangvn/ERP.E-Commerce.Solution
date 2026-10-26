import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fxApi } from '@/api/accounting/fx.api'

/** The currency, the amount in it and the rate of a foreign-currency document (ROADMAP P1-4). Empty currency = the base currency. */
export interface FxFields {
  currency: string
  amount: string
  rate: string
}

export const emptyFx = (): FxFields => ({ currency: '', amount: '', rate: '' })

/** The part of a create request that makes the document a foreign one; nothing for the base currency. */
export function toFxRequest(fx: FxFields): { currencyCode?: string; foreignAmount?: number; exchangeRate?: number } {
  if (!fx.currency.trim()) return {}
  return { currencyCode: fx.currency.trim().toUpperCase(), foreignAmount: Number(fx.amount) || 0, exchangeRate: Number(fx.rate) || 0 }
}

export const isForeign = (fx: FxFields) => fx.currency.trim().length > 0

export function ForeignCurrencyFields({ value, onChange, date }: { value: FxFields; onChange: (v: FxFields) => void; date?: string }) {
  const { t } = useTranslation()
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const suggest = async () => {
    setBusy(true)
    setMessage(null)
    try {
      const quote = await fxApi.rate(value.currency.trim().toUpperCase(), date)
      if (quote) onChange({ ...value, rate: String(quote.rate) })
      else setMessage(t('accounting.fx.noRate'))
    } catch {
      setMessage(t('accounting.fx.noRate'))
    } finally {
      setBusy(false)
    }
  }

  const base = (Number(value.amount) || 0) * (Number(value.rate) || 0)
  return (
    <div className="space-y-2 rounded border border-gray-200 p-3">
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.fx.currency')}</label>
          <input className="input w-24 uppercase" maxLength={3} placeholder={t('accounting.fx.baseCurrencyHint')} value={value.currency} onChange={(e) => onChange({ ...value, currency: e.target.value })} />
        </div>
        {isForeign(value) && (
          <>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.fx.foreignAmount')}</label>
              <input type="number" min="0" step="0.01" className="input w-36" value={value.amount} onChange={(e) => onChange({ ...value, amount: e.target.value })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.fx.rate')}</label>
              <input type="number" min="0" step="0.0001" className="input w-32" value={value.rate} onChange={(e) => onChange({ ...value, rate: e.target.value })} />
            </div>
            <button type="button" className="btn-outline" disabled={busy || value.currency.trim().length !== 3} onClick={suggest}>
              {t('accounting.fx.suggest')}
            </button>
          </>
        )}
      </div>
      {isForeign(value) && base > 0 && <p className="text-xs text-gray-600">{t('accounting.fx.baseEquivalent', { amount: base.toLocaleString() })}</p>}
      {message && <p className="text-xs text-amber-700">{message}</p>}
    </div>
  )
}
