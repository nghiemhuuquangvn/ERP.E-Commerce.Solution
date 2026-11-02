import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  emailSettingsApi,
  type SystemEmailSettings,
  type TenantEmailSettings,
} from '@/api/settings/email.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customSmtpAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('customSmtpAllowed')} label={t('settings.email.customSmtpAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.email.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.email.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.email.smtpHost')}>
        <Input type="text" disabled={!canManage} {...register('smtpHost')} />
      </FormField>
      <FormField label={t('settings.email.smtpPort')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('smtpPort', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.email.smtpUsername')}>
        <Input type="text" disabled={!canManage} {...register('smtpUsername')} />
      </FormField>
      <FormField label={t('settings.email.smtpPassword')} hint={t('settings.email.passwordHint')}>
        <Input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasSmtpPassword ? t('settings.email.passwordPlaceholderSet') : t('settings.email.passwordPlaceholderUnset')}
          autoComplete="new-password"
          {...register('smtpPassword')}
 />
      </FormField>
      <FormField label={t('settings.email.fromAddress')}>
        <Input type="email" disabled={!canManage} {...register('fromAddress')} />
      </FormField>
      <FormField label={t('settings.email.fromName')}>
        <Input type="text" disabled={!canManage} {...register('fromName')} />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.email.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="emailsettings" label={t('settings.email.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
