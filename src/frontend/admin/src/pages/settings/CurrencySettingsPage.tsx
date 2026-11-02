import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  currencySettingsApi,
  type SystemCurrencySettings,
  type TenantCurrencySettings,
} from '@/api/settings/currency.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  multiCurrencyAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'currency', 'system'],
    queryFn: () => currencySettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { multiCurrencyAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => currencySettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemCurrencySettings) => {
      queryClient.setQueryData(['settings', 'currency', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('multiCurrencyAllowed')} label={t('settings.currency.multiCurrencyAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.currency.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  defaultCurrencyCode: string
  exchangeRateSource: string
  decimalDigits: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'currency', 'tenant', tenantId],
    queryFn: () => currencySettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { defaultCurrencyCode: 'USD', exchangeRateSource: 'Manual', decimalDigits: 2 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      currencySettingsApi.updateTenantSettings(tenantId, {
        defaultCurrencyCode: values.defaultCurrencyCode,
        exchangeRateSource: values.exchangeRateSource,
        decimalDigits: Number(values.decimalDigits),
      }),
    onSuccess: (result: TenantCurrencySettings) => {
      queryClient.setQueryData(['settings', 'currency', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.currency.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.currency.defaultCurrencyCode')}>
        <Input
          type="text"
          maxLength={3}
          disabled={!canManage}
          {...register('defaultCurrencyCode')}
          className="uppercase"
 />
      </FormField>
      <FormField label={t('settings.currency.exchangeRateSource')}>
        <Input type="text" disabled={!canManage} {...register('exchangeRateSource')} />
      </FormField>
      <FormField label={t('settings.currency.decimalDigits')}>
        <Input
          type="number"
          min={0}
          max={6}
          disabled={!canManage}
          {...register('decimalDigits', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.currency.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function CurrencySettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:currency:system:view')
  const canManageSystem = hasPermission('settings:currency:system:manage')
  const canManageTenant = hasPermission('settings:currency:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.currency.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.currency.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.currency.title')}</h1>

      <TabList id="currencysettings" label={t('settings.currency.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
