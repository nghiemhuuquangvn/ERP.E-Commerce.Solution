import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { casesApi, CASE_PRIORITY, type CrmCase } from '@/api/crm/cases.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { usersApi } from '@/api/users.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

// ─── Open case modal ────────────────────────────────────────────────────────

interface OpenFormValues {
  title: string
  contactId: string
  description: string
  priority: string
}

function OpenCaseModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: contactsPage } = useQuery({
    queryKey: ['crm', 'contacts', 'picker'],
    queryFn: () => contactsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<OpenFormValues>({
    defaultValues: { title: '', contactId: '', description: '', priority: String(CASE_PRIORITY.Medium) },
  })

  const mutation = useMutation({
    mutationFn: (values: OpenFormValues) =>
      casesApi.open({
        title: values.title,
        contactId: values.contactId,
        description: values.description,
        priority: Number(values.priority),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'cases'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('crm.cases.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.cases.subject')}</label>
            <input className="input" {...register('title', { required: true })} />
            {errors.title && <p className="mt-1 text-xs text-red-600">{t('crm.cases.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.cases.contact')}</label>
            <select className="input" {...register('contactId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(contactsPage?.items ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
            {errors.contactId && <p className="mt-1 text-xs text-red-600">{t('crm.cases.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.cases.description')}</label>
            <textarea className="input" rows={3} {...register('description', { required: true })} />
            {errors.description && <p className="mt-1 text-xs text-red-600">{t('crm.cases.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.cases.priority')}</label>
            <select className="input" {...register('priority')}>
              {Object.entries(CASE_PRIORITY).map(([label, value]) => (
                <option key={value} value={value}>
                  {t(`crm.cases.priorities.${label}`)}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.cases.failedToSave')}</p>}
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

// ─── Assign modal ───────────────────────────────────────────────────────────

function AssignCaseModal({ crmCase, onClose }: { crmCase: CrmCase; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: usersPage } = useQuery({
    queryKey: ['crm', 'users', 'picker'],
    queryFn: () => usersApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })
  const { register, handleSubmit, formState: { errors } } = useForm<{ assignedToUserId: string }>({
    defaultValues: { assignedToUserId: crmCase.assignedToUserId ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { assignedToUserId: string }) => casesApi.assign(crmCase.id, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'cases'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('crm.cases.assignTitle', { number: crmCase.caseNumber })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.cases.assignee')}</label>
            <select className="input" {...register('assignedToUserId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(usersPage?.items ?? []).map((u) => (
                <option key={u.id} value={u.id}>
                  {u.fullName ?? u.email}
                </option>
              ))}
            </select>
            {errors.assignedToUserId && <p className="mt-1 text-xs text-red-600">{t('crm.cases.required')}</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.cases.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('crm.cases.assign')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Resolve modal ──────────────────────────────────────────────────────────

function ResolveCaseModal({ crmCase, onClose }: { crmCase: CrmCase; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<{ resolution: string }>({
    defaultValues: { resolution: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { resolution: string }) => casesApi.resolve(crmCase.id, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'cases'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('crm.cases.resolveTitle', { number: crmCase.caseNumber })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.cases.resolution')}</label>
            <textarea className="input" rows={3} {...register('resolution', { required: true })} />
            {errors.resolution && <p className="mt-1 text-xs text-red-600">{t('crm.cases.required')}</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.cases.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('crm.cases.resolve')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Tab ─────────────────────────────────────────────────────────────────────

type ModalKind = 'open' | 'assign' | 'resolve' | null

export function CasesTab() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [statusFilter, setStatusFilter] = useState('')
  const [modal, setModal] = useState<ModalKind>(null)
  const [activeCase, setActiveCase] = useState<CrmCase | null>(null)
  const queryClient = useQueryClient()

  const STATUS_OPTIONS = [
    { value: '', label: t('crm.cases.statusOptions.all') },
    { value: '0', label: t('crm.cases.statusOptions.open') },
    { value: '1', label: t('crm.cases.statusOptions.inProgress') },
    { value: '2', label: t('crm.cases.statusOptions.resolved') },
    { value: '3', label: t('crm.cases.statusOptions.closed') },
  ]

  const { data, isLoading } = useQuery({
    queryKey: ['crm', 'cases', { page, pageSize, statusFilter }],
    queryFn: () => casesApi.list({ page, pageSize, status: statusFilter ? Number(statusFilter) : undefined }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('crm:cases:manage')

  const closeMutation = useMutation({
    mutationFn: (id: string) => casesApi.close(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'cases'] }),
  })

  const openModal = (kind: ModalKind, crmCase: CrmCase) => {
    setActiveCase(crmCase)
    setModal(kind)
  }

  const closeModal = () => {
    setModal(null)
    setActiveCase(null)
  }

  const columns = useMemo(
    () => [
      { key: 'caseNumber', label: t('crm.cases.caseNumber'), className: 'font-mono text-xs' },
      { key: 'caseSubject', label: t('crm.cases.subject') },
      { key: 'priority', label: t('crm.cases.priority') },
      { key: 'status', label: t('common.status'), render: (c: CrmCase) => <StatusBadge status={c.status} /> },
      {
        key: 'actions',
        label: '',
        render: (c: CrmCase) =>
          canManage && c.status !== 'Closed' ? (
            <div className="flex gap-3">
              <button className="text-xs text-primary-600 hover:underline" onClick={() => openModal('assign', c)}>
                {t('crm.cases.assign')}
              </button>
              {c.status !== 'Resolved' && (
                <button className="text-xs text-primary-600 hover:underline" onClick={() => openModal('resolve', c)}>
                  {t('crm.cases.resolve')}
                </button>
              )}
              {c.status === 'Resolved' && (
                <button className="text-xs text-green-600 hover:underline" onClick={() => closeMutation.mutate(c.id)}>
                  {t('crm.cases.close')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, closeMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <select
          className="input max-w-xs"
          value={statusFilter}
          onChange={(e) => {
            setStatusFilter(e.target.value)
            setPage(1)
          }}
        >
          {STATUS_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        {canManage && (
          <button className="btn-primary" onClick={() => setModal('open')}>
            {t('crm.cases.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modal === 'open' && <OpenCaseModal onClose={closeModal} />}
      {modal === 'assign' && activeCase && <AssignCaseModal crmCase={activeCase} onClose={closeModal} />}
      {modal === 'resolve' && activeCase && <ResolveCaseModal crmCase={activeCase} onClose={closeModal} />}
    </div>
  )
}
