import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { Navigate, useSearchParams } from 'react-router-dom'
import { logsApi, LogEntry, LogLevel } from '@/api/logs/logs.api'
import { usePermission } from '@/hooks/usePermission'
import { Pagination } from '@/components/shared/Pagination'

const PAGE_SIZE = 25
const LEVELS: LogLevel[] = ['Information', 'Warning', 'Error', 'Critical']
const KINDS = ['Trace', 'Exception']
// Suggestions only: the services that ship logs today (AddSerilogLogging) and the admin app.
const KNOWN_SERVICES = ['Gateway', 'IdentityService', 'ProductService', 'LoggingService', 'Admin']

const levelClass: Record<LogLevel, string> = {
  Information: 'bg-blue-50 text-blue-700',
  Warning: 'bg-yellow-50 text-yellow-700',
  Error: 'bg-red-50 text-red-700',
  Critical: 'bg-red-100 text-red-800',
}

// Logs carry no tenant (every tenant's requests and stack traces), so logs:view is a
// platform permission: only platform administrators see this page.
export function LogsPage() {
  const { hasPermission } = usePermission()
  if (!hasPermission('logs:view')) {
    return <Navigate to="/dashboard" replace />
  }
  return <LogsView />
}

function LogsView() {
  const { t } = useTranslation()
  // Monitoring deep-links here with ?serviceName=Gateway&level=Error&requestId=…
  const [searchParams, setSearchParams] = useSearchParams()
  const [page, setPage] = useState(1)
  const [keywordInput, setKeywordInput] = useState(searchParams.get('keyword') ?? '')
  const [selected, setSelected] = useState<LogEntry | null>(null)

  const filters = {
    serviceName: searchParams.get('serviceName') ?? '',
    level: searchParams.get('level') ?? '',
    kind: searchParams.get('kind') ?? '',
    keyword: searchParams.get('keyword') ?? '',
    requestId: searchParams.get('requestId') ?? '',
    fromUtc: searchParams.get('fromUtc') ?? '',
    toUtc: searchParams.get('toUtc') ?? '',
  }

  const setFilter = (key: keyof typeof filters, value: string) => {
    const next = new URLSearchParams(searchParams)
    if (value) next.set(key, value)
    else next.delete(key)
    setSearchParams(next, { replace: true })
    setPage(1)
  }

  const { data, isLoading } = useQuery({
    queryKey: ['logs', filters, page],
    queryFn: () => logsApi.search({ ...filters, page, pageSize: PAGE_SIZE }),
    placeholderData: (previous) => previous,
  })

  const toUtcIso = (local: string) => (local ? new Date(local).toISOString() : '')
  const toLocalInput = (iso: string) => (iso ? format(new Date(iso), "yyyy-MM-dd'T'HH:mm") : '')

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">{t('logs.title')}</h1>
        <p className="text-sm text-gray-500">{t('logs.subtitle')}</p>
      </div>

      {filters.requestId && (
        <div className="flex items-center justify-between rounded-lg bg-primary-50 px-4 py-2 text-sm text-primary-700">
          <span>
            {t('logs.filters.requestIdActive')}: <code>{filters.requestId}</code>
          </span>
          <button onClick={() => setFilter('requestId', '')} className="hover:underline">
            {t('common.clear')}
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 gap-3 rounded-lg border border-gray-200 bg-white p-4 md:grid-cols-3 lg:grid-cols-6">
        <input
          list="log-services"
          value={filters.serviceName}
          onChange={(e) => setFilter('serviceName', e.target.value)}
          placeholder={t('logs.filters.allServices')}
          className="input"
        />
        <datalist id="log-services">
          {KNOWN_SERVICES.map((s) => (
            <option key={s} value={s} />
          ))}
        </datalist>
        <select value={filters.level} onChange={(e) => setFilter('level', e.target.value)} className="input">
          <option value="">{t('logs.filters.allLevels')}</option>
          {LEVELS.map((l) => (
            <option key={l} value={l}>
              {l}
            </option>
          ))}
        </select>
        <select value={filters.kind} onChange={(e) => setFilter('kind', e.target.value)} className="input">
          <option value="">{t('logs.filters.allKinds')}</option>
          {KINDS.map((k) => (
            <option key={k} value={k}>
              {k}
            </option>
          ))}
        </select>
        <input
          type="datetime-local"
          value={toLocalInput(filters.fromUtc)}
          onChange={(e) => setFilter('fromUtc', toUtcIso(e.target.value))}
          className="input"
        />
        <input
          type="datetime-local"
          value={toLocalInput(filters.toUtc)}
          onChange={(e) => setFilter('toUtc', toUtcIso(e.target.value))}
          className="input"
        />
        <form
          onSubmit={(e) => {
            e.preventDefault()
            setFilter('keyword', keywordInput.trim())
          }}
        >
          <input
            value={keywordInput}
            onChange={(e) => setKeywordInput(e.target.value)}
            placeholder={t('logs.filters.keyword')}
            className="input w-full"
          />
        </form>
      </div>

      <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
        <table className="min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50 text-left text-xs font-medium uppercase text-gray-500">
            <tr>
              <th className="px-4 py-3">{t('logs.columns.time')}</th>
              <th className="px-4 py-3">{t('logs.columns.service')}</th>
              <th className="px-4 py-3">{t('logs.columns.level')}</th>
              <th className="px-4 py-3">{t('logs.columns.kind')}</th>
              <th className="px-4 py-3">{t('logs.columns.message')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-gray-400">
                  {t('common.loading')}
                </td>
              </tr>
            )}
            {!isLoading && (data?.items.length ?? 0) === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-gray-400">
                  {t('common.noRecordsFound')}
                </td>
              </tr>
            )}
            {data?.items.map((log) => (
              <tr key={log.id} onClick={() => setSelected(log)} className="cursor-pointer hover:bg-gray-50">
                <td className="whitespace-nowrap px-4 py-2 text-gray-500">
                  {format(new Date(log.occurredAt), 'yyyy-MM-dd HH:mm:ss')}
                </td>
                <td className="whitespace-nowrap px-4 py-2">{log.serviceName}</td>
                <td className="px-4 py-2">
                  <span className={`rounded px-2 py-0.5 text-xs font-medium ${levelClass[log.level] ?? ''}`}>
                    {log.level}
                  </span>
                </td>
                <td className="px-4 py-2 text-gray-500">{log.kind}</td>
                <td className="max-w-xl truncate px-4 py-2">{log.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="px-4">
          <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
        </div>
      </div>

      {selected && <LogDetails log={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}

function LogDetails({ log, onClose }: { log: LogEntry; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-black/30" onClick={onClose}>
      <div className="h-full w-full max-w-2xl overflow-y-auto bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-900">{t('logs.details.title')}</h2>
          <button onClick={onClose} className="text-sm text-gray-500 hover:text-gray-700">
            {t('common.close')}
          </button>
        </div>
        <dl className="grid grid-cols-3 gap-2 text-sm">
          <dt className="text-gray-500">{t('logs.columns.time')}</dt>
          <dd className="col-span-2">{format(new Date(log.occurredAt), 'yyyy-MM-dd HH:mm:ss.SSS')}</dd>
          <dt className="text-gray-500">{t('logs.columns.service')}</dt>
          <dd className="col-span-2">
            {log.serviceName}
            {log.environment ? ` (${log.environment})` : ''}
          </dd>
          <dt className="text-gray-500">{t('logs.columns.level')}</dt>
          <dd className="col-span-2">{log.level}</dd>
          {log.exceptionType && (
            <>
              <dt className="text-gray-500">{t('logs.details.exceptionType')}</dt>
              <dd className="col-span-2 break-all">{log.exceptionType}</dd>
            </>
          )}
          {log.correlationId && (
            <>
              <dt className="text-gray-500">{t('logs.details.correlationId')}</dt>
              <dd className="col-span-2 break-all">{log.correlationId}</dd>
            </>
          )}
        </dl>
        <h3 className="mt-4 text-sm font-medium text-gray-700">{t('logs.columns.message')}</h3>
        <pre className="mt-1 whitespace-pre-wrap break-words rounded bg-gray-50 p-3 text-xs">{log.message}</pre>
        {log.stackTrace && (
          <pre className="mt-3 max-h-96 overflow-auto rounded bg-gray-900 p-3 text-xs text-gray-100">{log.stackTrace}</pre>
        )}
        {log.properties && Object.keys(log.properties).length > 0 && (
          <dl className="mt-3 grid grid-cols-3 gap-1 text-xs">
            {Object.entries(log.properties).map(([key, value]) => (
              <div key={key} className="contents">
                <dt className="text-gray-500">{key}</dt>
                <dd className="col-span-2 break-all">{value}</dd>
              </div>
            ))}
          </dl>
        )}
      </div>
    </div>
  )
}
