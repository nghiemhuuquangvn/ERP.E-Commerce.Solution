import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { notificationAdminApi } from '@/api/notifications/notificationAdmin.api'
import type { SentNotification } from '@/api/notifications/notificationAdmin.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { Alert, Button, EmptyState, Input, Modal, Select } from '@/components/ui'

const STATUSES = ['Queued', 'Sending', 'Sent', 'Delivered', 'Failed', 'Bounced']
const CHANNELS = ['Email', 'Sms', 'Push', 'InApp']

function DetailModal({ notification, onClose }: { notification: SentNotification; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <Modal
      open
      size="md"
      title={notification.subject}
      onClose={onClose}
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-3 text-sm">
        <div className="flex items-start justify-between gap-3">
          <p className="text-ink-secondary">{notification.recipientEmail} · {notification.channel}</p>
          <StatusBadge status={notification.status} />
        </div>
        <pre className="whitespace-pre-wrap rounded-md bg-surface-muted p-3 text-ink">{notification.body}</pre>
        {notification.errorMessage && (
          <Alert variant="danger">
            {t('notificationAdmin.error')}: {notification.errorMessage} ({t('notificationAdmin.retries', { count: notification.retryCount })})
          </Alert>
        )}
        <div>
          <h4 className="text-xs font-semibold uppercase tracking-wide text-ink-secondary">{t('notificationAdmin.log')}</h4>
          <ul className="mt-1 space-y-1 text-xs text-ink-secondary">
            {notification.logs.map((l, i) => (
              <li key={i}>{new Date(l.eventTimestamp).toLocaleString()} — {l.eventType}: {l.details}</li>
            ))}
          </ul>
        </div>
      </div>
    </Modal>
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

  const filtering = !!(status || channel || search)

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
        render: (n: SentNotification) => (n.errorMessage ? <span className="text-xs text-danger-soft-text">{n.errorMessage}</span> : null),
      },
      {
        key: 'actions',
        label: '',
        render: (n: SentNotification) => (
          <Button variant="link" className="text-xs" aria-label={`${t('notificationAdmin.view')}: ${n.subject}`} onClick={() => setDetail(n)}>
            {t('notificationAdmin.view')}
          </Button>
        ),
      },
    ],
    [t],
  )

  const reset = () => setPage(1)
  const clear = () => {
    setStatus('')
    setChannel('')
    setSearch('')
    reset()
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('notificationAdmin.historyTitle')}</h1>
        <p className="text-sm text-ink-secondary">{t('notificationAdmin.historyHint')}</p>
      </div>
      <div className="flex flex-wrap gap-3">
        <Select
          aria-label={t('common.status')}
          className="max-w-[11rem]"
          value={status}
          onChange={(e) => { setStatus(e.target.value); reset() }}
        >
          <option value="">{t('notificationAdmin.allStatuses')}</option>
          {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
        </Select>
        <Select
          aria-label={t('notificationAdmin.channel')}
          className="max-w-[11rem]"
          value={channel}
          onChange={(e) => { setChannel(e.target.value); reset() }}
        >
          <option value="">{t('notificationAdmin.allChannels')}</option>
          {CHANNELS.map((c) => <option key={c} value={c}>{c}</option>)}
        </Select>
        <Input
          type="search"
          aria-label={t('notificationAdmin.searchHint')}
          className="max-w-xs"
          placeholder={t('notificationAdmin.searchHint')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); reset() }}
        />
      </div>
      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          filtering ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              action={<Button variant="outline" size="sm" onClick={clear}>{t('common.clear')}</Button>}
            />
          ) : (
            <EmptyState title={t('notificationAdmin.empty')} />
          )
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
      {detail && <DetailModal notification={detail} onClose={() => setDetail(null)} />}
    </div>
  )
}
