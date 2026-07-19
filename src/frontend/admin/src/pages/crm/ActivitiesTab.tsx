import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { activitiesApi, ACTIVITY_TYPE, type Activity } from '@/api/crm/activities.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

// ─── Create modal ───────────────────────────────────────────────────────────

interface CreateFormValues {
  contactId: string
  activityType: string
  subject: string
  notes: string
  scheduledAt: string
  opportunityId: string
  caseId: string
}

function CreateActivityModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: contactsPage } = useQuery({
    queryKey: ['crm', 'contacts', 'picker'],
    queryFn: () => contactsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<CreateFormValues>({
    defaultValues: {
      contactId: '',
      activityType: String(ACTIVITY_TYPE.Call),
      subject: '',
      notes: '',
      scheduledAt: '',
      opportunityId: '',
      caseId: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CreateFormValues) =>
      activitiesApi.create({
        contactId: values.contactId,
        activityType: Number(values.activityType),
        subject: values.subject,
        notes: values.notes.trim() || null,
        scheduledAt: values.scheduledAt || null,
        opportunityId: values.opportunityId.trim() || null,
        caseId: values.caseId.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'activities'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('crm.activities.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.contact')}</label>
            <select className="input" {...register('contactId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(contactsPage?.items ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
            {errors.contactId && <p className="mt-1 text-xs text-red-600">{t('crm.activities.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.type')}</label>
            <select className="input" {...register('activityType')}>
              {Object.entries(ACTIVITY_TYPE).map(([label, value]) => (
                <option key={value} value={value}>
                  {t(`crm.activities.types.${label}`)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.subject')}</label>
            <input className="input" {...register('subject', { required: true })} />
            {errors.subject && <p className="mt-1 text-xs text-red-600">{t('crm.activities.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.notes')}</label>
            <textarea className="input" rows={3} {...register('notes')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.scheduledAtOptional')}</label>
            <input type="datetime-local" className="input" {...register('scheduledAt')} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('crm.activities.opportunityIdOptional')}
              </label>
              <input className="input" {...register('opportunityId')} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.caseIdOptional')}</label>
              <input className="input" {...register('caseId')} />
            </div>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.activities.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Complete modal ─────────────────────────────────────────────────────────

function CompleteActivityModal({ activity, onClose }: { activity: Activity; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<{ notes: string }>({
    defaultValues: { notes: activity.notes ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { notes: string }) =>
      activitiesApi.complete(activity.id, { notes: values.notes.trim() || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'activities'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('crm.activities.completeTitle', { subject: activity.subject })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.activities.notes')}</label>
            <textarea className="input" rows={3} {...register('notes')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.activities.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('crm.activities.complete')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Tab ─────────────────────────────────────────────────────────────────────

export function ActivitiesTab() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [typeFilter, setTypeFilter] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [completeTarget, setCompleteTarget] = useState<Activity | null>(null)
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['crm', 'activities', { page, pageSize, typeFilter }],
    queryFn: () =>
      activitiesApi.list({ page, pageSize, activityType: typeFilter ? Number(typeFilter) : undefined }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('crm:activities:manage')

  const cancelMutation = useMutation({
    mutationFn: (id: string) => activitiesApi.cancel(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'activities'] }),
  })

  const columns = useMemo(
    () => [
      { key: 'activityType', label: t('crm.activities.type') },
      { key: 'subject', label: t('crm.activities.subject') },
      {
        key: 'scheduledAt',
        label: t('crm.activities.scheduledAtOptional'),
        render: (a: Activity) => (a.scheduledAt ? new Date(a.scheduledAt).toLocaleString() : '—'),
      },
      { key: 'status', label: t('common.status'), render: (a: Activity) => <StatusBadge status={a.status} /> },
      {
        key: 'actions',
        label: '',
        render: (a: Activity) =>
          canManage && a.status === 'Scheduled' ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setCompleteTarget(a)}
              >
                {t('crm.activities.complete')}
              </button>
              <button className="text-xs text-red-600 hover:underline" onClick={() => cancelMutation.mutate(a.id)}>
                {t('crm.activities.cancel')}
              </button>
            </div>
          ) : null,
      },
    ],
    [cancelMutation, canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <select
          className="input max-w-xs"
          value={typeFilter}
          onChange={(e) => {
            setTypeFilter(e.target.value)
            setPage(1)
          }}
        >
          <option value="">{t('crm.activities.typeOptions.all')}</option>
          {Object.entries(ACTIVITY_TYPE).map(([label, value]) => (
            <option key={value} value={value}>
              {t(`crm.activities.types.${label}`)}
            </option>
          ))}
        </select>
        {canManage && (
          <button className="btn-primary" onClick={() => setShowCreate(true)}>
            {t('crm.activities.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {showCreate && <CreateActivityModal onClose={() => setShowCreate(false)} />}
      {completeTarget && <CompleteActivityModal activity={completeTarget} onClose={() => setCompleteTarget(null)} />}
    </div>
  )
}
