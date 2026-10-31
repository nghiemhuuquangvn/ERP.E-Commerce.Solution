import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { costCentersApi } from '@/api/masterdata/costCenters.api'
import type { CostCenter } from '@/api/masterdata/costCenters.api'
import { companiesApi } from '@/api/masterdata/companies.api'
import { departmentsApi } from '@/api/masterdata/departments.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface CostCenterFormValues {
  companyId: string
  name: string
  code: string
  budgetAmount: number
  departmentId: string
}

function CostCenterModal({
  costCenter,
  companies,
  departments,
  defaultCompanyId,
  onClose,
}: {
  costCenter: CostCenter | null
  companies: { id: string; name: string }[]
  departments: { id: string; departmentName: string }[]
  defaultCompanyId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!costCenter

  const { register, handleSubmit, formState: { errors } } = useForm<CostCenterFormValues>({
    defaultValues: {
      companyId: costCenter?.companyId ?? defaultCompanyId,
      name: costCenter?.name ?? '',
      code: costCenter?.code ?? '',
      budgetAmount: costCenter?.budgetAmount ?? 0,
      departmentId: costCenter?.departmentId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CostCenterFormValues) => {
      const departmentId = values.departmentId || null
      const budgetAmount = Number(values.budgetAmount)
      if (isEdit) {
        return costCentersApi.update(costCenter!.id, {
          name: values.name,
          code: values.code,
          budgetAmount,
          departmentId,
        })
      }
      return costCentersApi.create({
        companyId: values.companyId,
        name: values.name,
        code: values.code,
        budgetAmount,
        departmentId,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'costCenters'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.costCenters.editTitle') : t('masterdata.costCenters.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="cost-center-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="cost-center-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('masterdata.costCenters.company')} required error={errors.companyId ? t('common.required') : undefined}>
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
        <FormField label={t('masterdata.costCenters.budgetAmount')} required error={errors.budgetAmount ? t('common.required') : undefined}>
          <Input
            type="number"
            step="0.01"
            {...register('budgetAmount', { required: true, valueAsNumber: true, min: 0 })}
          />
        </FormField>
        <FormField label={t('masterdata.costCenters.departmentOptional')}>
          <Select {...register('departmentId')}>
            <option value="">{t('common.none')}</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.departmentName}
              </option>
            ))}
          </Select>
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.costCenters.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function CostCentersPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [companyId, setCompanyId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; costCenter: CostCenter | null }>({
    open: false,
    costCenter: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<CostCenter | null>(null)

  const { data: companiesPage } = useQuery({
    queryKey: ['masterdata', 'companies', 'picker'],
    queryFn: () => companiesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data: departmentsPage } = useQuery({
    queryKey: ['masterdata', 'departments', 'picker'],
    queryFn: () => departmentsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'costCenters', { companyId, page, pageSize }],
    queryFn: () => costCentersApi.list({ companyId: companyId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:cost-centers:manage')

  const toggleMutation = useMutation({
    mutationFn: (cc: CostCenter) =>
      cc.isActive ? costCentersApi.deactivate(cc.id) : costCentersApi.activate(cc.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'costCenters'] })
      setConfirmTarget(null)
    },
  })

  const companies = companiesPage?.items ?? []
  const departments = departmentsPage?.items ?? []

  const numberFormat = useMemo(() => new Intl.NumberFormat(undefined, { minimumFractionDigits: 2 }), [])

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'companyId',
        label: t('masterdata.costCenters.company'),
        render: (cc: CostCenter) => companies.find((c) => c.id === cc.companyId)?.name ?? cc.companyId,
      },
      {
        key: 'budgetAmount',
        label: t('masterdata.costCenters.budgetAmount'),
        className: 'text-right',
        render: (cc: CostCenter) => numberFormat.format(cc.budgetAmount),
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (cc: CostCenter) => (
          <StatusBadge
            status={cc.isActive ? 'Active' : 'Inactive'}
            label={cc.isActive ? t('masterdata.costCenters.active') : t('masterdata.costCenters.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (cc: CostCenter) =>
          canManage ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, costCenter: cc })}>
                {t('common.edit')}
              </Button>
              {cc.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(cc)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(cc)}>
                  {t('common.activate')}
                </Button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, companies, numberFormat, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.costCenters.title')}</h1>
        {canManage && (
          <Button onClick={() => setModalState({ open: true, costCenter: null })}>
            {t('masterdata.costCenters.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('masterdata.costCenters.allCompanies')}
        value={companyId}
        onChange={(e) => {
          setCompanyId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.costCenters.allCompanies')}</option>
        {companies.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </Select>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <CostCenterModal
          costCenter={modalState.costCenter}
          companies={companies}
          departments={departments}
          defaultCompanyId={companyId}
          onClose={() => setModalState({ open: false, costCenter: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.costCenters.deactivateTitle')}
        message={t('masterdata.costCenters.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
