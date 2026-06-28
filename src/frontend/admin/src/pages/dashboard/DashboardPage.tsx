import { useQuery } from '@tanstack/react-query'
import { reportsApi } from '@/api/reports.api'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import { format, subDays } from 'date-fns'

// Module-level constants — computed once, never recreated on re-render
const fmt = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 0,
})

const today = new Date()
const QUERY_PARAMS = {
  from: format(subDays(today, 30), 'yyyy-MM-dd'),
  to: format(today, 'yyyy-MM-dd'),
}
// Stable primitive keys prevent spurious cache invalidation
const QUERY_KEY = ['reports', 'sales', QUERY_PARAMS.from, QUERY_PARAMS.to] as const

function KpiCard({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <div className="card p-5">
      <p className="text-sm text-gray-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-gray-900">{value}</p>
      {sub && <p className="mt-0.5 text-xs text-gray-400">{sub}</p>}
    </div>
  )
}

export function DashboardPage() {
  const { data, isLoading } = useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => reportsApi.sales(QUERY_PARAMS),
    staleTime: 5 * 60_000, // Dashboard KPIs: fresh for 5 min
  })

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold text-gray-900">Dashboard</h1>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <KpiCard
          label="Revenue (30d)"
          value={isLoading ? '—' : fmt.format(data?.totalRevenue ?? 0)}
        />
        <KpiCard
          label="Orders (30d)"
          value={isLoading ? '—' : String(data?.totalOrders ?? 0)}
        />
        <KpiCard
          label="Avg Order Value"
          value={isLoading ? '—' : fmt.format(data?.averageOrderValue ?? 0)}
        />
      </div>

      <div className="card p-5">
        <h2 className="mb-4 text-sm font-semibold text-gray-700">Revenue Last 30 Days</h2>
        {isLoading ? (
          <div className="flex h-48 items-center justify-center text-gray-400">
            Loading chart...
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={220}>
            <LineChart data={data?.byDate ?? []}>
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
              <Line
                type="monotone"
                dataKey="revenue"
                stroke="#0284c7"
                strokeWidth={2}
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  )
}
