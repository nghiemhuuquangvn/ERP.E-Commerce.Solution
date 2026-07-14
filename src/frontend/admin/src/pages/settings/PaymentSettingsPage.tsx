import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  paymentSettingsApi,
  type SystemPaymentSettings,
  type TenantPaymentSettings,
} from '@/api/settings/payment.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  vnPayAvailable: boolean
  manualBankTransferAvailable: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'payment', 'system'],
    queryFn: () => paymentSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { vnPayAvailable: true, manualBankTransferAvailable: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => paymentSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemPaymentSettings) => {
      queryClient.setQueryData(['settings', 'payment', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('vnPayAvailable')} />
        {t('settings.payment.vnPayAvailable')}
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('manualBankTransferAvailable')} />
        {t('settings.payment.manualBankTransferAvailable')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.payment.failedToSave')}</p>}

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
  vnPayEnabled: boolean
  vnPaySecretKey: string
  sandboxMode: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'payment', 'tenant', tenantId],
    queryFn: () => paymentSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { vnPayEnabled: false, vnPaySecretKey: '', sandboxMode: true },
  })

  useEffect(() => {
    if (data) {
      reset({ vnPayEnabled: data.vnPayEnabled, vnPaySecretKey: '', sandboxMode: data.sandboxMode })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      paymentSettingsApi.updateTenantSettings(tenantId, {
        vnPayEnabled: values.vnPayEnabled,
        // Empty means "leave the currently stored key unchanged" — never send a blank overwrite.
        vnPaySecretKey: values.vnPaySecretKey || undefined,
        sandboxMode: values.sandboxMode,
      }),
    onSuccess: (result: TenantPaymentSettings) => {
      queryClient.setQueryData(['settings', 'payment', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.payment.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('vnPayEnabled')} />
        {t('settings.payment.vnPayEnabled')}
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.payment.vnPaySecretKey')}
        <input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasVnPaySecretKey ? t('settings.payment.secretKeyPlaceholderSet') : t('settings.payment.secretKeyPlaceholderUnset')}
          autoComplete="new-password"
          {...register('vnPaySecretKey')}
          className="input mt-1 w-full"
        />
        <span className="text-xs text-gray-400">{t('settings.payment.secretKeyHint')}</span>
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('sandboxMode')} />
        {t('settings.payment.sandboxMode')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.payment.failedToSave')}</p>}

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
