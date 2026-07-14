import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  emailSettingsApi,
  type SystemEmailSettings,
  type TenantEmailSettings,
} from '@/api/settings/email.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customSmtpAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'email', 'system'],
    queryFn: () => emailSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customSmtpAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => emailSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemEmailSettings) => {
      queryClient.setQueryData(['settings', 'email', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('customSmtpAllowed')} />
        {t('settings.email.customSmtpAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.email.failedToSave')}</p>}

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
  smtpHost: string
  smtpPort: number
  smtpUsername: string
  smtpPassword: string
  fromAddress: string
  fromName: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'email', 'tenant', tenantId],
    queryFn: () => emailSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: {
      smtpHost: '',
      smtpPort: 587,
      smtpUsername: '',
      smtpPassword: '',
      fromAddress: '',
      fromName: '',
    },
  })

  useEffect(() => {
    if (data) {
      reset({
        smtpHost: data.smtpHost ?? '',
        smtpPort: data.smtpPort,
        smtpUsername: data.smtpUsername ?? '',
        smtpPassword: '',
        fromAddress: data.fromAddress ?? '',
        fromName: data.fromName ?? '',
      })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      emailSettingsApi.updateTenantSettings(tenantId, {
        smtpHost: values.smtpHost,
        smtpPort: values.smtpPort,
        smtpUsername: values.smtpUsername,
        // Empty means "leave the currently stored password unchanged" — never send a blank overwrite.
        smtpPassword: values.smtpPassword || undefined,
        fromAddress: values.fromAddress,
        fromName: values.fromName,
      }),
    onSuccess: (result: TenantEmailSettings) => {
      queryClient.setQueryData(['settings', 'email', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.email.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.email.smtpHost')}
        <input type="text" disabled={!canManage} {...register('smtpHost')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.email.smtpPort')}
        <input
          type="number"
          disabled={!canManage}
          {...register('smtpPort', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.email.smtpUsername')}
        <input type="text" disabled={!canManage} {...register('smtpUsername')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.email.smtpPassword')}
        <input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasSmtpPassword ? t('settings.email.passwordPlaceholderSet') : t('settings.email.passwordPlaceholderUnset')}
          autoComplete="new-password"
          {...register('smtpPassword')}
          className="input mt-1 w-full"
        />
        <span className="text-xs text-gray-400">{t('settings.email.passwordHint')}</span>
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.email.fromAddress')}
        <input type="email" disabled={!canManage} {...register('fromAddress')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.email.fromName')}
        <input type="text" disabled={!canManage} {...register('fromName')} className="input mt-1 w-full" />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.email.failedToSave')}</p>}

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

export function EmailSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:email:system:view')
  const canManageSystem = hasPermission('settings:email:system:manage')
  const canManageTenant = hasPermission('settings:email:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.email.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.email.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.email.title')}</h1>

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
