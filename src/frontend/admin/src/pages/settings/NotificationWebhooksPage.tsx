import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { webhooksApi, type Webhook, type WebhookTestResult } from '@/api/notifications/webhooks.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Pagination } from '@/components/shared/Pagination'
import { serverMessage } from '@/api/purchasing/procurement.api'
import { Alert, Badge, Button, Checkbox, EmptyState, FormField, Input, Modal, Switch, useToast } from '@/components/ui'

const KEY = ['ntf', 'webhooks']
const when = (iso: string) => format(new Date(iso), 'dd MMM yyyy HH:mm:ss')

function WebhookModal({ webhook, onClose }: { webhook: Webhook | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: KEY }); toast.success(t('common.saved')); onClose() },
  })

  const valid = name.trim() && /^https:\/\/.+/i.test(url.trim()) && (allEvents || events.length > 0)

  return (
    <Modal
      open
      size="md"
      title={isEdit ? t('notificationAdmin.webhooks.editTitle') : t('notificationAdmin.webhooks.addTitle')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={!valid} loading={save.isPending} onClick={() => save.mutate()}>{save.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <div className="space-y-4">
        <FormField label={t('common.name')} required>
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </FormField>
        <FormField label={t('notificationAdmin.webhooks.url')} required hint={t('notificationAdmin.webhooks.urlHint')}>
          <Input className="font-mono text-xs" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://example.com/erp-webhook" />
        </FormField>
        <div>
          <FormField label={t('notificationAdmin.webhooks.secret')} hint={t('notificationAdmin.webhooks.secretHint')}>
            <Input type="password" autoComplete="new-password" value={secret} onChange={(e) => { setSecret(e.target.value); setClearSecret(false) }}
              placeholder={isEdit && webhook!.hasSecret ? t('notificationAdmin.webhooks.secretKept') : t('notificationAdmin.webhooks.secretNone')} />
          </FormField>
          {isEdit && webhook!.hasSecret && (
            <Checkbox className="mt-1" label={t('notificationAdmin.webhooks.removeSecret')} checked={clearSecret} onChange={(e) => { setClearSecret(e.target.checked); if (e.target.checked) setSecret('') }} />
          )}
        </div>
        <fieldset>
          <legend className="mb-1 block text-sm font-medium text-ink-secondary">{t('notificationAdmin.webhooks.events')}</legend>
          <Checkbox label={t('notificationAdmin.webhooks.allEvents')} checked={allEvents} onChange={(e) => setAllEvents(e.target.checked)} />
          {!allEvents && (
            <div className="mt-2 space-y-1 pl-6">
              {catalog.map((c) => (
                <Checkbox
                  key={c.type}
                  label={<span><span className="font-mono text-xs">{c.type}</span><span className="block text-xs text-ink-secondary">{c.description}</span></span>}
                  checked={events.includes(c.type)}
                  onChange={(e) => setEvents((all) => (e.target.checked ? [...all, c.type] : all.filter((x) => x !== c.type)))}
                />
              ))}
            </div>
          )}
        </fieldset>
        {save.isError && <Alert variant="danger">{serverMessage(save.error, t('notificationAdmin.webhooks.failedToSave'))}</Alert>}
      </div>
    </Modal>
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
        <span className={d.success ? 'text-success-soft-text' : 'text-danger-soft-text'}>{d.success ? t('notificationAdmin.webhooks.ok') : d.error ?? t('notificationAdmin.webhooks.failed')}{d.statusCode ? ` (HTTP ${d.statusCode})` : ''}</span>
      ),
    },
    { key: 'durationMs', label: t('notificationAdmin.webhooks.duration'), align: 'right' as const, render: (d: NonNullable<typeof data>['items'][number]) => `${d.durationMs} ms` },
  ]
  return (
    <Modal
      open
      size="lg"
      title={t('notificationAdmin.webhooks.deliveriesTitle', { name: webhook.name })}
      onClose={onClose}
      dismissOnBackdrop
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-3">
        <p className="text-xs text-ink-secondary">{t('notificationAdmin.webhooks.retention')}</p>
        <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} emptyState={<EmptyState title={t('notificationAdmin.webhooks.noDeliveries')} />} />
        <Pagination page={page} pageSize={size} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />
      </div>
    </Modal>
  )
}

/** Where the shop's webhooks are managed: address, secret (never shown again), events, a test button and the delivery log. */
export function NotificationWebhooksPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [modal, setModal] = useState<{ open: boolean; webhook: Webhook | null }>({ open: false, webhook: null })
  const [deleting, setDeleting] = useState<Webhook | null>(null)
  const [log, setLog] = useState<Webhook | null>(null)
  const [testResult, setTestResult] = useState<{ name: string; result?: WebhookTestResult; error?: string } | null>(null)

  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: webhooksApi.list, staleTime: 15_000 })
  const refresh = () => queryClient.invalidateQueries({ queryKey: KEY })

  const toggle = useMutation({
    mutationFn: (w: Webhook) => webhooksApi.setActive(w.id, !w.isActive),
    onSuccess: () => { toast.success(t('common.saved')); refresh() },
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })
  const remove = useMutation({ mutationFn: (w: Webhook) => webhooksApi.remove(w.id), onSuccess: () => { toast.success(t('common.saved')); setDeleting(null); refresh() }, onError: (e) => { toast.error(serverMessage(e, t('common.failedToSave'))); setDeleting(null) } })
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
        ? <span className="text-xs text-ink-secondary">{t('notificationAdmin.webhooks.allEvents')}</span>
        : <div className="flex flex-wrap gap-1">{w.eventTypes.map((e) => <Badge key={e} className="font-mono">{e}</Badge>)}</div>),
    },
    { key: 'secret', label: t('notificationAdmin.webhooks.secret'), render: (w: Webhook) => (w.hasSecret ? t('notificationAdmin.webhooks.signed') : '—') },
    {
      key: 'last',
      label: t('notificationAdmin.webhooks.lastDelivery'),
      render: (w: Webhook) => w.lastDelivery
        ? <div><span className={w.lastDelivery.success ? 'text-success-soft-text' : 'text-danger-soft-text'}>{w.lastDelivery.success ? t('notificationAdmin.webhooks.ok') : t('notificationAdmin.webhooks.failed')}{w.lastDelivery.statusCode ? ` · HTTP ${w.lastDelivery.statusCode}` : ''}</span><div className="text-xs text-ink-secondary">{when(w.lastDelivery.createdAt)}</div></div>
        : <span className="text-xs text-ink-secondary">{t('notificationAdmin.webhooks.never')}</span>,
    },
    {
      key: 'active',
      label: t('common.status'),
      render: (w: Webhook) => (
        <div className="flex items-center gap-2 text-sm">
          <Switch checked={w.isActive} disabled={toggle.isPending} label={`${w.name}: ${w.isActive ? t('common.active') : t('common.inactive')}`} onChange={() => toggle.mutate(w)} />
          {w.isActive ? t('common.active') : t('common.inactive')}
        </div>
      ),
    },
    {
      key: 'actions',
      label: '',
      className: 'text-right',
      render: (w: Webhook) => (
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="link" className="text-xs" loading={test.isPending && test.variables?.id === w.id} onClick={() => test.mutate(w)}>{t('notificationAdmin.webhooks.test')}</Button>
          <Button variant="link" className="text-xs" onClick={() => setLog(w)}>{t('notificationAdmin.webhooks.deliveries')}</Button>
          <Button variant="link" className="text-xs" onClick={() => setModal({ open: true, webhook: w })}>{t('common.edit')}</Button>
          <Button variant="link-danger" className="text-xs" onClick={() => setDeleting(w)}>{t('common.delete')}</Button>
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">{t('notificationAdmin.webhooks.title')}</h1>
          <p className="text-sm text-ink-secondary">{t('notificationAdmin.webhooks.hint')}</p>
        </div>
        <Button className="shrink-0" onClick={() => setModal({ open: true, webhook: null })}>{t('notificationAdmin.webhooks.add')}</Button>
      </div>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('notificationAdmin.webhooks.empty')} description={t('notificationAdmin.webhooks.emptyHint')} action={<Button onClick={() => setModal({ open: true, webhook: null })}>{t('notificationAdmin.webhooks.add')}</Button>} />}
      />

      {modal.open && <WebhookModal webhook={modal.webhook} onClose={() => setModal({ open: false, webhook: null })} />}
      {log && <DeliveriesDialog webhook={log} onClose={() => setLog(null)} />}

      {testResult && (
        <Modal
          open
          size="sm"
          title={t('notificationAdmin.webhooks.testTitle', { name: testResult.name })}
          onClose={() => setTestResult(null)}
          dismissOnBackdrop
          footer={<Button variant="outline" onClick={() => setTestResult(null)}>{t('common.close')}</Button>}
        >
          {testResult.error ? (
            <Alert variant="danger">{testResult.error}</Alert>
          ) : (
            <Alert variant={testResult.result!.success ? 'success' : 'danger'} title={testResult.result!.success ? t('notificationAdmin.webhooks.testOk') : t('notificationAdmin.webhooks.testFailed')}>
              <span className="text-xs">
                {testResult.result!.statusCode ? `HTTP ${testResult.result!.statusCode} · ` : ''}{testResult.result!.durationMs} ms
                {testResult.result!.error ? ` · ${testResult.result!.error}` : ''}
              </span>
            </Alert>
          )}
        </Modal>
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
