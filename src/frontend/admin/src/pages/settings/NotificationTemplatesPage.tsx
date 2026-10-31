import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { notificationAdminApi, TEMPLATE_CHANNELS } from '@/api/notifications/notificationAdmin.api'
import type { NotificationTemplate } from '@/api/notifications/notificationAdmin.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'
import { Alert, Badge, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/api/purchasing/procurement.api'

const KEY = ['notifications', 'templates']
/** The platform (SYSTEM) tenant: the only one that edits the platform's default templates directly. */
const PLATFORM_TENANT_ID = '00000000-0000-0000-0000-000000000001'
const identifiers = (csv: string | null | undefined) => (csv ?? '').split(',').map((v) => v.trim()).filter(Boolean)

/** Reading, editing and creating a template: placeholder chips insert {name}, the preview is rendered by the server with sample values. */
function TemplateEditor({ template, platformEditable, onClose }: { template: NotificationTemplate | null; platformEditable: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isNew = !template
  const readOnly = !!template && template.isPlatformDefault && !platformEditable

  const [code, setCode] = useState(template?.code ?? '')
  const [name, setName] = useState(template?.name ?? '')
  const [channel, setChannel] = useState<string>(template?.channel ?? 'Email')
  const [subject, setSubject] = useState(template?.subject ?? '')
  const [body, setBody] = useState(template?.body ?? '')
  const [variables, setVariables] = useState(template?.variables ?? '')
  const bodyRef = useRef<HTMLTextAreaElement>(null)

  const declared = useMemo(() => identifiers(variables), [variables])
  // Sample values are just the placeholder names in brackets, so the preview shows where each value goes.
  const samples = useMemo(() => Object.fromEntries(declared.map((v) => [v, `‹${v}›`])), [declared])

  const [preview, setPreview] = useState<{ subject: string; body: string; unknownPlaceholders: string[] } | null>(null)
  useEffect(() => {
    const handle = window.setTimeout(() => {
      notificationAdminApi.preview({ subject, body, variables: samples }).then(setPreview).catch(() => setPreview(null))
    }, 400)
    return () => window.clearTimeout(handle)
  }, [subject, body, samples])

  const insert = (v: string) => {
    const el = bodyRef.current
    const text = `{${v}}`
    if (!el) { setBody((b) => b + text); return }
    const start = el.selectionStart ?? body.length
    const end = el.selectionEnd ?? body.length
    setBody(body.slice(0, start) + text + body.slice(end))
    window.setTimeout(() => { el.focus(); el.setSelectionRange(start + text.length, start + text.length) }, 0)
  }

  const save = useMutation({
    mutationFn: () => isNew
      ? notificationAdminApi.createTemplate({ code: code.trim(), name: name.trim(), channel: TEMPLATE_CHANNELS[channel as keyof typeof TEMPLATE_CHANNELS], subject, body, variables: variables.trim() || null })
      : notificationAdminApi.updateTemplate(template!.id, { name: name.trim(), subject, body, variables: variables.trim() || null }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: KEY }); toast.success(t('common.saved')); onClose() },
  })

  const valid = name.trim() && body.trim() && (!isNew || /^[A-Za-z0-9_]{2,100}$/.test(code.trim()))

  return (
    <Modal
      open
      size="lg"
      title={isNew ? t('notificationAdmin.templates.addTitle') : readOnly ? template!.name : t('notificationAdmin.templates.editTitle')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{readOnly ? t('common.close') : t('common.cancel')}</Button>
          {!readOnly && <Button disabled={!valid} loading={save.isPending} onClick={() => save.mutate()}>{save.isPending ? t('common.saving') : t('common.save')}</Button>}
        </>
      }
    >
      <div className="space-y-4">
        {readOnly && <Alert variant="warning">{t('notificationAdmin.templates.platformReadOnly')}</Alert>}

        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <FormField label={t('notificationAdmin.code')} required={isNew}>
                <Input className="font-mono text-xs" value={code} disabled={!isNew} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="ORDER_SHIPPED" />
              </FormField>
              <FormField label={t('notificationAdmin.channel')}>
                <Select value={channel} disabled={!isNew} onChange={(e) => setChannel(e.target.value)}>
                  {Object.keys(TEMPLATE_CHANNELS).map((c) => <option key={c} value={c}>{c}</option>)}
                </Select>
              </FormField>
            </div>
            <FormField label={t('notificationAdmin.name')} required>
              <Input value={name} disabled={readOnly} onChange={(e) => setName(e.target.value)} />
            </FormField>
            <FormField label={t('notificationAdmin.subject')}>
              <Input value={subject} disabled={readOnly} onChange={(e) => setSubject(e.target.value)} />
            </FormField>
            <FormField label={t('notificationAdmin.body')} required>
              <Textarea ref={bodyRef} className="min-h-[10rem] font-mono text-xs" value={body} disabled={readOnly} onChange={(e) => setBody(e.target.value)} />
            </FormField>
            <div>
              <FormField label={t('notificationAdmin.variables')} hint={t('notificationAdmin.templates.variablesHint')}>
                <Input className="font-mono text-xs" value={variables} disabled={readOnly} onChange={(e) => setVariables(e.target.value)} placeholder="orderNumber, customerName" />
              </FormField>
              {!readOnly && declared.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1">
                  {declared.map((v) => (
                    <button
                      key={v}
                      type="button"
                      className="rounded-full bg-brand-soft px-2.5 py-0.5 font-mono text-xs font-medium text-brand-soft-text hover:bg-brand-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action"
                      onClick={() => insert(v)}
                      title={t('notificationAdmin.templates.insert')}
                    >{`{${v}}`}</button>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="space-y-3">
            <div className="text-xs font-semibold uppercase tracking-wide text-ink-secondary">{t('notificationAdmin.templates.preview')}</div>
            <div className="rounded-md bg-surface-muted p-3 text-sm">
              <div className="font-medium text-ink">{preview?.subject || <span className="text-ink-secondary">{t('notificationAdmin.templates.noSubject')}</span>}</div>
              <pre className="mt-2 whitespace-pre-wrap font-sans text-ink">{preview?.body}</pre>
            </div>
            {preview && preview.unknownPlaceholders.length > 0 && (
              <Alert variant="warning">{t('notificationAdmin.templates.unknown', { list: preview.unknownPlaceholders.map((p) => `{${p}}`).join(' ') })}</Alert>
            )}
          </div>
        </div>

        {save.isError && <Alert variant="danger">{serverMessage(save.error, t('notificationAdmin.templates.failedToSave'))}</Alert>}
      </div>
    </Modal>
  )
}

export function NotificationTemplatesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [editor, setEditor] = useState<{ open: boolean; template: NotificationTemplate | null }>({ open: false, template: null })
  const [deleting, setDeleting] = useState<NotificationTemplate | null>(null)
  const { data = [], isLoading } = useQuery({ queryKey: KEY, queryFn: notificationAdminApi.templates, staleTime: 60_000 })

  const canManage = hasPermission('notifications:templates:manage')
  const isPlatform = useAuthStore((st) => st.user?.tenantId?.toLowerCase() === PLATFORM_TENANT_ID)
  const refresh = () => queryClient.invalidateQueries({ queryKey: KEY })
  const fail = (e: unknown) => toast.error(serverMessage(e, t('notificationAdmin.templates.failed')))
  const ok = (text: string) => toast.success(text)

  const override = useMutation({ mutationFn: (x: NotificationTemplate) => notificationAdminApi.overrideTemplate(x.id), onSuccess: (copy) => { refresh(); ok(t('notificationAdmin.templates.overridden')); setEditor({ open: true, template: copy }) }, onError: fail })
  const toggle = useMutation({ mutationFn: (x: NotificationTemplate) => notificationAdminApi.setTemplateActive(x.id, !x.isActive), onSuccess: () => { refresh(); ok(t('common.saved')) }, onError: fail })
  const remove = useMutation({ mutationFn: (x: NotificationTemplate) => notificationAdminApi.deleteTemplate(x.id), onSuccess: () => { setDeleting(null); ok(t('notificationAdmin.templates.deleted')); refresh() }, onError: (e) => { setDeleting(null); fail(e) } })

  // A platform default a shop has overridden is shadowed by the shop's own row with the same code.
  const overridden = useMemo(() => new Set(data.filter((x) => !x.isPlatformDefault).map((x) => x.code)), [data])

  const columns = [
    { key: 'code', label: t('notificationAdmin.code'), className: 'font-mono text-xs' },
    { key: 'name', label: t('notificationAdmin.name') },
    { key: 'channel', label: t('notificationAdmin.channel') },
    { key: 'subject', label: t('notificationAdmin.subject'), render: (x: NotificationTemplate) => <span className="line-clamp-1 max-w-xs">{x.subject}</span> },
    {
      key: 'origin',
      label: t('notificationAdmin.origin'),
      render: (x: NotificationTemplate) => (
        <div className="space-y-1">
          <Badge variant={x.isPlatformDefault ? 'muted' : 'brand'}>
            {x.isPlatformDefault ? t('notificationAdmin.platformDefault') : t('notificationAdmin.tenantOverride')}
          </Badge>
          {x.isPlatformDefault && overridden.has(x.code) && <div className="text-xs text-ink-secondary">{t('notificationAdmin.templates.shadowed')}</div>}
        </div>
      ),
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (x: NotificationTemplate) => <Badge variant={x.isActive ? 'success' : 'muted'}>{x.isActive ? t('common.active') : t('notificationAdmin.templates.off')}</Badge>,
    },
    {
      key: 'actions',
      label: '',
      className: 'text-right',
      render: (x: NotificationTemplate) => (
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="link" className="text-xs" onClick={() => setEditor({ open: true, template: x })}>{canManage && (!x.isPlatformDefault || isPlatform) ? t('common.edit') : t('notificationAdmin.view')}</Button>
          {canManage && !isPlatform && x.isPlatformDefault && !overridden.has(x.code) && (
            <Button variant="link" className="text-xs" loading={override.isPending && override.variables?.id === x.id} onClick={() => override.mutate(x)}>{t('notificationAdmin.templates.override')}</Button>
          )}
          {canManage && (!x.isPlatformDefault || isPlatform) && (
            <Button variant="link" className="text-xs" loading={toggle.isPending && toggle.variables?.id === x.id} onClick={() => toggle.mutate(x)}>{x.isActive ? t('common.deactivate') : t('common.activate')}</Button>
          )}
          {canManage && !x.isPlatformDefault && (
            <Button variant="link-danger" className="text-xs" onClick={() => setDeleting(x)}>{t('notificationAdmin.templates.revert')}</Button>
          )}
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold">{t('notificationAdmin.templatesTitle')}</h1>
          <p className="text-sm text-ink-secondary">{t('notificationAdmin.templates.hint')}</p>
        </div>
        {canManage && <Button className="shrink-0" onClick={() => setEditor({ open: true, template: null })}>{t('notificationAdmin.templates.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={data}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('notificationAdmin.templates.empty')} description={t('notificationAdmin.templates.emptyHint')} />}
      />

      {editor.open && <TemplateEditor template={editor.template} platformEditable={isPlatform && canManage} onClose={() => setEditor({ open: false, template: null })} />}

      <ConfirmDialog
        open={!!deleting}
        title={t('notificationAdmin.templates.revertTitle')}
        message={t('notificationAdmin.templates.revertConfirm', { name: deleting?.name })}
        confirmLabel={t('notificationAdmin.templates.revert')}
        danger
        onConfirm={() => deleting && remove.mutate(deleting)}
        onCancel={() => setDeleting(null)}
      />
    </div>
  )
}
