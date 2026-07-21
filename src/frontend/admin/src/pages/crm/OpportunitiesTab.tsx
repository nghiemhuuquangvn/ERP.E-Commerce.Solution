import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { opportunitiesApi, OPPORTUNITY_STAGE, type Opportunity } from '@/api/crm/opportunities.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { usersApi } from '@/api/users.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

// ─── Create modal ───────────────────────────────────────────────────────────

interface CreateFormValues {
  title: string
  contactId: string
  value: string
  expectedCloseDate: string
  assignedToUserId: string
}

function CreateOpportunityModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data: contactsPage } = useQuery({
    queryKey: ['crm', 'contacts', 'picker'],
    queryFn: () => contactsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })
  const { data: usersPage } = useQuery({
    queryKey: ['crm', 'users', 'picker'],
    queryFn: () => usersApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<CreateFormValues>({
    defaultValues: {
      title: '',
      contactId: '',
      value: '',
      expectedCloseDate: new Date().toISOString().slice(0, 10),
      assignedToUserId: '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CreateFormValues) =>
      opportunitiesApi.create({
        title: values.title,
        contactId: values.contactId,
        value: Number(values.value),
        expectedCloseDate: values.expectedCloseDate,
        assignedToUserId: values.assignedToUserId.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('crm.opportunities.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.opportunities.name')}</label>
            <input className="input" {...register('title', { required: true })} />
            {errors.title && <p className="mt-1 text-xs text-red-600">{t('crm.opportunities.required')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.opportunities.contact')}</label>
            <select className="input" {...register('contactId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {(contactsPage?.items ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
            {errors.contactId && <p className="mt-1 text-xs text-red-600">{t('crm.opportunities.required')}</p>}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.opportunities.value')}</label>
              <input type="number" step="0.01" className="input" {...register('value', { required: true })} />
              {errors.value && <p className="mt-1 text-xs text-red-600">{t('crm.opportunities.required')}</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('crm.opportunities.expectedCloseDate')}
              </label>
              <input type="date" className="input" {...register('expectedCloseDate', { required: true })} />
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('crm.opportunities.assignedToOptional')}
            </label>
            <select className="input" {...register('assignedToUserId')}>
              <option value="">{t('common.none')}</option>
              {(usersPage?.items ?? []).map((u) => (
                <option key={u.id} value={u.id}>
                  {u.fullName ?? u.email}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.opportunities.failedToSave')}</p>}
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

function LoseOpportunityModal({ opportunity, onClose }: { opportunity: Opportunity; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<{ reason: string }>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { reason: string }) => opportunitiesApi.lose(opportunity.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {t('crm.opportunities.loseTitle', { name: opportunity.opportunityName })}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('crm.opportunities.reason')}</label>
            <textarea className="input" rows={3} {...register('reason', { required: true })} />
            {errors.reason && <p className="mt-1 text-xs text-red-600">{t('crm.opportunities.required')}</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('crm.opportunities.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-danger">
              {t('crm.opportunities.markLost')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Tab ─────────────────────────────────────────────────────────────────────

export function OpportunitiesTab() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [stageFilter, setStageFilter] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [loseTarget, setLoseTarget] = useState<Opportunity | null>(null)
  const queryClient = useQueryClient()

  const STAGE_OPTIONS = [
    { value: '', label: t('crm.opportunities.stageOptions.all') },
    ...Object.entries(OPPORTUNITY_STAGE).map(([label, value]) => ({
      value: String(value),
      label: t(`crm.opportunities.stages.${label}`),
    })),
  ]

  const { data: summary } = useQuery({
    queryKey: ['crm', 'opportunities', 'pipeline-summary'],
    queryFn: () => opportunitiesApi.pipelineSummary(),
    staleTime: 30_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['crm', 'opportunities', { page, pageSize, stageFilter }],
    queryFn: () => opportunitiesApi.list({ page, pageSize, stage: stageFilter ? Number(stageFilter) : undefined }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('crm:opportunities:manage')

  const advanceMutation = useMutation({
    mutationFn: (id: string) => opportunitiesApi.advance(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] }),
  })

  const winMutation = useMutation({
    mutationFn: (id: string) => opportunitiesApi.win(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] }),
  })

  const columns = useMemo(
    () => [
      { key: 'opportunityName', label: t('crm.opportunities.name') },
      { key: 'stage', label: t('crm.opportunities.stage'), render: (o: Opportunity) => <StatusBadge status={o.stage} /> },
      { key: 'probabilityPercent', label: t('crm.opportunities.probability'), render: (o: Opportunity) => `${o.probabilityPercent}%` },
      { key: 'estimatedValue', label: t('crm.opportunities.value'), render: (o: Opportunity) => o.estimatedValue ?? '—' },
      {
        key: 'expectedCloseDate',
        label: t('crm.opportunities.expectedCloseDate'),
        render: (o: Opportunity) => (o.expectedCloseDate ? o.expectedCloseDate.slice(0, 10) : '—'),
      },
      {
        key: 'actions',
        label: '',
        render: (o: Opportunity) =>
          canManage && o.stage !== 'Won' && o.stage !== 'Lost' ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => advanceMutation.mutate(o.id)}
              >
                {t('crm.opportunities.advance')}
              </button>
              <button className="text-xs text-green-600 hover:underline" onClick={() => winMutation.mutate(o.id)}>
                {t('crm.opportunities.win')}
              </button>
              <button className="text-xs text-red-600 hover:underline" onClick={() => setLoseTarget(o)}>
                {t('crm.opportunities.markLost')}
              </button>
            </div>
          ) : null,
      },
    ],
    [advanceMutation, canManage, t, winMutation],
  )

  return (
    <div className="space-y-4">
      {summary && (
        <div className="grid grid-cols-3 gap-3 sm:grid-cols-6">
          {(['prospecting', 'qualification', 'proposal', 'negotiation', 'won', 'lost'] as const).map((stage) => (
            <div key={stage} className="card p-3 text-center">
              <p className="text-xs uppercase text-gray-500">{t(`crm.opportunities.stages.${stage[0].toUpperCase()}${stage.slice(1)}`)}</p>
              <p className="text-lg font-semibold text-gray-900">{summary[stage]}</p>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-center justify-between">
        <select
          className="input max-w-xs"
          value={stageFilter}
          onChange={(e) => {
            setStageFilter(e.target.value)
            setPage(1)
          }}
        >
          {STAGE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        {canManage && (
          <button className="btn-primary" onClick={() => setShowCreate(true)}>
            {t('crm.opportunities.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {showCreate && <CreateOpportunityModal onClose={() => setShowCreate(false)} />}
      {loseTarget && <LoseOpportunityModal opportunity={loseTarget} onClose={() => setLoseTarget(null)} />}
    </div>
  )
}
