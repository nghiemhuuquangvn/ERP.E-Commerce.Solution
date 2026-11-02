import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  paymentSettingsApi,
  type SystemPaymentSettings,
  type TenantPaymentSettings,
} from '@/api/settings/payment.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'
import { PaymentGatewaysCard } from './PaymentGatewaysCard'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  vnPayAvailable: boolean
  manualBankTransferAvailable: boolean
  moMoAvailable: boolean
  zaloPayAvailable: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'payment', 'system'],
    queryFn: () => paymentSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { vnPayAvailable: true, manualBankTransferAvailable: true, moMoAvailable: true, zaloPayAvailable: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => paymentSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemPaymentSettings) => {
      queryClient.setQueryData(['settings', 'payment', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('vnPayAvailable')} label={t('settings.payment.vnPayAvailable')} />
      <Checkbox disabled={!canManage} {...register('moMoAvailable')} label={t('settings.payment.moMoAvailable')} />
      <Checkbox disabled={!canManage} {...register('zaloPayAvailable')} label={t('settings.payment.zaloPayAvailable')} />
      <Checkbox disabled={!canManage} {...register('manualBankTransferAvailable')} label={t('settings.payment.manualBankTransferAvailable')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.payment.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  vnPayEnabled: boolean
  vnPaySecretKey: string
  sandboxMode: boolean
  codEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'payment', 'tenant', tenantId],
    queryFn: () => paymentSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { vnPayEnabled: false, vnPaySecretKey: '', sandboxMode: true, codEnabled: true },
  })

  useEffect(() => {
    if (data) {
      reset({ vnPayEnabled: data.vnPayEnabled, vnPaySecretKey: '', sandboxMode: data.sandboxMode, codEnabled: data.codEnabled })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      paymentSettingsApi.updateTenantSettings(tenantId, {
        vnPayEnabled: values.vnPayEnabled,
        // Empty means "leave the currently stored key unchanged" — never send a blank overwrite.
        vnPaySecretKey: values.vnPaySecretKey || undefined,
        sandboxMode: values.sandboxMode,
        codEnabled: values.codEnabled,
      }),
    onSuccess: (result: TenantPaymentSettings) => {
      queryClient.setQueryData(['settings', 'payment', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.payment.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('vnPayEnabled')} label={t('settings.payment.vnPayEnabled')} />
      <FormField label={t('settings.payment.vnPaySecretKey')} hint={t('settings.payment.secretKeyHint')}>
        <Input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasVnPaySecretKey ? t('settings.payment.secretKeyPlaceholderSet') : t('settings.payment.secretKeyPlaceholderUnset')}
          autoComplete="new-password"
          {...register('vnPaySecretKey')}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('sandboxMode')} label={t('settings.payment.sandboxMode')} />
      <Checkbox disabled={!canManage} {...register('codEnabled')} label={t('settings.payment.codEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.payment.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function PaymentSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:payment:system:view')
  const canManageSystem = hasPermission('settings:payment:system:manage')
  const canManageTenant = hasPermission('settings:payment:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.payment.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.payment.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.payment.title')}</h1>

      <TabList id="paymentsettings" label={t('settings.payment.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <div className="space-y-8">
          <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
          {user?.tenantId && <PaymentGatewaysCard tenantId={user.tenantId} canManage={canManageTenant} />}
        </div>
      )}
    </div>
  )
}
