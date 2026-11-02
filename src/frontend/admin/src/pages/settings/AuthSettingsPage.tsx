import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  settingsApi,
  type SystemAuthSettings,
  type TenantAuthSettings,
} from '@/api/settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'system'],
    queryFn: () => settingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: {
      googleEnabled: false,
      microsoftEnabled: false,
      facebookEnabled: false,
      twitterEnabled: false,
      twoFactorEnabled: false,
      minPasswordLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireDigit: true,
      requireSymbol: true,
      maxLoginAttempts: 5,
      lockoutDurationMinutes: 15,
      sessionTimeoutMinutes: 60,
    },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => settingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemAuthSettings) => {
      queryClient.setQueryData(['settings', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('googleEnabled')} label={t('settings.auth.googleEnabled')} />
      <Checkbox disabled={!canManage} {...register('microsoftEnabled')} label={t('settings.auth.microsoftEnabled')} />
      <Checkbox disabled={!canManage} {...register('facebookEnabled')} label={t('settings.auth.facebookEnabled')} />
      <Checkbox disabled={!canManage} {...register('twitterEnabled')} label={t('settings.auth.twitterEnabled')} />
      <Checkbox disabled={!canManage} {...register('twoFactorEnabled')} label={t('settings.auth.twoFactorEnabled')} />

      <h2 className="pt-2 text-sm font-semibold text-ink">{t('settings.auth.securitySectionTitle')}</h2>
      <FormField label={t('settings.auth.minPasswordLength')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('minPasswordLength', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('requireUppercase')} label={t('settings.auth.requireUppercase')} />
      <Checkbox disabled={!canManage} {...register('requireLowercase')} label={t('settings.auth.requireLowercase')} />
      <Checkbox disabled={!canManage} {...register('requireDigit')} label={t('settings.auth.requireDigit')} />
      <Checkbox disabled={!canManage} {...register('requireSymbol')} label={t('settings.auth.requireSymbol')} />
      <FormField label={t('settings.auth.maxLoginAttempts')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('maxLoginAttempts', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.lockoutDurationMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('lockoutDurationMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.sessionTimeoutMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('sessionTimeoutMinutes', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.auth.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
  twoFactorRequired: boolean
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'tenant', tenantId],
    queryFn: () => settingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: {
      googleEnabled: false,
      microsoftEnabled: false,
      facebookEnabled: false,
      twitterEnabled: false,
      twoFactorEnabled: false,
      twoFactorRequired: false,
      minPasswordLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireDigit: true,
      requireSymbol: true,
      maxLoginAttempts: 5,
      lockoutDurationMinutes: 15,
      sessionTimeoutMinutes: 60,
    },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => settingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantAuthSettings) => {
      queryClient.setQueryData(['settings', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.auth.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('googleEnabled')} label={t('settings.auth.googleEnabled')} />
      <Checkbox disabled={!canManage} {...register('microsoftEnabled')} label={t('settings.auth.microsoftEnabled')} />
      <Checkbox disabled={!canManage} {...register('facebookEnabled')} label={t('settings.auth.facebookEnabled')} />
      <Checkbox disabled={!canManage} {...register('twitterEnabled')} label={t('settings.auth.twitterEnabled')} />
      <Checkbox disabled={!canManage} {...register('twoFactorEnabled')} label={t('settings.auth.twoFactorEnabled')} />
      <Checkbox disabled={!canManage} {...register('twoFactorRequired')} label={t('settings.auth.twoFactorRequired')} />

      <h2 className="pt-2 text-sm font-semibold text-ink">{t('settings.auth.securitySectionTitle')}</h2>
      <FormField label={t('settings.auth.minPasswordLength')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('minPasswordLength', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('requireUppercase')} label={t('settings.auth.requireUppercase')} />
      <Checkbox disabled={!canManage} {...register('requireLowercase')} label={t('settings.auth.requireLowercase')} />
      <Checkbox disabled={!canManage} {...register('requireDigit')} label={t('settings.auth.requireDigit')} />
      <Checkbox disabled={!canManage} {...register('requireSymbol')} label={t('settings.auth.requireSymbol')} />
      <FormField label={t('settings.auth.maxLoginAttempts')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('maxLoginAttempts', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.lockoutDurationMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('lockoutDurationMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.sessionTimeoutMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('sessionTimeoutMinutes', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.auth.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function AuthSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:system:view')
  const canManageSystem = hasPermission('settings:system:manage')
  const canManageTenant = hasPermission('settings:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.auth.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.auth.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.auth.title')}</h1>

      <TabList id="authsettings" label={t('settings.auth.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
