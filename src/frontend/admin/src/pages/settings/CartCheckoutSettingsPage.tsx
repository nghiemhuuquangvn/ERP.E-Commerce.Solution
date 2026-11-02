import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  cartCheckoutSettingsApi,
  type SystemCartCheckoutSettings,
  type TenantCartCheckoutSettings,
} from '@/api/settings/cartCheckout.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  guestCheckoutAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'cart-checkout', 'system'],
    queryFn: () => cartCheckoutSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { guestCheckoutAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => cartCheckoutSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemCartCheckoutSettings) => {
      queryClient.setQueryData(['settings', 'cart-checkout', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('guestCheckoutAllowed')} />
        {t('settings.cartCheckout.guestCheckoutAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.cartCheckout.failedToSave')}</p>}

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
  cartHoldMinutes: number
  requireRegistrationAtCheckout: boolean
  checkoutSteps: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'cart-checkout', 'tenant', tenantId],
    queryFn: () => cartCheckoutSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { cartHoldMinutes: 60, requireRegistrationAtCheckout: false, checkoutSteps: 3 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      cartCheckoutSettingsApi.updateTenantSettings(tenantId, {
        cartHoldMinutes: Number(values.cartHoldMinutes),
        requireRegistrationAtCheckout: values.requireRegistrationAtCheckout,
        checkoutSteps: Number(values.checkoutSteps),
      }),
    onSuccess: (result: TenantCartCheckoutSettings) => {
      queryClient.setQueryData(['settings', 'cart-checkout', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.cartCheckout.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-ink-secondary">
        {t('settings.cartCheckout.cartHoldMinutes')}
        <input
          type="number"
          disabled={!canManage}
          {...register('cartHoldMinutes', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" disabled={!canManage} {...register('requireRegistrationAtCheckout')} />
        {t('settings.cartCheckout.requireRegistrationAtCheckout')}
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('settings.cartCheckout.checkoutSteps')}
        <input
          type="number"
          disabled={!canManage}
          {...register('checkoutSteps', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('settings.cartCheckout.failedToSave')}</p>}

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

export function CartCheckoutSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:cart-checkout:system:view')
  const canManageSystem = hasPermission('settings:cart-checkout:system:manage')
  const canManageTenant = hasPermission('settings:cart-checkout:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.cartCheckout.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.cartCheckout.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.cartCheckout.title')}</h1>

      <div className="flex gap-2 border-b border-line">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-brand-action text-brand-action'
                : 'text-ink-muted hover:text-ink'
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
