import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  storeProductSettingsApi,
  type SystemStoreProductSettings,
  type TenantStoreProductSettings,
} from '@/api/settings/storeProduct.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  priceDisplayIncludesTaxByDefault: boolean
  productReviewsEnabled: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

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
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('priceDisplayIncludesTaxByDefault')} />
        {t('settings.storeProduct.priceDisplayIncludesTaxByDefault')}
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('productReviewsEnabled')} />
        {t('settings.storeProduct.productReviewsEnabled')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.storeProduct.failedToSave')}</p>}

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

interface TenantFormValues {
  storeName: string
  storeDescription: string
  logoUrl: string
  showOutOfStockProducts: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

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
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.storeProduct.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-ink-secondary">
        {t('settings.storeProduct.storeName')}
        <input type="text" disabled={!canManage} {...register('storeName')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.storeProduct.storeDescription')}
        <input type="text" disabled={!canManage} {...register('storeDescription')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.storeProduct.logoUrl')}
        <input type="text" disabled={!canManage} {...register('logoUrl')} className="input mt-1 w-full" />
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('showOutOfStockProducts')} />
        {t('settings.storeProduct.showOutOfStockProducts')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.storeProduct.failedToSave')}</p>}

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

      <div className="flex gap-2 border-b border-line">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-brand-action text-brand-action'
                : 'text-ink-muted hover:text-ink'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
