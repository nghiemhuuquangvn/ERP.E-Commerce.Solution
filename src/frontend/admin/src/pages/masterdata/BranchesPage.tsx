import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { branchesApi } from '@/api/masterdata/branches.api'
import type { Branch } from '@/api/masterdata/branches.api'
import { companiesApi } from '@/api/masterdata/companies.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface BranchFormValues {
  companyId: string
  name: string
  code: string
  address: string
}

function BranchModal({
  branch,
  companies,
  defaultCompanyId,
  onClose,
}: {
  branch: Branch | null
  companies: { id: string; name: string }[]
  defaultCompanyId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!branch

  const { register, handleSubmit, formState: { errors } } = useForm<BranchFormValues>({
    defaultValues: {
      companyId: branch?.companyId ?? defaultCompanyId,
      name: branch?.name ?? '',
      code: branch?.code ?? '',
      address: branch?.address ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: BranchFormValues) => {
      const address = values.address.trim() || null
      if (isEdit) {
        return branchesApi.update(branch!.id, {
          name: values.name,
          code: values.code,
          address,
          wardId: branch!.wardId ?? null,
          managerEmployeeId: branch!.managerEmployeeId ?? null,
        })
      }
      return branchesApi.create({
        companyId: values.companyId,
        name: values.name,
        code: values.code,
        address,
        wardId: null,
        managerEmployeeId: null,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'branches'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.branches.editTitle') : t('masterdata.branches.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="branch-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="branch-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('masterdata.branches.company')} required error={errors.companyId ? t('common.required') : undefined}>
            <Select {...register('companyId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {companies.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.branches.address')}>
          <Input {...register('address')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.branches.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function BranchesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [companyId, setCompanyId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; branch: Branch | null }>({
    open: false,
    branch: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Branch | null>(null)

  const { data: companiesPage } = useQuery({
    queryKey: ['masterdata', 'companies', 'picker'],
    queryFn: () => companiesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'branches', { companyId, page, pageSize }],
    queryFn: () => branchesApi.list({ companyId: companyId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:branches:manage')

  const toggleMutation = useMutation({
    mutationFn: (b: Branch) => (b.isActive ? branchesApi.deactivate(b.id) : branchesApi.activate(b.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'branches'] })
      setConfirmTarget(null)
    },
  })

  const companies = companiesPage?.items ?? []

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'companyId',
        label: t('masterdata.branches.company'),
        render: (b: Branch) => companies.find((c) => c.id === b.companyId)?.name ?? b.companyId,
      },
      { key: 'address', label: t('masterdata.branches.address'), render: (b: Branch) => b.address ?? '—' },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (b: Branch) => (
          <StatusBadge
            status={b.isActive ? 'Active' : 'Inactive'}
            label={b.isActive ? t('masterdata.branches.active') : t('masterdata.branches.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (b: Branch) =>
          canManage ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, branch: b })}>
                {t('common.edit')}
              </Button>
              {b.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(b)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(b)}>
                  {t('common.activate')}
                </Button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, companies, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.branches.title')}</h1>
        {canManage && (
          <Button onClick={() => setModalState({ open: true, branch: null })}>
            {t('masterdata.branches.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('masterdata.branches.allCompanies')}
        value={companyId}
        onChange={(e) => {
          setCompanyId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.branches.allCompanies')}</option>
        {companies.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </Select>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <BranchModal
          branch={modalState.branch}
          companies={companies}
          defaultCompanyId={companyId}
          onClose={() => setModalState({ open: false, branch: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.branches.deactivateTitle')}
        message={t('masterdata.branches.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
