import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, FormField, Input, useToast } from '@/components/ui'
import {
  companySettingsApi,
  type TenantCompanySettings,
} from '@/api/settings/company.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.company.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.company.companyName')}>
        <Input type="text" disabled={!canManage} {...register('companyName')} />
      </FormField>
      <FormField label={t('settings.company.legalName')}>
        <Input type="text" disabled={!canManage} {...register('legalName')} />
      </FormField>
      <FormField label={t('settings.company.taxCode')}>
        <Input type="text" disabled={!canManage} {...register('taxCode')} />
      </FormField>
      <FormField label={t('settings.company.address')}>
        <Input type="text" disabled={!canManage} {...register('address')} />
      </FormField>
      <FormField label={t('settings.company.phone')}>
        <Input type="text" disabled={!canManage} {...register('phone')} />
      </FormField>
      <FormField label={t('settings.company.email')}>
        <Input type="email" disabled={!canManage} {...register('email')} />
      </FormField>
      <FormField label={t('settings.company.logoUrl')}>
        <Input type="text" disabled={!canManage} {...register('logoUrl')} />
      </FormField>
      <FormField label={t('settings.company.website')}>
        <Input type="text" disabled={!canManage} {...register('website')} />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.company.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
