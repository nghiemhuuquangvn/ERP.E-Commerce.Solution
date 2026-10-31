import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { businessUnitsApi } from '@/api/masterdata/businessUnits.api'
import type { BusinessUnit } from '@/api/masterdata/businessUnits.api'
import { companiesApi } from '@/api/masterdata/companies.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface BusinessUnitFormValues {
  companyId: string
  name: string
  code: string
}

function BusinessUnitModal({
  businessUnit,
  companies,
  defaultCompanyId,
  onClose,
}: {
  businessUnit: BusinessUnit | null
  companies: { id: string; name: string }[]
  defaultCompanyId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!businessUnit

  const { register, handleSubmit, formState: { errors } } = useForm<BusinessUnitFormValues>({
    defaultValues: {
      companyId: businessUnit?.companyId ?? defaultCompanyId,
      name: businessUnit?.name ?? '',
      code: businessUnit?.code ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: BusinessUnitFormValues) => {
      if (isEdit) {
        return businessUnitsApi.update(businessUnit!.id, { name: values.name, code: values.code })
      }
      return businessUnitsApi.create({ companyId: values.companyId, name: values.name, code: values.code })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'businessUnits'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.businessUnits.editTitle') : t('masterdata.businessUnits.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="business-unit-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="business-unit-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('masterdata.businessUnits.company')} required error={errors.companyId ? t('common.required') : undefined}>
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
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.businessUnits.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function BusinessUnitsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [companyId, setCompanyId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; businessUnit: BusinessUnit | null }>({
    open: false,
    businessUnit: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<BusinessUnit | null>(null)

  const { data: companiesPage } = useQuery({
    queryKey: ['masterdata', 'companies', 'picker'],
    queryFn: () => companiesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'businessUnits', { companyId, page, pageSize }],
    queryFn: () => businessUnitsApi.list({ companyId: companyId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:business-units:manage')

  const toggleMutation = useMutation({
    mutationFn: (bu: BusinessUnit) =>
      bu.isActive ? businessUnitsApi.deactivate(bu.id) : businessUnitsApi.activate(bu.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'businessUnits'] })
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
        label: t('masterdata.businessUnits.company'),
        render: (bu: BusinessUnit) => companies.find((c) => c.id === bu.companyId)?.name ?? bu.companyId,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (bu: BusinessUnit) => (
          <StatusBadge
            status={bu.isActive ? 'Active' : 'Inactive'}
            label={bu.isActive ? t('masterdata.businessUnits.active') : t('masterdata.businessUnits.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (bu: BusinessUnit) =>
          canManage ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, businessUnit: bu })}>
                {t('common.edit')}
              </Button>
              {bu.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(bu)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(bu)}>
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
        <h1 className="text-xl font-semibold">{t('masterdata.businessUnits.title')}</h1>
        {canManage && (
          <Button onClick={() => setModalState({ open: true, businessUnit: null })}>
            {t('masterdata.businessUnits.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('masterdata.businessUnits.allCompanies')}
        value={companyId}
        onChange={(e) => {
          setCompanyId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.businessUnits.allCompanies')}</option>
        {companies.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </Select>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <BusinessUnitModal
          businessUnit={modalState.businessUnit}
          companies={companies}
          defaultCompanyId={companyId}
          onClose={() => setModalState({ open: false, businessUnit: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.businessUnits.deactivateTitle')}
        message={t('masterdata.businessUnits.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
