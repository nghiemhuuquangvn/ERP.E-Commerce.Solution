import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  fileStorageSettingsApi,
  type SystemFileStorageSettings,
  type TenantFileStorageSettings,
} from '@/api/settings/fileStorage.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customStorageProviderAllowed: boolean
  defaultMaxUploadSizeMb: number
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('customStorageProviderAllowed')} label={t('settings.fileStorage.customStorageProviderAllowed')} />
      <FormField label={t('settings.fileStorage.defaultMaxUploadSizeMb')}>
        <Input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('defaultMaxUploadSizeMb', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.fileStorage.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.fileStorage.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.fileStorage.provider')}>
        <Input type="text" disabled={!canManage} {...register('provider')} />
      </FormField>
      <FormField label={t('settings.fileStorage.maxUploadSizeMb')}>
        <Input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('maxUploadSizeMb', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.fileStorage.cdnBaseUrl')}>
        <Input type="text" disabled={!canManage} {...register('cdnBaseUrl')} />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.fileStorage.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="filestoragesettings" label={t('settings.fileStorage.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
