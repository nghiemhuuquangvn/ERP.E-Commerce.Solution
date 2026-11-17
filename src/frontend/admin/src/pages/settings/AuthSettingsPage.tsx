import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, Modal, useToast } from '@/components/ui'
import {
  settingsApi,
  type SystemAuthSettings,
  type TenantAuthSettings,
} from '@/api/settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

/**
 * SEC-M6 (docs/business/Identity/BruteForceProtection.md BR-BF-08): authentication settings decide 2FA, lockout and
 * sessions for a whole shop, so saving them re-asks the password. An account that only signs in externally leaves it
 * empty and must have signed in within the last few minutes (the server decides).
 */
function ConfirmPasswordDialog({ open, saving, error, onCancel, onConfirm }: {
  open: boolean
  saving: boolean
  error: unknown
  onCancel: () => void
  onConfirm: (currentPassword: string) => void
}) {
  const { t } = useTranslation()
  const [password, setPassword] = useState('')
  useEffect(() => {
    if (!open) setPassword('')
  }, [open])

  return (
    <Modal
      open={open}
      title={t('settings.auth.confirmTitle')}
      onClose={onCancel}
      closeOnEsc={!saving}
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={saving}>{t('common.cancel')}</Button>
          <Button onClick={() => onConfirm(password)} loading={saving}>{t('common.save')}</Button>
        </>
      }
    >
      <p className="mb-3 text-sm text-ink-secondary">{t('settings.auth.confirmHint')}</p>
      {error ? <Alert variant="danger" className="mb-3">{serverMessage(error, t('settings.auth.failedToSave'))}</Alert> : null}
      <FormField label={t('auth.password')}>
        <Input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
      </FormField>
    </Modal>
  )
}

interface SystemFormValues {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
  sessionAbsoluteHours: number
  customerSessionTimeoutMinutes: number
  customerSessionAbsoluteDays: number
  accountLockThreshold: number
  accountLockWindowMinutes: number
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'system'],
    queryFn: () => settingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: {
      googleEnabled: false,
      microsoftEnabled: false,
      facebookEnabled: false,
      twitterEnabled: false,
      twoFactorEnabled: false,
      minPasswordLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireDigit: true,
      requireSymbol: true,
      maxLoginAttempts: 5,
      lockoutDurationMinutes: 15,
      sessionTimeoutMinutes: 15,
      sessionAbsoluteHours: 12,
      customerSessionTimeoutMinutes: 10080,
      customerSessionAbsoluteDays: 30,
      accountLockThreshold: 20,
      accountLockWindowMinutes: 60,
    },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  // SEC-M6: the values wait here until the person re-confirms with their password.
  const [pending, setPending] = useState<SystemFormValues | null>(null)
  const mutation = useMutation({
    mutationFn: (body: SystemFormValues & { currentPassword?: string }) => settingsApi.updateSystemSettings(body),
    onSuccess: (result: SystemAuthSettings) => {
      queryClient.setQueryData(['settings', 'system'], result)
      setPending(null)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => { mutation.reset(); setPending(v) })} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('googleEnabled')} label={t('settings.auth.googleEnabled')} />
      <Checkbox disabled={!canManage} {...register('microsoftEnabled')} label={t('settings.auth.microsoftEnabled')} />
      <Checkbox disabled={!canManage} {...register('facebookEnabled')} label={t('settings.auth.facebookEnabled')} />
      <Checkbox disabled={!canManage} {...register('twitterEnabled')} label={t('settings.auth.twitterEnabled')} />
      <Checkbox disabled={!canManage} {...register('twoFactorEnabled')} label={t('settings.auth.twoFactorEnabled')} />

      <h2 className="pt-2 text-sm font-semibold text-ink">{t('settings.auth.securitySectionTitle')}</h2>
      <FormField label={t('settings.auth.minPasswordLength')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('minPasswordLength', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('requireUppercase')} label={t('settings.auth.requireUppercase')} />
      <Checkbox disabled={!canManage} {...register('requireLowercase')} label={t('settings.auth.requireLowercase')} />
      <Checkbox disabled={!canManage} {...register('requireDigit')} label={t('settings.auth.requireDigit')} />
      <Checkbox disabled={!canManage} {...register('requireSymbol')} label={t('settings.auth.requireSymbol')} />
      <FormField label={t('settings.auth.maxLoginAttempts')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('maxLoginAttempts', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.lockoutDurationMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('lockoutDurationMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.sessionTimeoutMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('sessionTimeoutMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.sessionAbsoluteHours')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('sessionAbsoluteHours', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.customerSessionTimeoutMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('customerSessionTimeoutMinutes', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.customerSessionAbsoluteDays')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('customerSessionAbsoluteDays', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.accountLockThreshold')} hint={t('settings.auth.accountLockHint')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('accountLockThreshold', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.accountLockWindowMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('accountLockWindowMinutes', { valueAsNumber: true })}
        />
      </FormField>

      <ConfirmPasswordDialog
        open={pending !== null}
        saving={mutation.isPending}
        error={mutation.error}
        onCancel={() => setPending(null)}
        onConfirm={(currentPassword) => pending && mutation.mutate({ ...pending, currentPassword })}
      />

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
  twoFactorRequired: boolean
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
  sessionAbsoluteHours: number
  customerSessionTimeoutMinutes: number
  customerSessionAbsoluteDays: number
  accountLockThreshold: number
  accountLockWindowMinutes: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'tenant', tenantId],
    queryFn: () => settingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: {
      googleEnabled: false,
      microsoftEnabled: false,
      facebookEnabled: false,
      twitterEnabled: false,
      twoFactorEnabled: false,
      twoFactorRequired: false,
      minPasswordLength: 8,
      requireUppercase: true,
      requireLowercase: true,
      requireDigit: true,
      requireSymbol: true,
      maxLoginAttempts: 5,
      lockoutDurationMinutes: 15,
      sessionTimeoutMinutes: 15,
      sessionAbsoluteHours: 12,
      customerSessionTimeoutMinutes: 10080,
      customerSessionAbsoluteDays: 30,
      accountLockThreshold: 20,
      accountLockWindowMinutes: 60,
    },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  // SEC-M6: the values wait here until the person re-confirms with their password.
  const [pending, setPending] = useState<TenantFormValues | null>(null)
  const mutation = useMutation({
    mutationFn: (body: TenantFormValues & { currentPassword?: string }) => settingsApi.updateTenantSettings(tenantId, body),
    onSuccess: (result: TenantAuthSettings) => {
      queryClient.setQueryData(['settings', 'tenant', tenantId], result)
      setPending(null)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.auth.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => { mutation.reset(); setPending(v) })} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('googleEnabled')} label={t('settings.auth.googleEnabled')} />
      <Checkbox disabled={!canManage} {...register('microsoftEnabled')} label={t('settings.auth.microsoftEnabled')} />
      <Checkbox disabled={!canManage} {...register('facebookEnabled')} label={t('settings.auth.facebookEnabled')} />
      <Checkbox disabled={!canManage} {...register('twitterEnabled')} label={t('settings.auth.twitterEnabled')} />
      <Checkbox disabled={!canManage} {...register('twoFactorEnabled')} label={t('settings.auth.twoFactorEnabled')} />
      <Checkbox disabled={!canManage} {...register('twoFactorRequired')} label={t('settings.auth.twoFactorRequired')} />

      <h2 className="pt-2 text-sm font-semibold text-ink">{t('settings.auth.securitySectionTitle')}</h2>
      <FormField label={t('settings.auth.minPasswordLength')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('minPasswordLength', { valueAsNumber: true })}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('requireUppercase')} label={t('settings.auth.requireUppercase')} />
      <Checkbox disabled={!canManage} {...register('requireLowercase')} label={t('settings.auth.requireLowercase')} />
      <Checkbox disabled={!canManage} {...register('requireDigit')} label={t('settings.auth.requireDigit')} />
      <Checkbox disabled={!canManage} {...register('requireSymbol')} label={t('settings.auth.requireSymbol')} />
      <FormField label={t('settings.auth.maxLoginAttempts')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('maxLoginAttempts', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.lockoutDurationMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('lockoutDurationMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.sessionTimeoutMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('sessionTimeoutMinutes', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.auth.sessionAbsoluteHours')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('sessionAbsoluteHours', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.customerSessionTimeoutMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('customerSessionTimeoutMinutes', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.customerSessionAbsoluteDays')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('customerSessionAbsoluteDays', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.accountLockThreshold')} hint={t('settings.auth.accountLockHint')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('accountLockThreshold', { valueAsNumber: true })}
        />
      </FormField>
      <FormField label={t('settings.auth.accountLockWindowMinutes')}>
        <Input
          type="number"
          disabled={!canManage}
          {...register('accountLockWindowMinutes', { valueAsNumber: true })}
        />
      </FormField>

      <ConfirmPasswordDialog
        open={pending !== null}
        saving={mutation.isPending}
        error={mutation.error}
        onCancel={() => setPending(null)}
        onConfirm={(currentPassword) => pending && mutation.mutate({ ...pending, currentPassword })}
      />

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function AuthSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:system:view')
  const canManageSystem = hasPermission('settings:system:manage')
  const canManageTenant = hasPermission('settings:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.auth.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.auth.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.auth.title')}</h1>

      <TabList id="authsettings" label={t('settings.auth.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
