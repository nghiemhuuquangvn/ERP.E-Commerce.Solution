import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  storeProductSettingsApi,
  type SystemStoreProductSettings,
  type TenantStoreProductSettings,
} from '@/api/settings/storeProduct.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  priceDisplayIncludesTaxByDefault: boolean
  productReviewsEnabled: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'store-product', 'system'],
    queryFn: () => storeProductSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { priceDisplayIncludesTaxByDefault: true, productReviewsEnabled: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => storeProductSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemStoreProductSettings) => {
      queryClient.setQueryData(['settings', 'store-product', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('priceDisplayIncludesTaxByDefault')} label={t('settings.storeProduct.priceDisplayIncludesTaxByDefault')} />
      <Checkbox disabled={!canManage} {...register('productReviewsEnabled')} label={t('settings.storeProduct.productReviewsEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.storeProduct.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  storeName: string
  storeDescription: string
  logoUrl: string
  showOutOfStockProducts: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'store-product', 'tenant', tenantId],
    queryFn: () => storeProductSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { storeName: '', storeDescription: '', logoUrl: '', showOutOfStockProducts: true },
  })

  useEffect(() => {
    if (data) {
      reset({
        storeName: data.storeName,
        storeDescription: data.storeDescription ?? '',
        logoUrl: data.logoUrl ?? '',
        showOutOfStockProducts: data.showOutOfStockProducts,
      })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      storeProductSettingsApi.updateTenantSettings(tenantId, {
        storeName: values.storeName,
        storeDescription: values.storeDescription || null,
        logoUrl: values.logoUrl || null,
        showOutOfStockProducts: values.showOutOfStockProducts,
      }),
    onSuccess: (result: TenantStoreProductSettings) => {
      queryClient.setQueryData(['settings', 'store-product', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.storeProduct.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.storeProduct.storeName')}>
        <Input type="text" disabled={!canManage} {...register('storeName')} />
      </FormField>
      <FormField label={t('settings.storeProduct.storeDescription')}>
        <Input type="text" disabled={!canManage} {...register('storeDescription')} />
      </FormField>
      <FormField label={t('settings.storeProduct.logoUrl')}>
        <Input type="text" disabled={!canManage} {...register('logoUrl')} />
      </FormField>
      <Checkbox disabled={!canManage} {...register('showOutOfStockProducts')} label={t('settings.storeProduct.showOutOfStockProducts')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.storeProduct.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function StoreProductSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:store-product:system:view')
  const canManageSystem = hasPermission('settings:store-product:system:manage')
  const canManageTenant = hasPermission('settings:store-product:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.storeProduct.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.storeProduct.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.storeProduct.title')}</h1>

      <TabList id="storeproductsettings" label={t('settings.storeProduct.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
