import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { colors } from '@/styles/tokens'
import { CheckCircle2 } from 'lucide-react'
import { format, formatDistanceToNow } from 'date-fns'
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts'
import {
  monitoringApi,
  ServiceHealth,
  ExceptionLog,
  ApiRequestLog,
  MonitoringDashboard,
  RouteStats,
} from '@/api/monitoring.api'
import { usePermission } from '@/hooks/usePermission'
import { Link, Navigate } from 'react-router-dom'

// Deep-links a Monitoring row into System Logs, scoped to Gateway (RequestId only has
// meaning within Gateway's own logs — it's Kestrel's per-request TraceIdentifier, not a
// cross-service correlation id) and to the request's exact RequestId when available.
function logsLinkFor(requestId?: string, level: 'Warning' | 'Error' = 'Warning') {
  const params = new URLSearchParams({ serviceName: 'Gateway', level })
  if (requestId) params.set('requestId', requestId)
  return `/logs?${params.toString()}`
}

export function MonitoringPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()

  if (!hasPermission('monitoring:view')) {
    return <Navigate to="/dashboard" replace />
  }

  // Single shared query — child cards receive data as props (no duplicate subscriptions)
  const { data: dashboard, isLoading: dashLoading } = useQuery({
    queryKey: ['monitoring', 'dashboard'],
    queryFn: monitoringApi.getDashboard,
    refetchInterval: 15_000,
    staleTime: 10_000,
  })

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{t('monitoring.title')}</h1>
          <p className="text-sm text-gray-500">{t('monitoring.subtitle')}</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <ProcessMetricsCard process={dashboard?.process} isLoading={dashLoading} />
        <ServicesHealthCard />
      </div>

      <ApiStatsCard api={dashboard?.api} isLoading={dashLoading} />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <RecentRequestsCard />
        <ExceptionsCard />
      </div>
    </div>
  )
}

// ── Process Metrics ────────────────────────────────────────────────────────
function ProcessMetricsCard({
  process,
  isLoading,
}: {
  process: MonitoringDashboard['process'] | undefined
  isLoading: boolean
}) {
  const { t } = useTranslation()
  const formatUptime = (seconds: number) => {
    const h = Math.floor(seconds / 3600)
    const m = Math.floor((seconds % 3600) / 60)
    return `${h}h ${m}m`
  }

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">{t('monitoring.gatewayProcess')}</h2>
      {isLoading ? (
        <div className="animate-pulse space-y-3">
          {[...Array(6)].map((_, i) => (
            <div key={i} className="h-5 bg-gray-100 rounded" />
          ))}
        </div>
      ) : process ? (
        <dl className="grid grid-cols-2 gap-3 text-sm">
          <MetricItem label="Machine" value={process.machineName} />
          <MetricItem label="Process" value={process.processName} />
          <MetricItem label="Uptime" value={formatUptime(process.uptimeSeconds)} />
          <MetricItem label=".NET Version" value={process.dotNetVersion} />
          <MetricItem label="Working Set" value={`${process.workingSetMb} MB`} />
          <MetricItem label="GC Memory" value={`${process.totalMemoryMb} MB`} />
          <MetricItem label="Threads" value={String(process.threadCount)} />
          <MetricItem label="Handles" value={String(process.handleCount)} />
          <div className="col-span-2 pt-2 border-t">
            <p className="text-xs text-gray-500">GC Collections</p>
            <div className="flex gap-4 mt-1">
              <span className="text-xs">
                Gen0: <strong>{process.gcGen0Collections}</strong>
              </span>
              <span className="text-xs">
                Gen1: <strong>{process.gcGen1Collections}</strong>
              </span>
              <span className="text-xs">
                Gen2: <strong>{process.gcGen2Collections}</strong>
              </span>
            </div>
          </div>
        </dl>
      ) : (
        <p className="text-sm text-gray-500">{t('monitoring.noData')}</p>
      )}
    </div>
  )
}

// ── Service Health ─────────────────────────────────────────────────────────
function ServicesHealthCard() {
  const { t } = useTranslation()
  const { data, isLoading, refetch } = useQuery({
    queryKey: ['monitoring', 'services'],
    queryFn: monitoringApi.getServicesHealth,
    refetchInterval: 30_000,
    staleTime: 20_000,
  })

  const statusColor: Record<string, string> = {
    Healthy: 'bg-green-100 text-green-800',
    Degraded: 'bg-yellow-100 text-yellow-800',
    Unreachable: 'bg-red-100 text-red-800',
  }

  const statusDot: Record<string, string> = {
    Healthy: 'bg-green-400',
    Degraded: 'bg-yellow-400',
    Unreachable: 'bg-red-400',
  }

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-900">{t('monitoring.servicesHealth')}</h2>
        <button onClick={() => refetch()} className="text-xs text-blue-600 hover:underline">
          {t('monitoring.refresh')}
        </button>
      </div>
      {isLoading ? (
        <div className="space-y-2">
          {[...Array(8)].map((_, i) => (
            <div key={i} className="h-8 bg-gray-100 rounded animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="space-y-2">
          {(data || []).map((svc: ServiceHealth) => (
            <div key={svc.serviceName} className="flex items-center justify-between py-1.5">
              <div className="flex items-center gap-2">
                <span
                  className={`w-2 h-2 rounded-full ${statusDot[svc.status] ?? 'bg-gray-400'}`}
                />
                <span className="text-sm font-medium">{svc.serviceName}</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="text-xs text-gray-400">{svc.responseTimeMs}ms</span>
                <span
                  className={`text-xs px-2 py-0.5 rounded-full font-medium ${statusColor[svc.status] ?? 'bg-gray-100 text-gray-700'}`}
                >
                  {svc.status}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// ── API Stats (bar chart by route) ────────────────────────────────────────
function ApiStatsCard({
  api,
  isLoading,
}: {
  api: MonitoringDashboard['api'] | undefined
  isLoading: boolean
}) {
  const { t } = useTranslation()
  const topRoutes: RouteStats[] = api?.topRoutes?.slice(0, 8) ?? []

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-900">{t('monitoring.apiTraffic')}</h2>
        {api && !isLoading && (
          <div className="flex gap-6 text-sm">
            <span className="text-gray-600">
              Total: <strong>{api.totalRequests.toLocaleString()}</strong>
            </span>
            <span className="text-red-600">
              Errors: <strong>{api.totalErrors.toLocaleString()}</strong>
            </span>
            <span className={api.errorRate > 5 ? 'text-red-600' : 'text-gray-600'}>
              Error Rate: <strong>{api.errorRate}%</strong>
            </span>
          </div>
        )}
      </div>
      <ResponsiveContainer width="100%" height={220}>
        <BarChart data={topRoutes}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis
            dataKey="pathPattern"
            tick={{ fontSize: 10 }}
            tickFormatter={(v: string) => (v.length > 30 ? v.slice(0, 28) + '…' : v)}
          />
          <YAxis tick={{ fontSize: 11 }} />
          <Tooltip
            formatter={(value: number, name: string) => [
              name === 'count' ? value.toLocaleString() : `${value}ms`,
              name === 'count' ? 'Requests' : 'Avg Response',
            ]}
          />
          <Bar dataKey="count" fill={colors.brandAccent} name="count" radius={[3, 3, 0, 0]} />
          <Bar dataKey="avgElapsedMs" fill={colors.success} name="avgElapsedMs" radius={[3, 3, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

// ── Recent Requests ────────────────────────────────────────────────────────
function RecentRequestsCard() {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['monitoring', 'requests'],
    queryFn: monitoringApi.getRecentRequests,
    refetchInterval: 10_000,
    staleTime: 8_000,
  })

  const statusClass = (code: number) =>
    code >= 500
      ? 'text-red-600 font-semibold'
      : code >= 400
        ? 'text-yellow-600 font-semibold'
        : 'text-green-600'

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">{t('monitoring.recentRequests')}</h2>
      {isLoading ? (
        <div className="space-y-2">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-8 bg-gray-100 rounded animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="overflow-auto max-h-72">
          <table className="min-w-full text-xs">
            <thead>
              <tr className="border-b text-gray-500 text-left">
                <th className="pb-2 pr-2">Time</th>
                <th className="pb-2 pr-2">Method</th>
                <th className="pb-2 pr-2">Path</th>
                <th className="pb-2 pr-2">Status</th>
                <th className="pb-2 pr-2">ms</th>
                <th className="pb-2"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {(data ?? []).map((req: ApiRequestLog, i) => (
                // timestamp+index as key: requests have no unique id, timestamp alone may collide in bursts
                <tr key={`${req.timestamp}-${i}`} className="hover:bg-gray-50">
                  <td className="py-1.5 pr-2 text-gray-400 whitespace-nowrap">
                    {format(new Date(req.timestamp), 'HH:mm:ss')}
                  </td>
                  <td className="pr-2">
                    <span className="font-mono bg-gray-100 px-1 rounded text-gray-700">
                      {req.method}
                    </span>
                  </td>
                  <td className="pr-2 max-w-[200px] truncate text-gray-700" title={req.path}>
                    {req.path}
                  </td>
                  <td className={`pr-2 ${statusClass(req.statusCode)}`}>{req.statusCode}</td>
                  <td className="text-gray-500 pr-2">{Math.round(req.elapsedMs)}</td>
                  <td>
                    {req.statusCode >= 400 && (
                      <Link
                        to={logsLinkFor(req.requestId, req.statusCode >= 500 ? 'Error' : 'Warning')}
                        className="text-primary-600 hover:underline whitespace-nowrap"
                      >
                        {t('monitoring.viewLogs')}
                      </Link>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// ── Exceptions ─────────────────────────────────────────────────────────────
function ExceptionsCard() {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['monitoring', 'exceptions'],
    queryFn: monitoringApi.getExceptions,
    refetchInterval: 15_000,
    staleTime: 10_000,
  })

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-lg font-semibold text-gray-900">{t('monitoring.recentExceptions')}</h2>
        {data && data.length > 0 && (
          <span className="bg-red-100 text-red-700 text-xs font-semibold px-2 py-0.5 rounded-full">
            {data.length}
          </span>
        )}
      </div>
      {isLoading ? (
        <div className="space-y-2">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-16 bg-gray-100 rounded animate-pulse" />
          ))}
        </div>
      ) : !data || data.length === 0 ? (
        <div className="flex flex-col items-center justify-center h-32 text-gray-400">
          <CheckCircle2 size={32} strokeWidth={1.75} aria-hidden="true" />
          <span className="text-sm mt-1">{t('monitoring.noExceptions')}</span>
        </div>
      ) : (
        <div className="space-y-3 overflow-auto max-h-72">
          {data.map((ex: ExceptionLog, i) => (
            // exceptionType+timestamp+index as key for stable identity
            <div
              key={`${ex.exceptionType}-${ex.timestamp}-${i}`}
              className="p-3 bg-red-50 rounded border border-red-100"
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-semibold text-red-700">{ex.exceptionType}</span>
                <span className="text-xs text-gray-400">
                  {formatDistanceToNow(new Date(ex.timestamp), { addSuffix: true })}
                </span>
              </div>
              <p className="text-xs text-gray-800 font-mono truncate">{ex.message}</p>
              {ex.path && (
                <p className="text-xs text-gray-500 mt-0.5">
                  {ex.method} {ex.path}
                </p>
              )}
              {ex.stackTrace && (
                <p className="text-xs text-gray-400 mt-1 truncate">{ex.stackTrace}</p>
              )}
              <Link to={logsLinkFor(ex.requestId, 'Error')} className="text-xs text-primary-600 hover:underline">
                {t('monitoring.viewLogs')}
              </Link>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// ── Shared helpers ─────────────────────────────────────────────────────────
function MetricItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-gray-500">{label}</dt>
      <dd className="text-sm font-medium text-gray-900 truncate">{value}</dd>
    </div>
  )
}
