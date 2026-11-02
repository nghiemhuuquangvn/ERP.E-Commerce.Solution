import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  cartCheckoutSettingsApi,
  type SystemCartCheckoutSettings,
  type TenantCartCheckoutSettings,
} from '@/api/settings/cartCheckout.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  guestCheckoutAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('guestCheckoutAllowed')} label={t('settings.cartCheckout.guestCheckoutAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.cartCheckout.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.cartCheckout.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.cartCheckout.cartHoldMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('cartHoldMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('requireRegistrationAtCheckout')} label={t('settings.cartCheckout.requireRegistrationAtCheckout')} />
      <FormField label={t('settings.cartCheckout.checkoutSteps')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('checkoutSteps', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.cartCheckout.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="cartcheckoutsettings" label={t('settings.cartCheckout.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
