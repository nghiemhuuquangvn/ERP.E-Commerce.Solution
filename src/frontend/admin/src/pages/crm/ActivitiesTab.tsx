import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CheckSquare, Mail, Phone, StickyNote, Users } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { activitiesApi, ACTIVITY_TYPE, type Activity } from '@/api/crm/activities.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const TYPE_ICON: Record<string, LucideIcon> = { Call: Phone, Email: Mail, Meeting: Users, Note: StickyNote, Task: CheckSquare }

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.activities.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-activity-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="create-activity-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.activities.contact')} required error={errors.contactId ? t('common.required') : undefined}>
          <Select {...register('contactId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(contactsPage?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('crm.activities.type')}>
          <Select {...register('activityType')}>
            {Object.entries(ACTIVITY_TYPE).map(([label, value]) => (
              <option key={value} value={value}>
                {t(`crm.activities.types.${label}`)}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('crm.activities.subject')} required error={errors.subject ? t('common.required') : undefined}>
          <Input {...register('subject', { required: true })} />
        </FormField>
        <FormField label={t('crm.activities.notes')}>
          <Textarea rows={3} {...register('notes')} />
        </FormField>
        <FormField label={t('crm.activities.scheduledAtOptional')}>
          <Input type="datetime-local" {...register('scheduledAt')} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('crm.activities.opportunityIdOptional')}>
            <Input {...register('opportunityId')} />
          </FormField>
          <FormField label={t('crm.activities.caseIdOptional')}>
            <Input {...register('caseId')} />
          </FormField>
        </div>
        <p className="-mt-2 text-xs text-ink-secondary">{t('crm.activities.idHint')}</p>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.activities.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Complete modal ─────────────────────────────────────────────────────────

function CompleteActivityModal({ activity, onClose }: { activity: Activity; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit } = useForm<{ notes: string }>({
    defaultValues: { notes: activity.notes ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { notes: string }) =>
      activitiesApi.complete(activity.id, { notes: values.notes.trim() || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'activities'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.activities.completeTitle', { subject: activity.subject })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="complete-activity-form" loading={mutation.isPending}>{t('crm.activities.complete')}</Button>
        </>
      }
    >
      <form id="complete-activity-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.activities.notes')}>
          <Textarea rows={3} {...register('notes')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.activities.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const [cancelTarget, setCancelTarget] = useState<Activity | null>(null)
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['crm', 'activities', { page, pageSize, typeFilter }],
    queryFn: () =>
      activitiesApi.list({ page, pageSize, activityType: typeFilter ? Number(typeFilter) : undefined }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('crm:activities:manage')

  const cancelMutation = useMutation({
    mutationFn: (id: string) => activitiesApi.cancel(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'activities'] })
      toast.success(t('common.saved'))
      setCancelTarget(null)
    },
    onError: (e) => {
      setCancelTarget(null)
      toast.error(serverMessage(e, t('crm.activities.failedToSave')))
    },
  })

  const columns = useMemo(
    () => [
      {
        key: 'activityType',
        label: t('crm.activities.type'),
        render: (a: Activity) => {
          const Icon = TYPE_ICON[a.activityType]
          return (
            <span className="inline-flex items-center gap-2">
              {Icon && <Icon size={16} strokeWidth={1.75} aria-hidden="true" className="text-ink-secondary" />}
              {t(`crm.activities.types.${a.activityType}`, { defaultValue: a.activityType })}
            </span>
          )
        },
      },
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
              <Button variant="link" className="text-xs"
                onClick={() => setCompleteTarget(a)}>
                {t('crm.activities.complete')}
              </Button>
              <Button variant="link-danger" className="text-xs" onClick={() => setCancelTarget(a)}>
                {t('crm.activities.cancel')}
              </Button>
            </div>
          ) : null,
      },
    ],
    [cancelMutation, canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Select
          className="max-w-xs"
          aria-label={t('crm.activities.type')}
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
        </Select>
        {canManage && (
          <Button onClick={() => setShowCreate(true)}>
            {t('crm.activities.add')}
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          typeFilter ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setTypeFilter(''); setPage(1) }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {showCreate && <CreateActivityModal onClose={() => setShowCreate(false)} />}
      {completeTarget && <CompleteActivityModal activity={completeTarget} onClose={() => setCompleteTarget(null)} />}

      <ConfirmDialog
        open={!!cancelTarget}
        title={t('crm.activities.cancelTitle')}
        message={t('crm.activities.cancelConfirm', { subject: cancelTarget?.subject })}
        confirmLabel={t('crm.activities.cancelTitle')}
        danger
        onConfirm={() => cancelTarget && cancelMutation.mutate(cancelTarget.id)}
        onCancel={() => setCancelTarget(null)}
      />
    </div>
  )
}
