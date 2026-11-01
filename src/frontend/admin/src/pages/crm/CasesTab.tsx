import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { casesApi, CASE_PRIORITY, type CrmCase } from '@/api/crm/cases.api'
import { contactsApi } from '@/api/crm/contacts.api'
import { usersApi } from '@/api/users.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Badge, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import type { BadgeVariant } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const PRIORITY_VARIANT: Record<string, BadgeVariant> = { Low: 'muted', Medium: 'info', High: 'warning', Critical: 'danger' }

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.cases.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="open-case-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="open-case-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.cases.subject')} required error={errors.title ? t('common.required') : undefined}>
          <Input {...register('title', { required: true })} />
        </FormField>
        <FormField label={t('crm.cases.contact')} required error={errors.contactId ? t('common.required') : undefined}>
          <Select {...register('contactId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(contactsPage?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('crm.cases.description')} required error={errors.description ? t('common.required') : undefined}>
          <Textarea rows={3} {...register('description', { required: true })} />
        </FormField>
        <FormField label={t('crm.cases.priority')}>
          <Select {...register('priority')}>
            {Object.entries(CASE_PRIORITY).map(([label, value]) => (
              <option key={value} value={value}>
                {t(`crm.cases.priorities.${label}`)}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.cases.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Assign modal ───────────────────────────────────────────────────────────

function AssignCaseModal({ crmCase, onClose }: { crmCase: CrmCase; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t(crmCase.assignedToUserId ? 'crm.cases.reassignTitle' : 'crm.cases.assignTitle', { number: crmCase.caseNumber })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="assign-case-form" loading={mutation.isPending}>{t('crm.cases.assign')}</Button>
        </>
      }
    >
      <form id="assign-case-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.cases.assignee')} required error={errors.assignedToUserId ? t('common.required') : undefined}>
          <Select {...register('assignedToUserId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {(usersPage?.items ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName ?? u.email}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.cases.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

// ─── Resolve modal ──────────────────────────────────────────────────────────

function ResolveCaseModal({ crmCase, onClose }: { crmCase: CrmCase; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ resolution: string }>({
    defaultValues: { resolution: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: { resolution: string }) => casesApi.resolve(crmCase.id, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'cases'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('crm.cases.resolveTitle', { number: crmCase.caseNumber })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="resolve-case-form" loading={mutation.isPending}>{t('crm.cases.resolve')}</Button>
        </>
      }
    >
      <form id="resolve-case-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('crm.cases.resolution')} required error={errors.resolution ? t('common.required') : undefined}>
          <Textarea rows={3} {...register('resolution', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('crm.cases.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const [closeTarget, setCloseTarget] = useState<CrmCase | null>(null)
  const queryClient = useQueryClient()
  const toast = useToast()

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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['crm', 'cases'] })
      toast.success(t('common.saved'))
      setCloseTarget(null)
    },
    onError: (e) => {
      setCloseTarget(null)
      toast.error(serverMessage(e, t('crm.cases.failedToSave')))
    },
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
      {
        key: 'priority',
        label: t('crm.cases.priority'),
        render: (c: CrmCase) => <Badge variant={PRIORITY_VARIANT[c.priority] ?? 'muted'}>{t(`crm.cases.priorities.${c.priority}`, { defaultValue: c.priority })}</Badge>,
      },
      { key: 'status', label: t('common.status'), render: (c: CrmCase) => <StatusBadge status={c.status} /> },
      {
        key: 'actions',
        label: '',
        render: (c: CrmCase) =>
          canManage && c.status !== 'Closed' ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs" onClick={() => openModal('assign', c)}>
                {t('crm.cases.assign')}
              </Button>
              {c.status !== 'Resolved' && (
                <Button variant="link" className="text-xs" onClick={() => openModal('resolve', c)}>
                  {t('crm.cases.resolve')}
                </Button>
              )}
              {c.status === 'Resolved' && (
                <Button variant="link" className="text-xs" onClick={() => setCloseTarget(c)}>
                  {t('crm.cases.close')}
                </Button>
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
          <Button onClick={() => setModal('open')}>
            {t('crm.cases.add')}
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

      {modal === 'open' && <OpenCaseModal onClose={closeModal} />}
      {modal === 'assign' && activeCase && <AssignCaseModal crmCase={activeCase} onClose={closeModal} />}
      {modal === 'resolve' && activeCase && <ResolveCaseModal crmCase={activeCase} onClose={closeModal} />}

      <ConfirmDialog
        open={!!closeTarget}
        title={t('crm.cases.closeTitle')}
        message={t('crm.cases.closeConfirm', { number: closeTarget?.caseNumber })}
        confirmLabel={t('crm.cases.closeTitle')}
        danger
        onConfirm={() => closeTarget && closeMutation.mutate(closeTarget.id)}
        onCancel={() => setCloseTarget(null)}
      />
    </div>
  )
}
