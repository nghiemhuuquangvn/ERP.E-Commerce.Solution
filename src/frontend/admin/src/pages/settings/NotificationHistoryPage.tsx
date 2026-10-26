import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { notificationAdminApi } from '@/api/notifications/notificationAdmin.api'
import type { SentNotification } from '@/api/notifications/notificationAdmin.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'

const STATUSES = ['Queued', 'Sending', 'Sent', 'Delivered', 'Failed', 'Bounced']
const CHANNELS = ['Email', 'Sms', 'Push', 'InApp']

function DetailModal({ notification, onClose }: { notification: SentNotification; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-xl overflow-y-auto p-6 text-sm">
        <div className="mb-3 flex items-start justify-between">
          <div>
            <h3 className="text-base font-semibold text-gray-900">{notification.subject}</h3>
            <p className="text-gray-500">{notification.recipientEmail} · {notification.channel}</p>
          </div>
          <StatusBadge status={notification.status} />
        </div>
        <pre className="whitespace-pre-wrap rounded-md bg-gray-50 p-3 text-gray-800">{notification.body}</pre>
        {notification.errorMessage && (
          <p className="mt-3 rounded-md bg-red-50 px-3 py-2 text-xs text-red-700">
            {t('notificationAdmin.error')}: {notification.errorMessage} ({t('notificationAdmin.retries', { count: notification.retryCount })})
          </p>
        )}
        <div className="mt-3 text-xs font-semibold uppercase tracking-wide text-gray-500">{t('notificationAdmin.log')}</div>
        <ul className="mt-1 space-y-1 text-xs text-gray-600">
          {notification.logs.map((l, i) => (
            <li key={i}>{new Date(l.eventTimestamp).toLocaleString()} — {l.eventType}: {l.details}</li>
          ))}
        </ul>
        <div className="mt-4 flex justify-end">
          <button className="btn-outline" onClick={onClose}>{t('common.close')}</button>
        </div>
      </div>
    </div>
  )
}

export function NotificationHistoryPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState('')
  const [channel, setChannel] = useState('')
  const [search, setSearch] = useState('')
  const [detail, setDetail] = useState<SentNotification | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['notifications', 'history', { page, pageSize, status, channel, search }],
    queryFn: () =>
      notificationAdminApi.history({
        status: status || undefined,
        channel: channel || undefined,
        search: search || undefined,
        skip: (page - 1) * pageSize,
        take: pageSize,
      }),
    staleTime: 10_000,
  })

  const columns = useMemo(
    () => [
      { key: 'createdAt', label: t('notificationAdmin.time'), render: (n: SentNotification) => new Date(n.createdAt).toLocaleString() },
      { key: 'recipientEmail', label: t('notificationAdmin.recipient') },
      { key: 'channel', label: t('notificationAdmin.channel') },
      { key: 'subject', label: t('notificationAdmin.subject') },
      { key: 'status', label: t('common.status'), render: (n: SentNotification) => <StatusBadge status={n.status} /> },
      {
        key: 'error',
        label: t('notificationAdmin.error'),
        render: (n: SentNotification) => (n.errorMessage ? <span className="text-xs text-red-600">{n.errorMessage}</span> : null),
      },
      {
        key: 'actions',
        label: '',
        render: (n: SentNotification) => (
          <button className="text-xs text-blue-600 hover:underline" onClick={() => setDetail(n)}>{t('notificationAdmin.view')}</button>
        ),
      },
    ],
    [t],
  )

  const reset = () => setPage(1)

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('notificationAdmin.historyTitle')}</h1>
        <p className="text-sm text-gray-500">{t('notificationAdmin.historyHint')}</p>
      </div>
      <div className="flex flex-wrap gap-3">
        <select className="input max-w-[11rem]" value={status} onChange={(e) => { setStatus(e.target.value); reset() }}>
          <option value="">{t('notificationAdmin.allStatuses')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select className="input max-w-[11rem]" value={channel} onChange={(e) => { setChannel(e.target.value); reset() }}>
          <option value="">{t('notificationAdmin.allChannels')}</option>
          {CHANNELS.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <input className="input max-w-xs" placeholder={t('notificationAdmin.searchHint')} value={search} onChange={(e) => { setSearch(e.target.value); reset() }} />
      </div>
      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
      {detail && <DetailModal notification={detail} onClose={() => setDetail(null)} />}
    </div>
  )
}
