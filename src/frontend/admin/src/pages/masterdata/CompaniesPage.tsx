import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { companiesApi } from '@/api/masterdata/companies.api'
import type { Company } from '@/api/masterdata/companies.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface CompanyFormValues {
  name: string
  code: string
  taxCode: string
  legalAddress: string
  currencyCode: string
}

function CompanyModal({ company, onClose }: { company: Company | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!company

  const { register, handleSubmit, formState: { errors } } = useForm<CompanyFormValues>({
    defaultValues: {
      name: company?.name ?? '',
      code: company?.code ?? '',
      taxCode: company?.taxCode ?? '',
      legalAddress: company?.legalAddress ?? '',
      currencyCode: company?.currencyCode ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CompanyFormValues) => {
      const payload = {
        name: values.name,
        code: values.code,
        taxCode: values.taxCode.trim() || null,
        legalAddress: values.legalAddress.trim() || null,
        currencyCode: values.currencyCode.trim() || null,
      }
      return isEdit ? companiesApi.update(company!.id, payload) : companiesApi.create(payload)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'companies'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.companies.editTitle') : t('masterdata.companies.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
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
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.companies.taxCode')}</label>
            <input className="input" {...register('taxCode')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.companies.legalAddress')}</label>
            <input className="input" {...register('legalAddress')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.companies.currencyCode')}</label>
            <input className="input" {...register('currencyCode')} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.companies.failedToSave')}</p>
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

export function CompaniesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const queryClient = useQueryClient()
  const [modalState, setModalState] = useState<{ open: boolean; company: Company | null }>({
    open: false,
    company: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Company | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'companies', { page, pageSize }],
    queryFn: () => companiesApi.list({ page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:companies:manage')

  const toggleMutation = useMutation({
    mutationFn: (c: Company) => (c.isActive ? companiesApi.deactivate(c.id) : companiesApi.activate(c.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'companies'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'taxCode', label: t('masterdata.companies.taxCode'), render: (c: Company) => c.taxCode ?? '—' },
      { key: 'currencyCode', label: t('masterdata.companies.currencyCode'), render: (c: Company) => c.currencyCode ?? '—' },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (c: Company) => (
          <StatusBadge
            status={c.isActive ? 'Active' : 'Inactive'}
            label={c.isActive ? t('masterdata.companies.active') : t('masterdata.companies.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (c: Company) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, company: c })}
              >
                {t('common.edit')}
              </button>
              {c.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(c)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(c)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.companies.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, company: null })}>
            {t('masterdata.companies.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {modalState.open && (
        <CompanyModal company={modalState.company} onClose={() => setModalState({ open: false, company: null })} />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.companies.deactivateTitle')}
        message={t('masterdata.companies.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
