import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { posApi } from '@/api/pos/pos.api'
import { TERMINALS_KEY } from './PosTerminalsPage'

const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`
const today = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10)

/** End-of-day (Z) report of one terminal, or of every terminal. */
export function PosZReportPage() {
  const { t } = useTranslation()
  const [date, setDate] = useState(today())
  const [terminalId, setTerminalId] = useState('')

  const terminals = useQuery({ queryKey: TERMINALS_KEY, queryFn: ({ signal }) => posApi.terminals(undefined, signal), staleTime: 60_000, retry: false })
  const { data, isLoading, isError } = useQuery({
    queryKey: ['pos', 'zReport', date, terminalId],
    queryFn: () => posApi.zReport(date, terminalId || undefined),
    enabled: !!date,
  })

  return (
    <div className="max-w-3xl space-y-4">
      <h1 className="text-xl font-semibold">{t('pos.zReport.title')}</h1>

      <div className="flex flex-wrap items-end gap-3">
        <input type="date" className="input w-44" value={date} max={today()} onChange={(e) => setDate(e.target.value)} />
        {terminals.data && (
          <select className="input w-52" value={terminalId} onChange={(e) => setTerminalId(e.target.value)}>
            <option value="">{t('pos.allTerminals')}</option>
            {terminals.data.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        )}
        <button className="btn-outline" onClick={() => window.print()}>{t('pos.zReport.print')}</button>
      </div>

      {isLoading && <p className="text-gray-400">{t('common.loading')}</p>}
      {isError && <p className="text-sm text-red-600">{t('pos.zReport.failed')}</p>}

      {data && (
        <div className="card space-y-4 p-4">
          <dl className="grid grid-cols-2 gap-3 text-sm md:grid-cols-3">
            <div><dt className="text-gray-500">{t('pos.zReport.shifts')}</dt><dd className="text-lg font-semibold">{data.shiftCount}</dd></div>
            <div><dt className="text-gray-500">{t('pos.shifts.completed')}</dt><dd className="text-lg font-semibold">{data.completedTransactionCount}</dd></div>
            <div><dt className="text-gray-500">{t('pos.shifts.voided')}</dt><dd className="text-lg font-semibold">{data.voidedTransactionCount}</dd></div>
            <div><dt className="text-gray-500">{t('pos.shifts.totalSales')}</dt><dd className="text-lg font-semibold">{money(data.totalSales)}</dd></div>
            <div><dt className="text-gray-500">{t('pos.transactions.discount')}</dt><dd className="text-lg font-semibold">{money(data.totalDiscount)}</dd></div>
            <div><dt className="text-gray-500">{t('pos.transactions.tax')}</dt><dd className="text-lg font-semibold">{money(data.totalTax)}</dd></div>
          </dl>

          <div>
            <h2 className="mb-1 text-xs font-semibold uppercase text-gray-500">{t('pos.shifts.salesByMethod')}</h2>
            {Object.keys(data.salesByPaymentMethod).length === 0 ? (
              <p className="text-sm text-gray-400">{t('common.noRecordsFound')}</p>
            ) : (
              <table className="text-sm">
                <tbody>
                  {Object.entries(data.salesByPaymentMethod).map(([method, amount]) => (
                    <tr key={method}><td className="pr-8">{method}</td><td>{money(amount)}</td></tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
