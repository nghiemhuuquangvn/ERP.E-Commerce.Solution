import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  fileStorageSettingsApi,
  type SystemFileStorageSettings,
  type TenantFileStorageSettings,
} from '@/api/settings/fileStorage.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customStorageProviderAllowed: boolean
  defaultMaxUploadSizeMb: number
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'file-storage', 'system'],
    queryFn: () => fileStorageSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customStorageProviderAllowed: true, defaultMaxUploadSizeMb: 10 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => fileStorageSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemFileStorageSettings) => {
      queryClient.setQueryData(['settings', 'file-storage', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('customStorageProviderAllowed')} />
        {t('settings.fileStorage.customStorageProviderAllowed')}
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.fileStorage.defaultMaxUploadSizeMb')}
        <input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('defaultMaxUploadSizeMb', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.fileStorage.failedToSave')}</p>}

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
  provider: string
  maxUploadSizeMb: number
  cdnBaseUrl: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'file-storage', 'tenant', tenantId],
    queryFn: () => fileStorageSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { provider: 'Local', maxUploadSizeMb: 10, cdnBaseUrl: '' },
  })

  useEffect(() => {
    if (data) {
      reset({ provider: data.provider, maxUploadSizeMb: data.maxUploadSizeMb, cdnBaseUrl: data.cdnBaseUrl ?? '' })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      fileStorageSettingsApi.updateTenantSettings(tenantId, {
        provider: values.provider,
        maxUploadSizeMb: values.maxUploadSizeMb,
        cdnBaseUrl: values.cdnBaseUrl || null,
      }),
    onSuccess: (result: TenantFileStorageSettings) => {
      queryClient.setQueryData(['settings', 'file-storage', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.fileStorage.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.fileStorage.provider')}
        <input type="text" disabled={!canManage} {...register('provider')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.fileStorage.maxUploadSizeMb')}
        <input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('maxUploadSizeMb', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.fileStorage.cdnBaseUrl')}
        <input type="text" disabled={!canManage} {...register('cdnBaseUrl')} className="input mt-1 w-full" />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.fileStorage.failedToSave')}</p>}

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

export function FileStorageSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:file-storage:system:view')
  const canManageSystem = hasPermission('settings:file-storage:system:manage')
  const canManageTenant = hasPermission('settings:file-storage:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.fileStorage.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.fileStorage.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.fileStorage.title')}</h1>

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
