import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { colors } from '@/styles/tokens'
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

  const inventoryQuery = useQuery({
    queryKey: ['reports', 'inventory'],
    queryFn: () => reportsApi.inventory(),
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

      <div className="grid grid-cols-4 gap-4">
        {[
          { label: t('reports.totalRevenue'), value: fmt.format(salesQuery.data?.totalRevenue ?? 0) },
          { label: t('reports.orders'), value: String(salesQuery.data?.totalOrders ?? 0) },
          { label: t('reports.avgOrderValue'), value: fmt.format(salesQuery.data?.averageOrderValue ?? 0) },
          { label: t('reports.newCustomers'), value: String(salesQuery.data?.newCustomers ?? 0) },
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
          <BarChart data={salesQuery.data?.revenueByDay ?? []}>
            <CartesianGrid strokeDasharray="3 3" stroke={colors.line} />
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
            <Bar dataKey="revenue" name="Revenue" fill={colors.brandAccent} radius={[3, 3, 0, 0]} />
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

      <div className="grid grid-cols-2 gap-4">
        <div className="card p-5">
          <h2 className="mb-4 text-sm font-semibold text-gray-600">{t('reports.topProducts')}</h2>
          {salesQuery.isLoading ? (
            <div className="text-gray-400">{t('common.loading')}</div>
          ) : (salesQuery.data?.topProducts ?? []).length === 0 ? (
            <div className="text-sm text-gray-400">{t('reports.noData')}</div>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-gray-500">
                  <th className="pb-2 font-medium">{t('reports.product')}</th>
                  <th className="pb-2 font-medium text-right">{t('reports.unitsSold')}</th>
                  <th className="pb-2 font-medium text-right">{t('reports.revenue')}</th>
                </tr>
              </thead>
              <tbody>
                {(salesQuery.data?.topProducts ?? []).map((p) => (
                  <tr key={p.productId} className="border-t border-gray-100">
                    <td className="py-2 truncate max-w-[200px]">{p.productName}</td>
                    <td className="py-2 text-right">{p.unitsSold}</td>
                    <td className="py-2 text-right font-medium">{fmt.format(p.revenue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="card p-5">
          <h2 className="mb-4 text-sm font-semibold text-gray-600">{t('reports.inventory')}</h2>
          {inventoryQuery.isLoading ? (
            <div className="text-gray-400">{t('common.loading')}</div>
          ) : (
            <>
              <div className="mb-4 grid grid-cols-3 gap-3">
                <div>
                  <p className="text-xs text-gray-500">{t('reports.totalProducts')}</p>
                  <p className="text-lg font-semibold">{inventoryQuery.data?.totalProducts ?? 0}</p>
                </div>
                <div>
                  <p className="text-xs text-amber-600">{t('reports.lowStock')}</p>
                  <p className="text-lg font-semibold text-amber-600">{inventoryQuery.data?.lowStockCount ?? 0}</p>
                </div>
                <div>
                  <p className="text-xs text-red-600">{t('reports.outOfStock')}</p>
                  <p className="text-lg font-semibold text-red-600">{inventoryQuery.data?.outOfStockCount ?? 0}</p>
                </div>
              </div>
              <div className="max-h-48 overflow-y-auto">
                {(inventoryQuery.data?.stockLevels ?? [])
                  .filter((s) => s.stockStatus !== 'InStock')
                  .slice(0, 10)
                  .map((s) => (
                    <div key={s.productId} className="flex items-center justify-between border-t border-gray-100 py-2 text-sm">
                      <span className="truncate max-w-[220px]">{s.productName}</span>
                      <span
                        className={
                          s.stockStatus === 'OutOfStock'
                            ? 'text-red-600 font-medium'
                            : 'text-amber-600 font-medium'
                        }
                      >
                        {s.availableQuantity} {t('reports.units')}
                      </span>
                    </div>
                  ))}
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
