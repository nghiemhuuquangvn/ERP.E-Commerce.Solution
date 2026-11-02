import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { leadsApi, LEAD_SOURCE, type Lead } from '@/api/crm/leads.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Badge, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
  const { data: contactsPage, isSuccess: contactsLoaded } = useQuery({
    queryKey: ['crm', 'contacts', 'picker'],
    queryFn: () => contactsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })

  const { register, handleSubmit, formState: { errors } } = useForm<CreateFormValues>({
    defaultValues: { leadName: '', leadSource: String(LEAD_SOURCE.Website), estimatedValue: '', contactId: '' },
  })

  const noContacts = contactsLoaded && (contactsPage?.items ?? []).length === 0

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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.leads.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-lead-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="create-lead-form" noValidate onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.leads.leadName')} required error={errors.leadName ? t('common.required') : undefined}>
          <Input {...register('leadName', { required: true })} />
        </FormField>
        <FormField label={t('crm.leads.source')}>
          <Select {...register('leadSource')}>
            {Object.entries(LEAD_SOURCE).map(([label, value]) => (
              <option key={value} value={value}>
                {t(`crm.leads.sources.${label}`)}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('crm.leads.estimatedValue')}>
          <Input type="number" step="0.01" {...register('estimatedValue')} />
        </FormField>
        {noContacts && <Alert variant="warning">{t('crm.leads.noContactsYet')}</Alert>}
        <FormField label={t('crm.leads.contact')} required hint={t('crm.leads.contactHint')} error={errors.contactId ? t('common.required') : undefined}>
          <Select {...register('contactId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(contactsPage?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.leads.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Lose modal ─────────────────────────────────────────────────────────────

function LoseLeadModal({ lead, onClose }: { lead: Lead; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ reason: string }>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { reason: string }) => leadsApi.lose(lead.id, { reason: values.reason }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.leads.loseTitle', { name: lead.leadName })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="lose-lead-form" variant="danger" loading={mutation.isPending}>{t('crm.leads.markLost')}</Button>
        </>
      }
    >
      <form id="lose-lead-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.leads.reason')} required error={errors.reason ? t('common.required') : undefined}>
          <Textarea rows={3} {...register('reason', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.leads.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const [qualifyTarget, setQualifyTarget] = useState<Lead | null>(null)
  const queryClient = useQueryClient()
  const toast = useToast()

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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] })
      toast.success(t('common.saved'))
    },
    onError: (e) => toast.error(serverMessage(e, t('crm.leads.failedToSave'))),
  })

  const qualifyMutation = useMutation({
    mutationFn: (id: string) => leadsApi.qualify(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'leads'] })
      queryClient.invalidateQueries({ queryKey: ['crm', 'opportunities'] })
      toast.success(t('common.saved'))
      setQualifyTarget(null)
    },
    onError: (e) => {
      setQualifyTarget(null)
      toast.error(serverMessage(e, t('crm.leads.failedToSave')))
    },
  })

  const columns = useMemo(
    () => [
      { key: 'leadName', label: t('crm.leads.leadName') },
      { key: 'leadSource', label: t('crm.leads.source') },
      {
        key: 'estimatedValue',
        label: t('crm.leads.estimatedValue'),
        align: 'right' as const,
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
                <Button variant="link" className="text-xs"
                  onClick={() => contactMutation.mutate(l.id)}>
                  {t('crm.leads.markContacted')}
                </Button>
              )}
              {l.contactId ? (
                <Button variant="link" className="text-xs"
                  onClick={() => setQualifyTarget(l)}>
                  {t('crm.leads.qualify')}
                </Button>
              ) : (
                <Badge variant="warning">{t('crm.leads.noContactBadge')}</Badge>
              )}
              <Button variant="link-danger" className="text-xs" onClick={() => setLoseTarget(l)}>
                {t('crm.leads.markLost')}
              </Button>
            </div>
          ) : null,
      },
    ],
    [canManage, contactMutation, qualifyMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <Select
          className="max-w-xs"
          aria-label={t('common.status')}
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
        </Select>
        {canManage && (
          <Button onClick={() => setShowCreate(true)}>
            {t('crm.leads.add')}
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          statusFilter ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setStatusFilter(''); setPage(1) }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {showCreate && <CreateLeadModal onClose={() => setShowCreate(false)} />}
      {loseTarget && <LoseLeadModal lead={loseTarget} onClose={() => setLoseTarget(null)} />}

      <ConfirmDialog
        open={!!qualifyTarget}
        title={t('crm.leads.qualifyTitle')}
        message={t('crm.leads.qualifyConfirm', { name: qualifyTarget?.leadName })}
        confirmLabel={t('crm.leads.qualify')}
        onConfirm={() => qualifyTarget && qualifyMutation.mutate(qualifyTarget.id)}
        onCancel={() => setQualifyTarget(null)}
      />
    </div>
  )
}
