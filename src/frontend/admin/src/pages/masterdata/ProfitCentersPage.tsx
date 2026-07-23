import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { profitCentersApi } from '@/api/masterdata/profitCenters.api'
import type { ProfitCenter } from '@/api/masterdata/profitCenters.api'
import { companiesApi } from '@/api/masterdata/companies.api'
import { businessUnitsApi } from '@/api/masterdata/businessUnits.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface ProfitCenterFormValues {
  companyId: string
  name: string
  code: string
  businessUnitId: string
}

function ProfitCenterModal({
  profitCenter,
  companies,
  businessUnits,
  defaultCompanyId,
  onClose,
}: {
  profitCenter: ProfitCenter | null
  companies: { id: string; name: string }[]
  businessUnits: { id: string; name: string }[]
  defaultCompanyId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!profitCenter

  const { register, handleSubmit, formState: { errors } } = useForm<ProfitCenterFormValues>({
    defaultValues: {
      companyId: profitCenter?.companyId ?? defaultCompanyId,
      name: profitCenter?.name ?? '',
      code: profitCenter?.code ?? '',
      businessUnitId: profitCenter?.businessUnitId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: ProfitCenterFormValues) => {
      const businessUnitId = values.businessUnitId || null
      if (isEdit) {
        return profitCentersApi.update(profitCenter!.id, {
          name: values.name,
          code: values.code,
          businessUnitId,
        })
      }
      return profitCentersApi.create({
        companyId: values.companyId,
        name: values.name,
        code: values.code,
        businessUnitId,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'profitCenters'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.profitCenters.editTitle') : t('masterdata.profitCenters.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.profitCenters.company')}</label>
              <select className="input" {...register('companyId', { required: true })}>
                <option value="">{t('common.select')}</option>
                {companies.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              {errors.companyId && (
                <p className="mt-1 text-xs text-red-600">{t('masterdata.profitCenters.company')} is required.</p>
              )}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
            {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.profitCenters.businessUnitOptional')}</label>
            <select className="input" {...register('businessUnitId')}>
              <option value="">{t('common.none')}</option>
              {businessUnits.map((bu) => (
                <option key={bu.id} value={bu.id}>
                  {bu.name}
                </option>
              ))}
            </select>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.profitCenters.failedToSave')}</p>
          )}
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

export function ProfitCentersPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [companyId, setCompanyId] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; profitCenter: ProfitCenter | null }>({
    open: false,
    profitCenter: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<ProfitCenter | null>(null)

  const { data: companiesPage } = useQuery({
    queryKey: ['masterdata', 'companies', 'picker'],
    queryFn: () => companiesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data: businessUnitsPage } = useQuery({
    queryKey: ['masterdata', 'businessUnits', 'picker'],
    queryFn: () => businessUnitsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'profitCenters', { companyId, page, pageSize }],
    queryFn: () => profitCentersApi.list({ companyId: companyId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:profit-centers:manage')

  const toggleMutation = useMutation({
    mutationFn: (pc: ProfitCenter) =>
      pc.isActive ? profitCentersApi.deactivate(pc.id) : profitCentersApi.activate(pc.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'profitCenters'] })
      setConfirmTarget(null)
    },
  })

  const companies = companiesPage?.items ?? []
  const businessUnits = businessUnitsPage?.items ?? []

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'companyId',
        label: t('masterdata.profitCenters.company'),
        render: (pc: ProfitCenter) => companies.find((c) => c.id === pc.companyId)?.name ?? pc.companyId,
      },
      {
        key: 'businessUnitId',
        label: t('masterdata.profitCenters.businessUnitOptional'),
        render: (pc: ProfitCenter) =>
          pc.businessUnitId ? businessUnits.find((bu) => bu.id === pc.businessUnitId)?.name ?? pc.businessUnitId : '—',
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (pc: ProfitCenter) => (
          <StatusBadge
            status={pc.isActive ? 'Active' : 'Inactive'}
            label={pc.isActive ? t('masterdata.profitCenters.active') : t('masterdata.profitCenters.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (pc: ProfitCenter) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, profitCenter: pc })}
              >
                {t('common.edit')}
              </button>
              {pc.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(pc)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(pc)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, companies, businessUnits, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.profitCenters.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, profitCenter: null })}>
            {t('masterdata.profitCenters.add')}
          </button>
        )}
      </div>

      <select
        className="input max-w-xs"
        value={companyId}
        onChange={(e) => {
          setCompanyId(e.target.value)
          setPage(1)
        }}
      >
        <option value="">{t('masterdata.profitCenters.allCompanies')}</option>
        {companies.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <ProfitCenterModal
          profitCenter={modalState.profitCenter}
          companies={companies}
          businessUnits={businessUnits}
          defaultCompanyId={companyId}
          onClose={() => setModalState({ open: false, profitCenter: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.profitCenters.deactivateTitle')}
        message={t('masterdata.profitCenters.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
