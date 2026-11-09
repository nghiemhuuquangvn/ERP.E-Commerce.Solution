import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, Select, useToast } from '@/components/ui'
import { warehousesApi } from '@/api/warehouses.api'
import {
  inventorySettingsApi,
  type SystemInventorySettings,
  type TenantInventorySettings,
} from '@/api/settings/inventory.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  negativeStockAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('negativeStockAllowed')} label={t('settings.inventory.negativeStockAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.inventory.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  lowStockThreshold: number
  backorderAllowed: boolean
  defaultHoldDays: number
  defaultWarehouseId: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'inventory', 'tenant', tenantId],
    queryFn: () => inventorySettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { lowStockThreshold: 10, backorderAllowed: false, defaultHoldDays: 7, defaultWarehouseId: '' },
  })

  const warehouses = useQuery({ queryKey: ['warehouses'], queryFn: () => warehousesApi.list(), staleTime: 60_000 })

  useEffect(() => {
    if (data) reset({ ...data, defaultWarehouseId: data.defaultWarehouseId ?? '' })
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      inventorySettingsApi.updateTenantSettings(tenantId, {
        lowStockThreshold: Number(values.lowStockThreshold),
        backorderAllowed: values.backorderAllowed,
        defaultHoldDays: Number(values.defaultHoldDays),
        defaultWarehouseId: values.defaultWarehouseId || null,
      }),
    onSuccess: (result: TenantInventorySettings) => {
      queryClient.setQueryData(['settings', 'inventory', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.inventory.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.inventory.lowStockThreshold')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('lowStockThreshold', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.inventory.defaultWarehouse')} hint={t('settings.inventory.defaultWarehouseHint')}>
        <Select disabled={!canManage} {...register('defaultWarehouseId')}>
          <option value="">{t('settings.inventory.defaultWarehouseAuto')}</option>
          {(warehouses.data ?? []).filter((w) => w.isActive).map((w) => (
            <option key={w.id} value={w.id}>{w.code} — {w.name}</option>
          ))}
        </Select>
      </FormField>
      <Checkbox disabled={!canManage} {...register('backorderAllowed')} label={t('settings.inventory.backorderAllowed')} />
      <FormField label={t('settings.inventory.defaultHoldDays')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('defaultHoldDays', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.inventory.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="inventorysettings" label={t('settings.inventory.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
