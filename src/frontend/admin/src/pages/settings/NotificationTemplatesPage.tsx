import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { notificationAdminApi, TEMPLATE_CHANNELS } from '@/api/notifications/notificationAdmin.api'
import type { NotificationTemplate } from '@/api/notifications/notificationAdmin.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'
import { serverMessage } from '@/api/purchasing/procurement.api'

const KEY = ['notifications', 'templates']
/** The platform (SYSTEM) tenant: the only one that edits the platform's default templates directly. */
const PLATFORM_TENANT_ID = '00000000-0000-0000-0000-000000000001'
const identifiers = (csv: string | null | undefined) => (csv ?? '').split(',').map((v) => v.trim()).filter(Boolean)

/** Reading, editing and creating a template: placeholder chips insert {name}, the preview is rendered by the server with sample values. */
function TemplateEditor({ template, platformEditable, onClose }: { template: NotificationTemplate | null; platformEditable: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
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
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: KEY }); onClose() },
  })

  const label = 'mb-1 block text-sm font-medium text-gray-700'
  const valid = name.trim() && body.trim() && (!isNew || /^[A-Za-z0-9_]{2,100}$/.test(code.trim()))

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-4xl space-y-4 p-6">
        <div>
          <h3 className="text-base font-semibold text-gray-900">
            {isNew ? t('notificationAdmin.templates.addTitle') : readOnly ? template!.name : t('notificationAdmin.templates.editTitle')}
          </h3>
          {readOnly && <p className="mt-1 rounded bg-yellow-50 px-3 py-2 text-xs text-yellow-800">{t('notificationAdmin.templates.platformReadOnly')}</p>}
        </div>

        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className={label}>{t('notificationAdmin.code')}</label>
                <input className="input font-mono text-xs" value={code} disabled={!isNew} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="ORDER_SHIPPED" />
              </div>
              <div>
                <label className={label}>{t('notificationAdmin.channel')}</label>
                <select className="input" value={channel} disabled={!isNew} onChange={(e) => setChannel(e.target.value)}>
                  {Object.keys(TEMPLATE_CHANNELS).map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
            </div>
            <div>
              <label className={label}>{t('notificationAdmin.name')}</label>
              <input className="input" value={name} disabled={readOnly} onChange={(e) => setName(e.target.value)} />
            </div>
            <div>
              <label className={label}>{t('notificationAdmin.subject')}</label>
              <input className="input" value={subject} disabled={readOnly} onChange={(e) => setSubject(e.target.value)} />
            </div>
            <div>
              <label className={label}>{t('notificationAdmin.body')}</label>
              <textarea ref={bodyRef} className="input min-h-[10rem] font-mono text-xs" value={body} disabled={readOnly} onChange={(e) => setBody(e.target.value)} />
            </div>
            <div>
              <label className={label}>{t('notificationAdmin.variables')}</label>
              <input className="input font-mono text-xs" value={variables} disabled={readOnly} onChange={(e) => setVariables(e.target.value)} placeholder="orderNumber, customerName" />
              <p className="mt-1 text-xs text-gray-500">{t('notificationAdmin.templates.variablesHint')}</p>
              {!readOnly && declared.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1">
                  {declared.map((v) => (
                    <button key={v} type="button" className="badge bg-primary-50 font-mono text-primary-700 hover:bg-primary-100" onClick={() => insert(v)} title={t('notificationAdmin.templates.insert')}>{`{${v}}`}</button>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="space-y-3">
            <div className="text-xs font-semibold uppercase tracking-wide text-gray-500">{t('notificationAdmin.templates.preview')}</div>
            <div className="rounded-md bg-gray-50 p-3 text-sm">
              <div className="font-medium text-gray-900">{preview?.subject || <span className="text-gray-400">{t('notificationAdmin.templates.noSubject')}</span>}</div>
              <pre className="mt-2 whitespace-pre-wrap font-sans text-gray-800">{preview?.body}</pre>
            </div>
            {preview && preview.unknownPlaceholders.length > 0 && (
              <p className="rounded bg-yellow-50 px-3 py-2 text-xs text-yellow-800" role="alert">
                {t('notificationAdmin.templates.unknown', { list: preview.unknownPlaceholders.map((p) => `{${p}}`).join(' ') })}
              </p>
            )}
          </div>
        </div>

        {save.isError && <p className="text-sm text-red-600" role="alert">{serverMessage(save.error, t('notificationAdmin.templates.failedToSave'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{readOnly ? t('common.close') : t('common.cancel')}</button>
          {!readOnly && <button className="btn-primary" disabled={!valid || save.isPending} onClick={() => save.mutate()}>{save.isPending ? t('common.saving') : t('common.save')}</button>}
        </div>
      </div>
    </div>
  )
}

export function NotificationTemplatesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [editor, setEditor] = useState<{ open: boolean; template: NotificationTemplate | null }>({ open: false, template: null })
  const [deleting, setDeleting] = useState<NotificationTemplate | null>(null)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const { data = [], isLoading } = useQuery({ queryKey: KEY, queryFn: notificationAdminApi.templates, staleTime: 60_000 })

  const canManage = hasPermission('notifications:templates:manage')
  const isPlatform = useAuthStore((st) => st.user?.tenantId?.toLowerCase() === PLATFORM_TENANT_ID)
  const refresh = () => queryClient.invalidateQueries({ queryKey: KEY })
  const fail = (e: unknown) => setMessage({ ok: false, text: serverMessage(e, t('notificationAdmin.templates.failed')) })
  const ok = (text: string) => setMessage({ ok: true, text })

  const override = useMutation({ mutationFn: (x: NotificationTemplate) => notificationAdminApi.overrideTemplate(x.id), onSuccess: (copy) => { refresh(); ok(t('notificationAdmin.templates.overridden')); setEditor({ open: true, template: copy }) }, onError: fail })
  const toggle = useMutation({ mutationFn: (x: NotificationTemplate) => notificationAdminApi.setTemplateActive(x.id, !x.isActive), onSuccess: refresh, onError: fail })
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
          <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${x.isPlatformDefault ? 'bg-gray-100 text-gray-600' : 'bg-blue-100 text-blue-700'}`}>
            {x.isPlatformDefault ? t('notificationAdmin.platformDefault') : t('notificationAdmin.tenantOverride')}
          </span>
          {x.isPlatformDefault && overridden.has(x.code) && <div className="text-xs text-gray-400">{t('notificationAdmin.templates.shadowed')}</div>}
        </div>
      ),
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (x: NotificationTemplate) => <span className={`badge ${x.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>{x.isActive ? t('common.active') : t('notificationAdmin.templates.off')}</span>,
    },
    {
      key: 'actions',
      label: '',
      className: 'text-right',
      render: (x: NotificationTemplate) => (
        <div className="flex flex-wrap justify-end gap-3">
          <button className="text-xs text-blue-600 hover:underline" onClick={() => setEditor({ open: true, template: x })}>{canManage && (!x.isPlatformDefault || isPlatform) ? t('common.edit') : t('notificationAdmin.view')}</button>
          {canManage && !isPlatform && x.isPlatformDefault && !overridden.has(x.code) && (
            <button className="text-xs text-primary-600 hover:underline" disabled={override.isPending} onClick={() => override.mutate(x)}>{t('notificationAdmin.templates.override')}</button>
          )}
          {canManage && (!x.isPlatformDefault || isPlatform) && (
            <button className="text-xs text-gray-600 hover:underline" disabled={toggle.isPending} onClick={() => toggle.mutate(x)}>{x.isActive ? t('common.deactivate') : t('common.activate')}</button>
          )}
          {canManage && !x.isPlatformDefault && (
            <button className="text-xs text-red-600 hover:underline" onClick={() => setDeleting(x)}>{t('notificationAdmin.templates.revert')}</button>
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
          <p className="text-sm text-gray-500">{t('notificationAdmin.templates.hint')}</p>
        </div>
        {canManage && <button className="btn-primary" onClick={() => setEditor({ open: true, template: null })}>{t('notificationAdmin.templates.add')}</button>}
      </div>

      {message && (
        <div className={`rounded border px-3 py-2 text-sm ${message.ok ? 'border-green-200 bg-green-50 text-green-800' : 'border-red-200 bg-red-50 text-red-700'}`} role={message.ok ? 'status' : 'alert'}>{message.text}</div>
      )}

      <DataTable columns={columns} data={data} isLoading={isLoading} />

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
