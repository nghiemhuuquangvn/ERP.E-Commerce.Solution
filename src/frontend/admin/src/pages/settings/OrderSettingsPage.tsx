import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  orderSettingsApi,
  type SystemOrderSettings,
  type TenantOrderSettings,
} from '@/api/settings/order.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customOrderNumberFormatAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'order', 'system'],
    queryFn: () => orderSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customOrderNumberFormatAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => orderSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemOrderSettings) => {
      queryClient.setQueryData(['settings', 'order', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('customOrderNumberFormatAllowed')} label={t('settings.order.customOrderNumberFormatAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.order.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  orderNumberPrefix: string
  autoCancelUnpaidAfterHours: number
  allowEditAfterSubmit: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'order', 'tenant', tenantId],
    queryFn: () => orderSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { orderNumberPrefix: 'SO-', autoCancelUnpaidAfterHours: 24, allowEditAfterSubmit: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      orderSettingsApi.updateTenantSettings(tenantId, {
        orderNumberPrefix: values.orderNumberPrefix,
        autoCancelUnpaidAfterHours: Number(values.autoCancelUnpaidAfterHours),
        allowEditAfterSubmit: values.allowEditAfterSubmit,
      }),
    onSuccess: (result: TenantOrderSettings) => {
      queryClient.setQueryData(['settings', 'order', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.order.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.order.orderNumberPrefix')}>
        <Input type="text" disabled={!canManage} {...register('orderNumberPrefix')} />
      </FormField>
      <FormField label={t('settings.order.autoCancelUnpaidAfterHours')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('autoCancelUnpaidAfterHours', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('allowEditAfterSubmit')} label={t('settings.order.allowEditAfterSubmit')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.order.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function OrderSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:order:system:view')
  const canManageSystem = hasPermission('settings:order:system:manage')
  const canManageTenant = hasPermission('settings:order:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.order.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.order.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.order.title')}</h1>

      <TabList id="ordersettings" label={t('settings.order.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
