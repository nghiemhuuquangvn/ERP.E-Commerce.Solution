import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { Navigate, useSearchParams } from 'react-router-dom'
import { logsApi, LogEntry, LogLevel } from '@/api/logs/logs.api'
import { usePermission } from '@/hooks/usePermission'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { Badge, Button, EmptyState, FormField, Input, Modal, Select } from '@/components/ui'
import type { BadgeVariant } from '@/components/ui'

const PAGE_SIZE = 25
const LEVELS: LogLevel[] = ['Information', 'Warning', 'Error', 'Critical']
const KINDS = ['Trace', 'Exception']
// Suggestions only: the services that ship logs today (AddSerilogLogging) and the admin app.
const KNOWN_SERVICES = ['Gateway', 'IdentityService', 'ProductService', 'LoggingService', 'Admin']

const LEVEL_VARIANT: Record<LogLevel, BadgeVariant> = {
  Information: 'info',
  Warning: 'warning',
  Error: 'danger',
  Critical: 'danger',
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
  const filtering = Object.values(filters).some(Boolean)
  const clearAll = () => {
    setSearchParams(new URLSearchParams(), { replace: true })
    setKeywordInput('')
    setPage(1)
  }

  const columns = useMemo(
    () => [
      { key: 'occurredAt', label: t('logs.columns.time'), className: 'whitespace-nowrap text-ink-secondary', render: (l: LogEntry & { id: string }) => format(new Date(l.occurredAt), 'yyyy-MM-dd HH:mm:ss') },
      { key: 'serviceName', label: t('logs.columns.service'), className: 'whitespace-nowrap' },
      { key: 'level', label: t('logs.columns.level'), render: (l: LogEntry & { id: string }) => <Badge variant={LEVEL_VARIANT[l.level] ?? 'muted'}>{l.level}</Badge> },
      { key: 'kind', label: t('logs.columns.kind'), className: 'text-ink-secondary' },
      { key: 'message', label: t('logs.columns.message'), className: 'max-w-xl truncate' },
      {
        key: 'actions',
        label: '',
        render: (l: LogEntry & { id: string }) => (
          <Button variant="link" className="text-xs" aria-label={`${t('common.view')}: ${l.message.slice(0, 40)}`} onClick={() => setSelected(l)}>
            {t('common.view')}
          </Button>
        ),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold text-ink">{t('logs.title')}</h1>
        <p className="text-sm text-ink-secondary">{t('logs.subtitle')}</p>
      </div>

      {filters.requestId && (
        <div className="flex items-center justify-between rounded-lg bg-info-soft px-4 py-2 text-sm text-info-soft-text">
          <span>
            {t('logs.filters.requestIdActive')}: <code>{filters.requestId}</code>
          </span>
          <Button variant="link" onClick={() => setFilter('requestId', '')}>{t('common.clear')}</Button>
        </div>
      )}

      <div className="grid grid-cols-1 gap-3 rounded-lg border border-line bg-surface p-4 md:grid-cols-3 lg:grid-cols-6">
        <FormField label={t('logs.filters.service')}>
          <Input
            list="log-services"
            value={filters.serviceName}
            onChange={(e) => setFilter('serviceName', e.target.value)}
            placeholder={t('logs.filters.allServices')}
          />
          <datalist id="log-services">
            {KNOWN_SERVICES.map((s) => (
              <option key={s} value={s} />
            ))}
          </datalist>
        </FormField>
        <FormField label={t('logs.filters.level')}>
          <Select value={filters.level} onChange={(e) => setFilter('level', e.target.value)}>
            <option value="">{t('logs.filters.allLevels')}</option>
            {LEVELS.map((l) => (
              <option key={l} value={l}>{l}</option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('logs.filters.kind')}>
          <Select value={filters.kind} onChange={(e) => setFilter('kind', e.target.value)}>
            <option value="">{t('logs.filters.allKinds')}</option>
            {KINDS.map((k) => (
              <option key={k} value={k}>{k}</option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('logs.filters.from')}>
          <Input type="datetime-local" value={toLocalInput(filters.fromUtc)} onChange={(e) => setFilter('fromUtc', toUtcIso(e.target.value))} />
        </FormField>
        <FormField label={t('logs.filters.to')}>
          <Input type="datetime-local" value={toLocalInput(filters.toUtc)} onChange={(e) => setFilter('toUtc', toUtcIso(e.target.value))} />
        </FormField>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            setFilter('keyword', keywordInput.trim())
          }}
        >
          <FormField label={t('logs.filters.keywordLabel')}>
            <Input value={keywordInput} onChange={(e) => setKeywordInput(e.target.value)} placeholder={t('logs.filters.keyword')} />
          </FormField>
        </form>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          filtering ? (
            <EmptyState variant="no-results" title={t('common.noResults')} action={<Button variant="outline" size="sm" onClick={clearAll}>{t('common.clear')}</Button>} />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {selected && <LogDetails log={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}

function LogDetails({ log, onClose }: { log: LogEntry; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <Modal
      open
      size="lg"
      title={t('logs.details.title')}
      onClose={onClose}
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-3">
        <dl className="grid grid-cols-3 gap-2 text-sm">
          <dt className="text-ink-secondary">{t('logs.columns.time')}</dt>
          <dd className="col-span-2">{format(new Date(log.occurredAt), 'yyyy-MM-dd HH:mm:ss.SSS')}</dd>
          <dt className="text-ink-secondary">{t('logs.columns.service')}</dt>
          <dd className="col-span-2">
            {log.serviceName}
            {log.environment ? ` (${log.environment})` : ''}
          </dd>
          <dt className="text-ink-secondary">{t('logs.columns.level')}</dt>
          <dd className="col-span-2"><Badge variant={LEVEL_VARIANT[log.level] ?? 'muted'}>{log.level}</Badge></dd>
          {log.exceptionType && (
            <>
              <dt className="text-ink-secondary">{t('logs.details.exceptionType')}</dt>
              <dd className="col-span-2 break-all">{log.exceptionType}</dd>
            </>
          )}
          {log.correlationId && (
            <>
              <dt className="text-ink-secondary">{t('logs.details.correlationId')}</dt>
              <dd className="col-span-2 break-all">{log.correlationId}</dd>
            </>
          )}
        </dl>
        <h3 className="text-sm font-medium text-ink">{t('logs.columns.message')}</h3>
        <pre className="whitespace-pre-wrap break-words rounded bg-surface-muted p-3 text-xs text-ink">{log.message}</pre>
        {log.stackTrace && (
          <pre className="max-h-96 overflow-auto rounded bg-surface-muted p-3 text-xs text-ink">{log.stackTrace}</pre>
        )}
        {log.properties && Object.keys(log.properties).length > 0 && (
          <dl className="grid grid-cols-3 gap-1 text-xs">
            {Object.entries(log.properties).map(([key, value]) => (
              <div key={key} className="contents">
                <dt className="text-ink-secondary">{key}</dt>
                <dd className="col-span-2 break-all">{value}</dd>
              </div>
            ))}
          </dl>
        )}
      </div>
    </Modal>
  )
}
