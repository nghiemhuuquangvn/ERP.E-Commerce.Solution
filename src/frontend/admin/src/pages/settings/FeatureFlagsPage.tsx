import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  featureFlagsApi,
  type SystemFeatureFlag,
  type TenantFeatureFlag,
} from '@/api/settings/featureFlags.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface NewFlagFormValues {
  key: string
  isEnabled: boolean
  description: string
}

function SystemFlagsPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'feature-flags', 'system'],
    queryFn: () => featureFlagsApi.getSystemFlags(),
    staleTime: 30_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['settings', 'feature-flags', 'system'] })

  const toggleMutation = useMutation({
    mutationFn: (flag: SystemFeatureFlag) =>
      featureFlagsApi.upsertSystemFlag(flag.key, { isEnabled: !flag.isEnabled, description: flag.description }),
    onSuccess: invalidate,
  })

  const { register, handleSubmit, reset } = useForm<NewFlagFormValues>({
    defaultValues: { key: '', isEnabled: true, description: '' },
  })

  const createMutation = useMutation({
    mutationFn: (values: NewFlagFormValues) =>
      featureFlagsApi.upsertSystemFlag(values.key, {
        isEnabled: values.isEnabled,
        description: values.description || null,
      }),
    onSuccess: () => {
      invalidate()
      reset()
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <div className="space-y-6">
      <table className="w-full max-w-2xl text-sm">
        <thead>
          <tr className="border-b border-gray-200 text-left text-gray-500">
            <th className="py-2">{t('settings.featureFlags.key')}</th>
            <th className="py-2">{t('settings.featureFlags.description')}</th>
            <th className="py-2">{t('settings.featureFlags.enabled')}</th>
          </tr>
        </thead>
        <tbody>
          {(data ?? []).map((flag) => (
            <tr key={flag.key} className="border-b border-gray-100">
              <td className="py-2 font-mono text-xs">{flag.key}</td>
              <td className="py-2 text-gray-600">{flag.description ?? ''}</td>
              <td className="py-2">
                <input
                  type="checkbox"
                  checked={flag.isEnabled}
                  disabled={!canManage || toggleMutation.isPending}
                  onChange={() => toggleMutation.mutate(flag)}
                />
              </td>
            </tr>
          ))}
          {(data ?? []).length === 0 && (
            <tr>
              <td colSpan={3} className="py-4 text-gray-400">
                {t('settings.featureFlags.noFlags')}
              </td>
            </tr>
          )}
        </tbody>
      </table>

      {canManage && (
        <form
          onSubmit={handleSubmit((v) => createMutation.mutate(v))}
          className="max-w-md space-y-3 border-t border-gray-200 pt-4"
        >
          <h2 className="text-sm font-semibold text-gray-800">{t('settings.featureFlags.addFlag')}</h2>
          <label className="block text-sm text-gray-700">
            {t('settings.featureFlags.key')}
            <input
              type="text"
              placeholder="my-feature-key"
              {...register('key', { required: true })}
              className="input mt-1 w-full"
            />
          </label>
          <label className="block text-sm text-gray-700">
            {t('settings.featureFlags.description')}
            <input type="text" {...register('description')} className="input mt-1 w-full" />
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" {...register('isEnabled')} />
            {t('settings.featureFlags.enabled')}
          </label>

          {createMutation.isError && (
            <p className="text-xs text-red-600">{t('settings.featureFlags.failedToSave')}</p>
          )}

          <button type="submit" disabled={createMutation.isPending} className="btn-primary">
            {createMutation.isPending ? t('common.saving') : t('settings.featureFlags.addFlag')}
          </button>
        </form>
      )}
    </div>
  )
}

function TenantFlagsPanel({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'feature-flags', 'tenant', tenantId],
    queryFn: () => featureFlagsApi.getTenantFlags(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const toggleMutation = useMutation({
    mutationFn: (flag: TenantFeatureFlag) =>
      featureFlagsApi.setTenantFlagOverride(tenantId, flag.key, { isEnabled: !flag.isEnabled }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['settings', 'feature-flags', 'tenant', tenantId] }),
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.featureFlags.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <table className="w-full max-w-2xl text-sm">
      <thead>
        <tr className="border-b border-gray-200 text-left text-gray-500">
          <th className="py-2">{t('settings.featureFlags.key')}</th>
          <th className="py-2">{t('settings.featureFlags.description')}</th>
          <th className="py-2">{t('settings.featureFlags.enabled')}</th>
          <th className="py-2">{t('settings.featureFlags.overridden')}</th>
        </tr>
      </thead>
      <tbody>
        {(data ?? []).map((flag) => (
          <tr key={flag.key} className="border-b border-gray-100">
            <td className="py-2 font-mono text-xs">{flag.key}</td>
            <td className="py-2 text-gray-600">{flag.description ?? ''}</td>
            <td className="py-2">
              <input
                type="checkbox"
                checked={flag.isEnabled}
                disabled={!canManage || toggleMutation.isPending}
                onChange={() => toggleMutation.mutate(flag)}
              />
            </td>
            <td className="py-2 text-xs text-gray-400">
              {flag.isOverridden ? t('settings.featureFlags.overridden') : t('settings.featureFlags.inherited')}
            </td>
          </tr>
        ))}
        {(data ?? []).length === 0 && (
          <tr>
            <td colSpan={4} className="py-4 text-gray-400">
              {t('settings.featureFlags.noFlags')}
            </td>
          </tr>
        )}
      </tbody>
    </table>
  )
}

export function FeatureFlagsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:feature-flags:system:view')
  const canManageSystem = hasPermission('settings:feature-flags:system:manage')
  const canManageTenant = hasPermission('settings:feature-flags:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.featureFlags.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.featureFlags.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.featureFlags.title')}</h1>

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

      {tab === 'system' && canViewSystem && <SystemFlagsPanel canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantFlagsPanel canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
