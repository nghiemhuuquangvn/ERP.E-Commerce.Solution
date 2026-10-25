import { useTranslation } from 'react-i18next'

/** One VAT line as typed: category 0 = taxable at a rate, 1 = not subject to VAT (ROADMAP P1-3b). */
export interface VatLineRow {
  category: '0' | '1'
  rate: string
  amount: string
  description: string
}

export const emptyVatLine = (): VatLineRow => ({ category: '0', rate: '10', amount: '', description: '' })

/** The lines the API takes; rows without an amount are left out. */
export const toVatLineInputs = (rows: VatLineRow[]) =>
  rows
    .filter((r) => Number(r.amount) > 0)
    .map((r) => ({ category: Number(r.category), ratePercent: r.category === '1' ? 0 : Number(r.rate) || 0, amount: Number(r.amount), description: r.description || undefined }))

/** Rows of VAT lines: category, rate, amount (gross when prices include tax) and a description. */
export function VatLinesEditor({ rows, onChange }: { rows: VatLineRow[]; onChange: (rows: VatLineRow[]) => void }) {
  const { t } = useTranslation()
  const set = (i: number, patch: Partial<VatLineRow>) => onChange(rows.map((r, j) => (j === i ? { ...r, ...patch } : r)))
  return (
    <div className="space-y-2">
      {rows.map((r, i) => (
        <div key={i} className="flex flex-wrap items-center gap-2">
          <select className="input w-40" value={r.category} onChange={(e) => set(i, { category: e.target.value as '0' | '1' })}>
            <option value="0">{t('accounting.vat.taxable')}</option>
            <option value="1">{t('accounting.vat.notTaxable')}</option>
          </select>
          <input type="number" min="0" max="100" step="0.01" className="input w-20" disabled={r.category === '1'} placeholder="%" value={r.category === '1' ? '' : r.rate} onChange={(e) => set(i, { rate: e.target.value })} />
          <input type="number" min="0" step="0.01" className="input w-32" placeholder={t('accounting.vat.lineAmount')} value={r.amount} onChange={(e) => set(i, { amount: e.target.value })} />
          <input className="input w-40" placeholder={t('accounting.vat.lineDescription')} value={r.description} onChange={(e) => set(i, { description: e.target.value })} />
          {rows.length > 1 && (
            <button type="button" className="text-xs text-red-600 hover:underline" onClick={() => onChange(rows.filter((_, j) => j !== i))}>
              {t('common.delete')}
            </button>
          )}
        </div>
      ))}
      <button type="button" className="text-xs text-primary-700 hover:underline" onClick={() => onChange([...rows, emptyVatLine()])}>
        {t('accounting.vat.addLine')}
      </button>
    </div>
  )
}
