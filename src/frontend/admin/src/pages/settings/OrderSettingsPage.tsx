import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  orderSettingsApi,
  type SystemOrderSettings,
  type TenantOrderSettings,
} from '@/api/settings/order.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customOrderNumberFormatAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'order', 'system'],
    queryFn: () => orderSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customOrderNumberFormatAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => orderSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemOrderSettings) => {
      queryClient.setQueryData(['settings', 'order', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('customOrderNumberFormatAllowed')} />
        {t('settings.order.customOrderNumberFormatAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.order.failedToSave')}</p>}

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
  orderNumberPrefix: string
  autoCancelUnpaidAfterHours: number
  allowEditAfterSubmit: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'order', 'tenant', tenantId],
    queryFn: () => orderSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { orderNumberPrefix: 'SO-', autoCancelUnpaidAfterHours: 24, allowEditAfterSubmit: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      orderSettingsApi.updateTenantSettings(tenantId, {
        orderNumberPrefix: values.orderNumberPrefix,
        autoCancelUnpaidAfterHours: Number(values.autoCancelUnpaidAfterHours),
        allowEditAfterSubmit: values.allowEditAfterSubmit,
      }),
    onSuccess: (result: TenantOrderSettings) => {
      queryClient.setQueryData(['settings', 'order', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.order.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.order.orderNumberPrefix')}
        <input type="text" disabled={!canManage} {...register('orderNumberPrefix')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.order.autoCancelUnpaidAfterHours')}
        <input
          type="number"
          disabled={!canManage}
          {...register('autoCancelUnpaidAfterHours', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('allowEditAfterSubmit')} />
        {t('settings.order.allowEditAfterSubmit')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.order.failedToSave')}</p>}

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

export function OrderSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:order:system:view')
  const canManageSystem = hasPermission('settings:order:system:manage')
  const canManageTenant = hasPermission('settings:order:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.order.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.order.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.order.title')}</h1>

      <div className="flex gap-2 border-b border-gray-200">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-primary-600 text-primary-700'
                : 'text-gray-500 hover:text-gray-700'
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
