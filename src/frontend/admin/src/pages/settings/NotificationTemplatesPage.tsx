import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { notificationAdminApi } from '@/api/notifications/notificationAdmin.api'
import type { NotificationTemplate } from '@/api/notifications/notificationAdmin.api'
import { DataTable } from '@/components/shared/DataTable'

function PreviewModal({ template, onClose }: { template: NotificationTemplate; onClose: () => void }) {
  const { t } = useTranslation()
  const variables = (template.variables ?? '').split(',').map((v) => v.trim()).filter(Boolean)
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-xl overflow-y-auto p-6 text-sm">
        <h3 className="text-base font-semibold text-gray-900">{template.name}</h3>
        <p className="mb-3 font-mono text-xs text-gray-500">{template.code} · {template.channel}</p>
        <div className="text-xs font-semibold uppercase tracking-wide text-gray-500">{t('notificationAdmin.subject')}</div>
        <p className="mb-3 rounded-md bg-gray-50 p-3 text-gray-800">{template.subject}</p>
        <div className="text-xs font-semibold uppercase tracking-wide text-gray-500">{t('notificationAdmin.body')}</div>
        <pre className="mb-3 whitespace-pre-wrap rounded-md bg-gray-50 p-3 text-gray-800">{template.body}</pre>
        {variables.length > 0 && (
          <p className="text-xs text-gray-600">
            {t('notificationAdmin.variables')}: {variables.map((v) => `{${v}}`).join('  ')}
          </p>
        )}
        <div className="mt-4 flex justify-end">
          <button className="btn-outline" onClick={onClose}>{t('common.close')}</button>
        </div>
      </div>
    </div>
  )
}

export function NotificationTemplatesPage() {
  const { t } = useTranslation()
  const [preview, setPreview] = useState<NotificationTemplate | null>(null)
  const { data = [], isLoading } = useQuery({ queryKey: ['notifications', 'templates'], queryFn: notificationAdminApi.templates, staleTime: 60_000 })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('notificationAdmin.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('notificationAdmin.name') },
      { key: 'channel', label: t('notificationAdmin.channel') },
      { key: 'subject', label: t('notificationAdmin.subject') },
      {
        key: 'origin',
        label: t('notificationAdmin.origin'),
        render: (x: NotificationTemplate) => (
          <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${x.isPlatformDefault ? 'bg-gray-100 text-gray-600' : 'bg-blue-100 text-blue-700'}`}>
            {x.isPlatformDefault ? t('notificationAdmin.platformDefault') : t('notificationAdmin.tenantOverride')}
          </span>
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (x: NotificationTemplate) => (
          <button className="text-xs text-blue-600 hover:underline" onClick={() => setPreview(x)}>{t('notificationAdmin.view')}</button>
        ),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{t('notificationAdmin.templatesTitle')}</h1>
        <p className="text-sm text-gray-500">{t('notificationAdmin.templatesHint')}</p>
      </div>
      <DataTable columns={columns} data={data} isLoading={isLoading} />
      {preview && <PreviewModal template={preview} onClose={() => setPreview(null)} />}
    </div>
  )
}
