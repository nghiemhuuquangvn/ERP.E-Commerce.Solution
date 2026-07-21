import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { leadsApi, LEAD_SOURCE, type Lead } from '@/api/crm/leads.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

// ─── Create modal ───────────────────────────────────────────────────────────

interface CreateFormValues {
  leadName: string
  leadSource: string
  estimatedValue: string
  contactId: string
}

function CreateLeadModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: contactsPage } = useQuery({
    queryKey: ['crm', 'contacts', 'picker'],
    queryFn: () => contactsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<CreateFormValues>({
    defaultValues: { leadName: '', leadSource: String(LEAD_SOURCE.Website), estimatedValue: '', contactId: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: CreateFormValues) =>
      leadsApi.create({
        leadName: values.leadName,
        leadSource: Number(values.leadSource),
        estimatedValue: values.estimatedValue.trim() ? Number(values.estimatedValue) : null,
        contactId: values.contactId.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('crm.leads.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.leads.leadName')}</label>
            <input className="input" {...register('leadName', { required: true })} />
            {errors.leadName && <p className="mt-1 text-xs text-red-600">{t('crm.leads.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.leads.source')}</label>
            <select className="input" {...register('leadSource')}>
              {Object.entries(LEAD_SOURCE).map(([label, value]) => (
                <option key={value} value={value}>
                  {t(`crm.leads.sources.${label}`)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.leads.estimatedValue')}</label>
            <input type="number" step="0.01" className="input" {...register('estimatedValue')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.leads.contactOptional')}</label>
            <select className="input" {...register('contactId')}>
              <option value="">{t('common.none')}</option>
              {(contactsPage?.items ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.leads.failedToSave')}</p>}
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

// ─── Lose modal ─────────────────────────────────────────────────────────────

function LoseLeadModal({ lead, onClose }: { lead: Lead; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<{ reason: string }>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { reason: string }) => leadsApi.lose(lead.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('crm.leads.loseTitle', { name: lead.leadName })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.leads.reason')}</label>
            <textarea className="input" rows={3} {...register('reason', { required: true })} />
            {errors.reason && <p className="mt-1 text-xs text-red-600">{t('crm.leads.required')}</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.leads.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-danger">
              {t('crm.leads.markLost')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Tab ─────────────────────────────────────────────────────────────────────

export function LeadsTab() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [statusFilter, setStatusFilter] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [loseTarget, setLoseTarget] = useState<Lead | null>(null)
  const queryClient = useQueryClient()

  const STATUS_OPTIONS = [
    { value: '', label: t('crm.leads.statusOptions.all') },
    { value: '0', label: t('crm.leads.statusOptions.new') },
    { value: '1', label: t('crm.leads.statusOptions.contacted') },
    { value: '2', label: t('crm.leads.statusOptions.qualified') },
    { value: '3', label: t('crm.leads.statusOptions.lost') },
  ]

  const { data, isLoading } = useQuery({
    queryKey: ['crm', 'leads', { page, pageSize, statusFilter }],
    queryFn: () => leadsApi.list({ page, pageSize, status: statusFilter ? Number(statusFilter) : undefined }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('crm:leads:manage')

  const contactMutation = useMutation({
    mutationFn: (id: string) => leadsApi.contact(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] }),
  })

  const qualifyMutation = useMutation({
    mutationFn: (id: string) => leadsApi.qualify(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] }),
  })

  const columns = useMemo(
    () => [
      { key: 'leadName', label: t('crm.leads.leadName') },
      { key: 'leadSource', label: t('crm.leads.source') },
      {
        key: 'estimatedValue',
        label: t('crm.leads.estimatedValue'),
        render: (l: Lead) => l.estimatedValue ?? '—',
      },
      {
        key: 'leadStatus',
        label: t('common.status'),
        render: (l: Lead) => <StatusBadge status={l.leadStatus} />,
      },
      {
        key: 'actions',
        label: '',
        render: (l: Lead) =>
          canManage && l.leadStatus !== 'Lost' && l.leadStatus !== 'Qualified' ? (
            <div className="flex gap-3">
              {l.leadStatus === 'New' && (
                <button
                  className="text-xs text-primary-600 hover:underline"
                  onClick={() => contactMutation.mutate(l.id)}
                >
                  {t('crm.leads.markContacted')}
                </button>
              )}
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => qualifyMutation.mutate(l.id)}
              >
                {t('crm.leads.qualify')}
              </button>
              <button className="text-xs text-red-600 hover:underline" onClick={() => setLoseTarget(l)}>
                {t('crm.leads.markLost')}
              </button>
            </div>
          ) : null,
      },
    ],
    [canManage, contactMutation, qualifyMutation, t],
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
          <button className="btn-primary" onClick={() => setShowCreate(true)}>
            {t('crm.leads.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {showCreate && <CreateLeadModal onClose={() => setShowCreate(false)} />}
      {loseTarget && <LoseLeadModal lead={loseTarget} onClose={() => setLoseTarget(null)} />}
    </div>
  )
}
