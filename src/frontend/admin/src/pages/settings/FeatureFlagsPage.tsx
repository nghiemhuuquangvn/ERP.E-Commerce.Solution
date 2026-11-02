import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { serverMessage } from '@/lib/serverMessage'
import {
  featureFlagsApi,
  type SystemFeatureFlag,
  type TenantFeatureFlag,
} from '@/api/settings/featureFlags.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
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
  const toast = useToast()
  const [confirmFlag, setConfirmFlag] = useState<SystemFeatureFlag | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'feature-flags', 'system'],
    queryFn: () => featureFlagsApi.getSystemFlags(),
    staleTime: 30_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['settings', 'feature-flags', 'system'] })

  const toggleMutation = useMutation({
    mutationFn: (flag: SystemFeatureFlag) =>
      featureFlagsApi.upsertSystemFlag(flag.key, { isEnabled: !flag.isEnabled, description: flag.description }),
    onSuccess: () => {
      invalidate()
      setConfirmFlag(null)
      toast.success(t('common.saved'))
    },
    onError: (e) => {
      setConfirmFlag(null)
      toast.error(serverMessage(e, t('settings.featureFlags.failedToSave')))
    },
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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <div className="space-y-6">
      <table className="w-full max-w-2xl text-sm">
        <thead>
          <tr className="border-b border-line text-left text-ink-muted">
            <th scope="col" className="py-2">{t('settings.featureFlags.key')}</th>
            <th scope="col" className="py-2">{t('settings.featureFlags.description')}</th>
            <th scope="col" className="py-2">{t('settings.featureFlags.enabled')}</th>
          </tr>
        </thead>
        <tbody>
          {(data ?? []).map((flag) => (
            <tr key={flag.key} className="border-b border-line">
              <td className="py-2 font-mono text-xs">{flag.key}</td>
              <td className="py-2 text-ink-secondary">{flag.description ?? ''}</td>
              <td className="py-2">
                <input
                  type="checkbox"
                  checked={flag.isEnabled}
                  aria-label={`${t('settings.featureFlags.enabled')}: ${flag.key}`}
                  disabled={!canManage || toggleMutation.isPending}
                  onChange={() => setConfirmFlag(flag)}
                />
              </td>
            </tr>
          ))}
          {(data ?? []).length === 0 && (
            <tr>
              <td colSpan={3} className="py-4 text-ink-muted">
                {t('settings.featureFlags.noFlags')}
              </td>
            </tr>
          )}
        </tbody>
      </table>

      {canManage && (
        <form
          onSubmit={handleSubmit((v) => createMutation.mutate(v))}
          className="max-w-md space-y-3 border-t border-line pt-4"
        >
          <h2 className="text-sm font-semibold text-ink">{t('settings.featureFlags.addFlag')}</h2>
          <FormField label={t('settings.featureFlags.key')}>
            <Input
              type="text"
              placeholder="my-feature-key"
              {...register('key', { required: true })}
            />
          </FormField>
          <FormField label={t('settings.featureFlags.description')}>
            <Input type="text" {...register('description')} />
          </FormField>
          <Checkbox {...register('isEnabled')} label={t('settings.featureFlags.enabled')} />

          {createMutation.isError && <Alert variant="danger">{serverMessage(createMutation.error, t('settings.featureFlags.failedToSave'))}</Alert>}

          <Button type="submit" loading={createMutation.isPending}>{t('settings.featureFlags.addFlag')}</Button>
        </form>
      )}

      <ConfirmDialog
        open={!!confirmFlag}
        title={t('settings.featureFlags.confirmTitle')}
        message={confirmFlag ? t(confirmFlag.isEnabled ? 'settings.featureFlags.confirmOff' : 'settings.featureFlags.confirmOn', { key: confirmFlag.key }) : ''}
        confirmLabel={t('common.confirm')}
        onConfirm={() => confirmFlag && !toggleMutation.isPending && toggleMutation.mutate(confirmFlag)}
        onCancel={() => setConfirmFlag(null)}
      />
    </div>
  )
}

function TenantFlagsPanel({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'feature-flags', 'tenant', tenantId],
    queryFn: () => featureFlagsApi.getTenantFlags(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const toggleMutation = useMutation({
    mutationFn: (flag: TenantFeatureFlag) =>
      featureFlagsApi.setTenantFlagOverride(tenantId, flag.key, { isEnabled: !flag.isEnabled }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings', 'feature-flags', 'tenant', tenantId] })
      toast.success(t('common.saved'))
    },
    onError: (e) => toast.error(serverMessage(e, t('settings.featureFlags.failedToSave'))),
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.featureFlags.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <table className="w-full max-w-2xl text-sm">
      <thead>
        <tr className="border-b border-line text-left text-ink-muted">
          <th scope="col" className="py-2">{t('settings.featureFlags.key')}</th>
          <th scope="col" className="py-2">{t('settings.featureFlags.description')}</th>
          <th scope="col" className="py-2">{t('settings.featureFlags.enabled')}</th>
          <th scope="col" className="py-2">{t('settings.featureFlags.overridden')}</th>
        </tr>
      </thead>
      <tbody>
        {(data ?? []).map((flag) => (
          <tr key={flag.key} className="border-b border-line">
            <td className="py-2 font-mono text-xs">{flag.key}</td>
            <td className="py-2 text-ink-secondary">{flag.description ?? ''}</td>
            <td className="py-2">
              <input
                type="checkbox"
                checked={flag.isEnabled}
                aria-label={`${t('settings.featureFlags.enabled')}: ${flag.key}`}
                disabled={!canManage || toggleMutation.isPending}
                onChange={() => toggleMutation.mutate(flag)}
              />
            </td>
            <td className="py-2 text-xs text-ink-muted">
              {flag.isOverridden ? t('settings.featureFlags.overridden') : t('settings.featureFlags.inherited')}
            </td>
          </tr>
        ))}
        {(data ?? []).length === 0 && (
          <tr>
            <td colSpan={4} className="py-4 text-ink-muted">
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

      <TabList id="featureflags" label={t('settings.featureFlags.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemFlagsPanel canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantFlagsPanel canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
