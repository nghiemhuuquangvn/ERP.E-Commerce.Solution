import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  companySettingsApi,
  type TenantCompanySettings,
} from '@/api/settings/company.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

interface TenantFormValues {
  companyName: string
  legalName: string
  taxCode: string
  address: string
  phone: string
  email: string
  logoUrl: string
  website: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'company', 'tenant', tenantId],
    queryFn: () => companySettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: {
      companyName: '',
      legalName: '',
      taxCode: '',
      address: '',
      phone: '',
      email: '',
      logoUrl: '',
      website: '',
    },
  })

  useEffect(() => {
    if (data) {
      reset({
        companyName: data.companyName,
        legalName: data.legalName ?? '',
        taxCode: data.taxCode ?? '',
        address: data.address ?? '',
        phone: data.phone ?? '',
        email: data.email ?? '',
        logoUrl: data.logoUrl ?? '',
        website: data.website ?? '',
      })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => companySettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantCompanySettings) => {
      queryClient.setQueryData(['settings', 'company', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.company.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.company.companyName')}
        <input type="text" disabled={!canManage} {...register('companyName')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.legalName')}
        <input type="text" disabled={!canManage} {...register('legalName')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.taxCode')}
        <input type="text" disabled={!canManage} {...register('taxCode')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.address')}
        <input type="text" disabled={!canManage} {...register('address')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.phone')}
        <input type="text" disabled={!canManage} {...register('phone')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.email')}
        <input type="email" disabled={!canManage} {...register('email')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.logoUrl')}
        <input type="text" disabled={!canManage} {...register('logoUrl')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.company.website')}
        <input type="text" disabled={!canManage} {...register('website')} className="input mt-1 w-full" />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.company.failedToSave')}</p>}

      {canManage && (
        <div className="pt-2">
          <button type="submit" disabled={mutation.isPending} className="btn-primary">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </form>
  )
}

export function CompanySettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canManageTenant = hasPermission('settings:company:tenant:manage')

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.company.title')}</h1>
      <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
    </div>
  )
}
