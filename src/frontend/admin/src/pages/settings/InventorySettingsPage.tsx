import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  inventorySettingsApi,
  type SystemInventorySettings,
  type TenantInventorySettings,
} from '@/api/settings/inventory.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  negativeStockAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'inventory', 'system'],
    queryFn: () => inventorySettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { negativeStockAllowed: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => inventorySettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemInventorySettings) => {
      queryClient.setQueryData(['settings', 'inventory', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('negativeStockAllowed')} />
        {t('settings.inventory.negativeStockAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.inventory.failedToSave')}</p>}

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
  lowStockThreshold: number
  backorderAllowed: boolean
  defaultHoldDays: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'inventory', 'tenant', tenantId],
    queryFn: () => inventorySettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { lowStockThreshold: 10, backorderAllowed: false, defaultHoldDays: 7 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      inventorySettingsApi.updateTenantSettings(tenantId, {
        lowStockThreshold: Number(values.lowStockThreshold),
        backorderAllowed: values.backorderAllowed,
        defaultHoldDays: Number(values.defaultHoldDays),
      }),
    onSuccess: (result: TenantInventorySettings) => {
      queryClient.setQueryData(['settings', 'inventory', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.inventory.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-ink-secondary">
        {t('settings.inventory.lowStockThreshold')}
        <input
          type="number"
          disabled={!canManage}
          {...register('lowStockThreshold', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('backorderAllowed')} />
        {t('settings.inventory.backorderAllowed')}
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.inventory.defaultHoldDays')}
        <input
          type="number"
          disabled={!canManage}
          {...register('defaultHoldDays', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.inventory.failedToSave')}</p>}

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

export function InventorySettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:inventory:system:view')
  const canManageSystem = hasPermission('settings:inventory:system:manage')
  const canManageTenant = hasPermission('settings:inventory:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.inventory.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.inventory.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.inventory.title')}</h1>

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
