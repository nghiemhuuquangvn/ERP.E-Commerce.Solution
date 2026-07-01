import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { reportsApi } from '@/api/reports.api'
import { format, subDays } from 'date-fns'
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts'

// Module-level formatter — created once, never recreated
const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })

export function ReportsPage() {
  const { t } = useTranslation()
  const [from, setFrom] = useState(format(subDays(new Date(), 30), 'yyyy-MM-dd'))
  const [to, setTo] = useState(format(new Date(), 'yyyy-MM-dd'))

  const salesQuery = useQuery({
    queryKey: ['reports', 'sales', from, to],
    queryFn: () => reportsApi.sales({ from, to }),
    staleTime: 2 * 60_000, // Reports: fresh for 2 min (user-driven date range)
  })

  const payQuery = useQuery({
    queryKey: ['reports', 'payments', from, to],
    queryFn: () => reportsApi.payments({ from, to }),
    staleTime: 2 * 60_000,
  })

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <h1 className="text-xl font-semibold">{t('reports.title')}</h1>
        <div className="ml-auto flex items-center gap-2">
          <label className="text-sm text-gray-500">{t('reports.from')}</label>
          <input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="input w-auto"
          />
          <label className="text-sm text-gray-500">{t('reports.to')}</label>
          <input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="input w-auto"
          />
        </div>
      </div>

      <div className="grid grid-cols-3 gap-4">
        {[
          { label: t('reports.totalRevenue'), value: fmt.format(salesQuery.data?.totalRevenue ?? 0) },
          { label: t('reports.orders'), value: String(salesQuery.data?.totalOrders ?? 0) },
          { label: t('reports.avgOrderValue'), value: fmt.format(salesQuery.data?.averageOrderValue ?? 0) },
        ].map((kpi) => (
          <div key={kpi.label} className="card p-5">
            <p className="text-sm text-gray-500">{kpi.label}</p>
            <p className="mt-1 text-2xl font-semibold">
              {salesQuery.isLoading ? '—' : kpi.value}
            </p>
          </div>
        ))}
      </div>

      <div className="card p-5">
        <h2 className="mb-4 text-sm font-semibold text-gray-600">{t('reports.revenueByDay')}</h2>
        <ResponsiveContainer width="100%" height={240}>
          <BarChart data={salesQuery.data?.byDate ?? []}>
            <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
            <XAxis
              dataKey="date"
              tick={{ fontSize: 11 }}
              tickFormatter={(d) => format(new Date(d), 'MMM d')}
            />
            <YAxis
              tick={{ fontSize: 11 }}
              tickFormatter={(v) => `$${(v / 1000).toFixed(0)}k`}
            />
            <Tooltip formatter={(v: number) => fmt.format(v)} />
            <Legend />
            <Bar dataKey="revenue" name="Revenue" fill="#0284c7" radius={[3, 3, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="card p-5">
        <h2 className="mb-4 text-sm font-semibold text-gray-600">{t('reports.revenueByPaymentMethod')}</h2>
        {payQuery.isLoading ? (
          <div className="text-gray-400">{t('common.loading')}</div>
        ) : (
          <div className="space-y-2">
            {(payQuery.data?.byMethod ?? []).map((m) => (
              <div key={m.method} className="flex items-center gap-3">
                <span className="w-32 text-sm text-gray-600">{m.method}</span>
                <div className="flex-1 rounded-full bg-gray-100 h-3">
                  <div
                    className="h-3 rounded-full bg-primary-500"
                    style={{
                      width: `${payQuery.data ? (m.amount / payQuery.data.totalProcessed) * 100 : 0}%`,
                    }}
                  />
                </div>
                <span className="w-24 text-right text-sm font-medium">{fmt.format(m.amount)}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
