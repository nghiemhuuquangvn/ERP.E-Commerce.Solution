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
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

/** yyyy-mm-dd, `days` days from today (the server only accepts an expected close date in the future). */
const dayFromToday = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10)

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
  const toast = useToast()
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
      expectedCloseDate: dayFromToday(30),
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.opportunities.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-opportunity-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="create-opportunity-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.opportunities.name')} required error={errors.title ? t('common.required') : undefined}>
          <Input {...register('title', { required: true })} />
        </FormField>
        <FormField label={t('crm.opportunities.contact')} required error={errors.contactId ? t('common.required') : undefined}>
          <Select {...register('contactId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(contactsPage?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label={t('crm.opportunities.value')} required error={errors.value ? t('common.required') : undefined}>
            <Input type="number" step="0.01" {...register('value', { required: true })} />
          </FormField>
          <FormField
            label={t('crm.opportunities.expectedCloseDate')}
            required
            error={errors.expectedCloseDate ? (errors.expectedCloseDate.type === 'validate' ? t('crm.opportunities.closeDateFuture') : t('common.required')) : undefined}
          >
            <Input
              type="date"
              min={dayFromToday(1)}
              {...register('expectedCloseDate', { required: true, validate: (v) => v >= dayFromToday(1) })}
            />
          </FormField>
        </div>
        <FormField label={t('crm.opportunities.assignedToOptional')}>
          <Select {...register('assignedToUserId')}>
            <option value="">{t('common.none')}</option>
            {(usersPage?.items ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName ?? u.email}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.opportunities.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Lose modal ─────────────────────────────────────────────────────────────

function LoseOpportunityModal({ opportunity, onClose }: { opportunity: Opportunity; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ reason: string }>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { reason: string }) => opportunitiesApi.lose(opportunity.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.opportunities.loseTitle', { name: opportunity.opportunityName })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="lose-opportunity-form" variant="danger" loading={mutation.isPending}>{t('crm.opportunities.markLost')}</Button>
        </>
      }
    >
      <form id="lose-opportunity-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.opportunities.reason')} required error={errors.reason ? t('common.required') : undefined}>
          <Textarea rows={3} {...register('reason', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.opportunities.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()

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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] })
      toast.success(t('common.saved'))
    },
    onError: (e) => toast.error(serverMessage(e, t('crm.opportunities.failedToSave'))),
  })

  const winMutation = useMutation({
    mutationFn: (id: string) => opportunitiesApi.win(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] })
      toast.success(t('common.saved'))
    },
    onError: (e) => toast.error(serverMessage(e, t('crm.opportunities.failedToSave'))),
  })

  const columns = useMemo(
    () => [
      { key: 'opportunityName', label: t('crm.opportunities.name') },
      { key: 'stage', label: t('crm.opportunities.stage'), render: (o: Opportunity) => <StatusBadge status={o.stage} /> },
      { key: 'probabilityPercent', label: t('crm.opportunities.probability'), align: 'right' as const, render: (o: Opportunity) => `${o.probabilityPercent}%` },
      { key: 'estimatedValue', label: t('crm.opportunities.value'), align: 'right' as const, render: (o: Opportunity) => o.estimatedValue ?? '—' },
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
              <Button variant="link" className="text-xs"
                onClick={() => advanceMutation.mutate(o.id)}>
                {t('crm.opportunities.advance')}
              </Button>
              <Button variant="link" className="text-xs" onClick={() => winMutation.mutate(o.id)}>
                {t('crm.opportunities.win')}
              </Button>
              <Button variant="link-danger" className="text-xs" onClick={() => setLoseTarget(o)}>
                {t('crm.opportunities.markLost')}
              </Button>
            </div>
          ) : null,
      },
    ],
    [advanceMutation, canManage, t, winMutation],
  )

  return (
    <div className="space-y-4">
      {summary && (
        <div className="grid grid-cols-3 gap-3 sm:grid-cols-6" role="group" aria-label={t('crm.opportunities.stage')}>
          {(['prospecting', 'qualification', 'proposal', 'negotiation', 'won', 'lost'] as const).map((stage) => {
            const name = `${stage[0].toUpperCase()}${stage.slice(1)}` as keyof typeof OPPORTUNITY_STAGE
            const value = String(OPPORTUNITY_STAGE[name])
            const active = stageFilter === value
            return (
              <button
                key={stage}
                type="button"
                aria-pressed={active}
                onClick={() => { setStageFilter(active ? '' : value); setPage(1) }}
                className={`card p-3 text-center transition-colors hover:bg-surface-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action ${active ? 'border-brand-action ring-1 ring-brand-action' : ''}`}
              >
                <span className="block text-xs uppercase text-ink-secondary">{t(`crm.opportunities.stages.${name}`)}</span>
                <span className="block text-lg font-semibold text-ink">{summary[stage]}</span>
              </button>
            )
          })}
        </div>
      )}

      <div className="flex items-center justify-between">
        <Select
          className="max-w-xs"
          aria-label={t('crm.opportunities.stage')}
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
        </Select>
        {canManage && (
          <Button onClick={() => setShowCreate(true)}>
            {t('crm.opportunities.add')}
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          stageFilter ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setStageFilter(''); setPage(1) }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {showCreate && <CreateOpportunityModal onClose={() => setShowCreate(false)} />}
      {loseTarget && <LoseOpportunityModal opportunity={loseTarget} onClose={() => setLoseTarget(null)} />}
    </div>
  )
}
