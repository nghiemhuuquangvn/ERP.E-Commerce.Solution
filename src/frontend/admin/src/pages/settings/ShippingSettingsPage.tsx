import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  shippingSettingsApi,
  type SystemShippingSettings,
  type TenantShippingSettings,
} from '@/api/settings/shipping.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'
import { ShippingCarriersCard } from './ShippingCarriersCard'
import { CarrierRulesCard } from './CarrierRulesCard'
import { ShippingFeeRulesCard } from './ShippingFeeRulesCard'

type Tab = 'system' | 'tenant' | 'fees' | 'carriers' | 'rules'

type SystemFormValues = SystemShippingSettings

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'shipping', 'system'],
    queryFn: () => shippingSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customCarriersAllowed: true, ghtkAvailable: true, vtpAvailable: true, ghnAvailable: true, jntAvailable: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => shippingSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemShippingSettings) => {
      queryClient.setQueryData(['settings', 'shipping', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('customCarriersAllowed')} label={t('settings.shipping.customCarriersAllowed')} />

      <div className="space-y-2 rounded border border-line p-3">
        <p className="text-xs text-ink-muted">{t('settings.shipping.carriersAvailableHint')}</p>
        <Checkbox disabled={!canManage} {...register('ghtkAvailable')} label="GHTK" />
        <Checkbox disabled={!canManage} {...register('vtpAvailable')} label="Viettel Post" />
        <Checkbox disabled={!canManage} {...register('ghnAvailable')} label="GHN" />
        <Checkbox disabled={!canManage} {...register('jntAvailable')} label="J&T Express" />
      </div>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.shipping.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

type TenantFormValues = Omit<TenantShippingSettings, 'tenantId'>

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'shipping', 'tenant', tenantId],
    queryFn: () => shippingSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { defaultShippingFee: 0, freeShippingThreshold: 0, defaultItemWeightGrams: 500 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      shippingSettingsApi.updateTenantSettings(tenantId, {
        ...values,
        defaultShippingFee: Number(values.defaultShippingFee),
        freeShippingThreshold: Number(values.freeShippingThreshold),
        defaultItemWeightGrams: Number(values.defaultItemWeightGrams),
      }),
    onSuccess: (result: TenantShippingSettings) => {
      queryClient.setQueryData(['settings', 'shipping', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.shipping.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  const text = (name: keyof TenantFormValues, label: string) => (
    <FormField label={t(label)}>
      <Input disabled={!canManage} {...register(name)} />
    </FormField>
  )

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-xl space-y-4">
      <p className="text-xs text-ink-muted">{t('settings.shipping.feeHint')}</p>
      <FormField label={t('settings.shipping.defaultShippingFee')}>
        <Input type="number" step="0.01" disabled={!canManage} {...register('defaultShippingFee', { valueAsNumber: true })} />
      </FormField>
      <FormField label={t('settings.shipping.freeShippingThreshold')}>
        <Input type="number" step="0.01" disabled={!canManage} {...register('freeShippingThreshold', { valueAsNumber: true })} />
      </FormField>

      <div className="space-y-3 rounded border border-line p-4">
        <div>
          <h2 className="text-sm font-semibold text-ink">{t('settings.shipping.sender.title')}</h2>
          <p className="text-xs text-ink-muted">{t('settings.shipping.sender.hint')}</p>
        </div>
        <div className="grid grid-cols-2 gap-3">
          {text('senderName', 'settings.shipping.sender.name')}
          {text('senderPhone', 'settings.shipping.sender.phone')}
        </div>
        {text('senderAddress', 'settings.shipping.sender.address')}
        <div className="grid grid-cols-3 gap-3">
          {text('senderWard', 'settings.shipping.sender.ward')}
          {text('senderDistrict', 'settings.shipping.sender.district')}
          {text('senderProvince', 'settings.shipping.sender.province')}
        </div>
        <FormField label={t('settings.shipping.sender.defaultWeight')} hint={t('settings.shipping.sender.defaultWeightHint')}>
          <Input type="number" min={1} disabled={!canManage} {...register('defaultItemWeightGrams', { valueAsNumber: true })} />
        </FormField>
      </div>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.shipping.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function ShippingSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:shipping:system:view')
  const canManageSystem = hasPermission('settings:shipping:system:manage')
  const canManageTenant = hasPermission('settings:shipping:tenant:manage')
  const canManageRules = hasPermission('shipping:carriers:manage')
  const tenantId = user?.tenantId ?? ''

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.shipping.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.shipping.tenantTab') },
    { key: 'fees' as Tab, label: t('settings.shipping.feesTab') },
    { key: 'carriers' as Tab, label: t('settings.shipping.carriersTab') },
    { key: 'rules' as Tab, label: t('settings.shipping.rulesTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.shipping.title')}</h1>

      <TabList id="shippingsettings" label={t('settings.shipping.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && <TenantSettingsForm canManage={canManageTenant} tenantId={tenantId} />}
      {tab === 'fees' && <ShippingFeeRulesCard canManage={canManageRules} />}
      {tab === 'carriers' && <ShippingCarriersCard tenantId={tenantId} canManage={canManageTenant} />}
      {tab === 'rules' && <CarrierRulesCard canManage={canManageRules} />}
    </div>
  )
}
