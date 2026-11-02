import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, Select, useToast } from '@/components/ui'
import {
  taxSettingsApi,
  type SystemTaxSettings,
  type TenantTaxSettings,
} from '@/api/settings/tax.settings.api'
import { invoicesApi } from '@/api/accounting/invoices.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  regionalTaxConfigAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'tax', 'system'],
    queryFn: () => taxSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { regionalTaxConfigAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => taxSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemTaxSettings) => {
      queryClient.setQueryData(['settings', 'tax', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('regionalTaxConfigAllowed')} label={t('settings.tax.regionalTaxConfigAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.tax.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  companyTaxCode: string
  defaultTaxRatePercent: number
  pricesIncludeTax: boolean
  eInvoiceProvider: string
  eInvoiceSeries: string
  autoIssueEInvoice: boolean
  baseCurrencyCode: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'tax', 'tenant', tenantId],
    queryFn: () => taxSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { companyTaxCode: '', defaultTaxRatePercent: 0, pricesIncludeTax: true, eInvoiceProvider: '', eInvoiceSeries: '', autoIssueEInvoice: false, baseCurrencyCode: 'VND' },
  })

  useEffect(() => {
    if (data) {
      reset({
        companyTaxCode: data.companyTaxCode ?? '',
        defaultTaxRatePercent: data.defaultTaxRatePercent,
        pricesIncludeTax: data.pricesIncludeTax,
        eInvoiceProvider: data.eInvoiceProvider ?? '',
        eInvoiceSeries: data.eInvoiceSeries ?? '',
        autoIssueEInvoice: !!data.autoIssueEInvoice,
        baseCurrencyCode: data.baseCurrencyCode ?? 'VND',
      })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      taxSettingsApi.updateTenantSettings(tenantId, {
        companyTaxCode: values.companyTaxCode || null,
        defaultTaxRatePercent: Number(values.defaultTaxRatePercent),
        pricesIncludeTax: values.pricesIncludeTax,
        eInvoiceProvider: values.eInvoiceProvider || null,
        eInvoiceSeries: values.eInvoiceSeries || null,
        autoIssueEInvoice: values.autoIssueEInvoice,
        baseCurrencyCode: values.baseCurrencyCode.trim().toUpperCase() || 'VND',
      }),
    onSuccess: (result: TenantTaxSettings) => {
      queryClient.setQueryData(['settings', 'tax', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  const { data: providers } = useQuery({ queryKey: ['accounting', 'e-invoice-providers'], queryFn: () => invoicesApi.eInvoiceProviders(), staleTime: 300_000 })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.tax.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.tax.companyTaxCode')}>
        <Input type="text" disabled={!canManage} {...register('companyTaxCode')} />
      </FormField>
      <FormField label={t('settings.tax.defaultTaxRatePercent')}>
        <Input
          type="number"
          step="0.01"
          disabled={!canManage}
          {...register('defaultTaxRatePercent', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('pricesIncludeTax')} label={t('settings.tax.pricesIncludeTax')} />
      <FormField label={t('settings.tax.eInvoiceProvider')}>
        <Select disabled={!canManage} {...register('eInvoiceProvider')} >
          <option value="">{t('settings.tax.eInvoiceOff')}</option>
          {(providers ?? []).map((p) => (
            <option key={p} value={p}>
              {p}
            </option>
          ))}
        </Select>
      </FormField>
      <FormField label={t('settings.tax.eInvoiceSeries')}>
        <Input type="text" disabled={!canManage} {...register('eInvoiceSeries')}  placeholder="C26TAA" />
      </FormField>
      <FormField label={t('settings.tax.baseCurrency')}>
        <Input type="text" maxLength={3} disabled={!canManage} {...register('baseCurrencyCode')} className="w-32 uppercase" placeholder="VND" />
        <span className="mt-1 block text-xs text-ink-muted">{t('settings.tax.baseCurrencyHint')}</span>
      </FormField>
      <Checkbox disabled={!canManage} {...register('autoIssueEInvoice')} label={t('settings.tax.autoIssueEInvoice')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.tax.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function TaxSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:tax:system:view')
  const canManageSystem = hasPermission('settings:tax:system:manage')
  const canManageTenant = hasPermission('settings:tax:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.tax.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.tax.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.tax.title')}</h1>

      <TabList id="taxsettings" label={t('settings.tax.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
