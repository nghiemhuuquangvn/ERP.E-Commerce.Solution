import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { webhooksApi, type Webhook, type WebhookTestResult } from '@/api/notifications/webhooks.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Pagination } from '@/components/shared/Pagination'
import { serverMessage } from '@/api/purchasing/procurement.api'

const KEY = ['ntf', 'webhooks']
const when = (iso: string) => format(new Date(iso), 'dd MMM yyyy HH:mm:ss')

function WebhookModal({ webhook, onClose }: { webhook: Webhook | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!webhook
  const { data: catalog = [] } = useQuery({ queryKey: [...KEY, 'event-types'], queryFn: webhooksApi.eventTypes, staleTime: 600_000 })
  const [name, setName] = useState(webhook?.name ?? '')
  const [url, setUrl] = useState(webhook?.url ?? 'https://')
  const [secret, setSecret] = useState('')
  const [clearSecret, setClearSecret] = useState(false)
  const [allEvents, setAllEvents] = useState(!webhook || webhook.eventTypes.length === 0)
  const [events, setEvents] = useState<string[]>(webhook?.eventTypes ?? [])

  const save = useMutation({
    mutationFn: () => {
      const body = { name: name.trim(), url: url.trim(), secret: secret.trim(), clearSecret, eventTypes: allEvents ? [] : events }
      return isEdit ? webhooksApi.update(webhook!.id, body) : webhooksApi.create(body)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: KEY }); onClose() },
  })

  const label = 'mb-1 block text-sm font-medium text-gray-700'
  const valid = name.trim() && /^https:\/\/.+/i.test(url.trim()) && (allEvents || events.length > 0)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-xl space-y-4 p-6">
        <h3 className="text-base font-semibold text-gray-900">{isEdit ? t('notificationAdmin.webhooks.editTitle') : t('notificationAdmin.webhooks.addTitle')}</h3>
        <div>
          <label className={label}>{t('common.name')}</label>
          <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div>
          <label className={label}>{t('notificationAdmin.webhooks.url')}</label>
          <input className="input font-mono text-xs" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://example.com/erp-webhook" />
          <p className="mt-1 text-xs text-gray-500">{t('notificationAdmin.webhooks.urlHint')}</p>
        </div>
        <div>
          <label className={label}>{t('notificationAdmin.webhooks.secret')}</label>
          <input className="input" type="password" autoComplete="new-password" value={secret} onChange={(e) => { setSecret(e.target.value); setClearSecret(false) }}
            placeholder={isEdit && webhook!.hasSecret ? t('notificationAdmin.webhooks.secretKept') : t('notificationAdmin.webhooks.secretNone')} />
          <p className="mt-1 text-xs text-gray-500">{t('notificationAdmin.webhooks.secretHint')}</p>
          {isEdit && webhook!.hasSecret && (
            <label className="mt-1 flex items-center gap-2 text-sm text-gray-600">
              <input type="checkbox" checked={clearSecret} onChange={(e) => { setClearSecret(e.target.checked); if (e.target.checked) setSecret('') }} />
              {t('notificationAdmin.webhooks.removeSecret')}
            </label>
          )}
        </div>
        <fieldset>
          <legend className={label}>{t('notificationAdmin.webhooks.events')}</legend>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={allEvents} onChange={(e) => setAllEvents(e.target.checked)} />
            {t('notificationAdmin.webhooks.allEvents')}
          </label>
          {!allEvents && (
            <div className="mt-2 space-y-1 pl-6">
              {catalog.map((c) => (
                <label key={c.type} className="flex items-start gap-2 text-sm">
                  <input type="checkbox" className="mt-1" checked={events.includes(c.type)} onChange={(e) => setEvents((all) => (e.target.checked ? [...all, c.type] : all.filter((x) => x !== c.type)))} />
                  <span><span className="font-mono text-xs">{c.type}</span><span className="block text-xs text-gray-500">{c.description}</span></span>
                </label>
              ))}
            </div>
          )}
        </fieldset>
        {save.isError && <p className="text-sm text-red-600" role="alert">{serverMessage(save.error, t('notificationAdmin.webhooks.failedToSave'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!valid || save.isPending} onClick={() => save.mutate()}>{save.isPending ? t('common.saving') : t('common.save')}</button>
        </div>
      </div>
    </div>
  )
}

function DeliveriesDialog({ webhook, onClose }: { webhook: Webhook; onClose: () => void }) {
  const { t } = useTranslation()
  const [page, setPage] = useState(1)
  const size = 10
  const { data, isLoading } = useQuery({ queryKey: [...KEY, webhook.id, 'deliveries', page], queryFn: () => webhooksApi.deliveries(webhook.id, page, size) })
  const columns = [
    { key: 'createdAt', label: t('notificationAdmin.time'), render: (d: NonNullable<typeof data>['items'][number]) => when(d.createdAt) },
    { key: 'eventType', label: t('notificationAdmin.webhooks.event'), className: 'font-mono text-xs' },
    {
      key: 'result',
      label: t('notificationAdmin.webhooks.result'),
      render: (d: NonNullable<typeof data>['items'][number]) => (
        <span className={d.success ? 'text-green-700' : 'text-red-600'}>{d.success ? t('notificationAdmin.webhooks.ok') : d.error ?? t('notificationAdmin.webhooks.failed')}{d.statusCode ? ` (HTTP ${d.statusCode})` : ''}</span>
      ),
    },
    { key: 'durationMs', label: t('notificationAdmin.webhooks.duration'), className: 'text-right', render: (d: NonNullable<typeof data>['items'][number]) => `${d.durationMs} ms` },
  ]
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-3xl space-y-3 p-6">
        <h3 className="text-base font-semibold text-gray-900">{t('notificationAdmin.webhooks.deliveriesTitle', { name: webhook.name })}</h3>
        <p className="text-xs text-gray-500">{t('notificationAdmin.webhooks.retention')}</p>
        <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} emptyMessage={t('notificationAdmin.webhooks.noDeliveries')} />
        <Pagination page={page} pageSize={size} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
        <div className="flex justify-end"><button className="btn-outline" onClick={onClose}>{t('common.close')}</button></div>
      </div>
    </div>
  )
}

/** Where the shop's webhooks are managed: address, secret (never shown again), events, a test button and the delivery log. */
export function NotificationWebhooksPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [modal, setModal] = useState<{ open: boolean; webhook: Webhook | null }>({ open: false, webhook: null })
  const [deleting, setDeleting] = useState<Webhook | null>(null)
  const [log, setLog] = useState<Webhook | null>(null)
  const [testResult, setTestResult] = useState<{ name: string; result?: WebhookTestResult; error?: string } | null>(null)

  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: webhooksApi.list, staleTime: 15_000 })
  const refresh = () => queryClient.invalidateQueries({ queryKey: KEY })

  const toggle = useMutation({ mutationFn: (w: Webhook) => webhooksApi.setActive(w.id, !w.isActive), onSuccess: refresh })
  const remove = useMutation({ mutationFn: (w: Webhook) => webhooksApi.remove(w.id), onSuccess: () => { setDeleting(null); refresh() } })
  const test = useMutation({
    mutationFn: (w: Webhook) => webhooksApi.test(w.id),
    onSuccess: (result, w) => { setTestResult({ name: w.name, result }); refresh() },
    onError: (e, w) => setTestResult({ name: w.name, error: serverMessage(e, t('notificationAdmin.webhooks.failed')) }),
  })

  const columns = [
    { key: 'name', label: t('common.name') },
    { key: 'url', label: t('notificationAdmin.webhooks.url'), render: (w: Webhook) => <span className="break-all font-mono text-xs">{w.url}</span> },
    {
      key: 'events',
      label: t('notificationAdmin.webhooks.events'),
      render: (w: Webhook) => (w.eventTypes.length === 0
        ? <span className="text-xs text-gray-500">{t('notificationAdmin.webhooks.allEvents')}</span>
        : <div className="flex flex-wrap gap-1">{w.eventTypes.map((e) => <span key={e} className="badge bg-gray-100 font-mono text-gray-700">{e}</span>)}</div>),
    },
    { key: 'secret', label: t('notificationAdmin.webhooks.secret'), render: (w: Webhook) => (w.hasSecret ? t('notificationAdmin.webhooks.signed') : '—') },
    {
      key: 'last',
      label: t('notificationAdmin.webhooks.lastDelivery'),
      render: (w: Webhook) => w.lastDelivery
        ? <div><span className={w.lastDelivery.success ? 'text-green-700' : 'text-red-600'}>{w.lastDelivery.success ? t('notificationAdmin.webhooks.ok') : t('notificationAdmin.webhooks.failed')}{w.lastDelivery.statusCode ? ` · HTTP ${w.lastDelivery.statusCode}` : ''}</span><div className="text-xs text-gray-500">{when(w.lastDelivery.createdAt)}</div></div>
        : <span className="text-xs text-gray-400">{t('notificationAdmin.webhooks.never')}</span>,
    },
    {
      key: 'active',
      label: t('common.status'),
      render: (w: Webhook) => (
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" role="switch" checked={w.isActive} disabled={toggle.isPending} onChange={() => toggle.mutate(w)} />
          {w.isActive ? t('common.active') : t('common.inactive')}
        </label>
      ),
    },
    {
      key: 'actions',
      label: '',
      className: 'text-right',
      render: (w: Webhook) => (
        <div className="flex flex-wrap justify-end gap-3">
          <button className="text-xs text-primary-600 hover:underline" disabled={test.isPending} onClick={() => test.mutate(w)}>{t('notificationAdmin.webhooks.test')}</button>
          <button className="text-xs text-primary-600 hover:underline" onClick={() => setLog(w)}>{t('notificationAdmin.webhooks.deliveries')}</button>
          <button className="text-xs text-primary-600 hover:underline" onClick={() => setModal({ open: true, webhook: w })}>{t('common.edit')}</button>
          <button className="text-xs text-red-600 hover:underline" onClick={() => setDeleting(w)}>{t('common.delete')}</button>
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">{t('notificationAdmin.webhooks.title')}</h1>
          <p className="text-sm text-gray-500">{t('notificationAdmin.webhooks.hint')}</p>
        </div>
        <button className="btn-primary" onClick={() => setModal({ open: true, webhook: null })}>{t('notificationAdmin.webhooks.add')}</button>
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} emptyMessage={t('notificationAdmin.webhooks.empty')} />

      {modal.open && <WebhookModal webhook={modal.webhook} onClose={() => setModal({ open: false, webhook: null })} />}
      {log && <DeliveriesDialog webhook={log} onClose={() => setLog(null)} />}

      {testResult && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="card w-full max-w-md space-y-3 p-6">
            <h3 className="text-base font-semibold text-gray-900">{t('notificationAdmin.webhooks.testTitle', { name: testResult.name })}</h3>
            {testResult.error ? (
              <p className="text-sm text-red-600" role="alert">{testResult.error}</p>
            ) : (
              <div className={`rounded border px-3 py-2 text-sm ${testResult.result!.success ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-700'}`} role="status">
                <div className="font-medium">{testResult.result!.success ? t('notificationAdmin.webhooks.testOk') : t('notificationAdmin.webhooks.testFailed')}</div>
                <div className="mt-1 text-xs">
                  {testResult.result!.statusCode ? `HTTP ${testResult.result!.statusCode} · ` : ''}{testResult.result!.durationMs} ms
                  {testResult.result!.error ? ` · ${testResult.result!.error}` : ''}
                </div>
              </div>
            )}
            <div className="flex justify-end"><button className="btn-outline" onClick={() => setTestResult(null)}>{t('common.close')}</button></div>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={!!deleting}
        title={t('notificationAdmin.webhooks.deleteTitle')}
        message={t('notificationAdmin.webhooks.deleteConfirm', { name: deleting?.name })}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => deleting && remove.mutate(deleting)}
        onCancel={() => setDeleting(null)}
      />
    </div>
  )
}
