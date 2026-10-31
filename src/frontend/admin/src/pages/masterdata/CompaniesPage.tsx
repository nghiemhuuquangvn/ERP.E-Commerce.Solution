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
import { Alert, Button, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.companies.editTitle') : t('masterdata.companies.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="company-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="company-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.companies.taxCode')}>
          <Input {...register('taxCode')} />
        </FormField>
        <FormField label={t('masterdata.companies.legalAddress')}>
          <Input {...register('legalAddress')} />
        </FormField>
        <FormField label={t('masterdata.companies.currencyCode')}>
          <Input {...register('currencyCode')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.companies.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, company: c })}>
                {t('common.edit')}
              </Button>
              {c.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(c)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(c)}>
                  {t('common.activate')}
                </Button>
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
          <Button onClick={() => setModalState({ open: true, company: null })}>
            {t('masterdata.companies.add')}
          </Button>
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
