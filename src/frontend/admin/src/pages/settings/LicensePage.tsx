import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  licenseApi,
  type LicenseModule,
  type LicensePlan,
  type TenantLicense,
} from '@/api/license/license.api'
import { tenantsApi } from '@/api/tenants.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'

type Tab = 'modules' | 'plans' | 'tenant'

function ModuleRow({ mod, canManage }: { mod: LicenseModule; canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(mod.moduleName)

  const mutation = useMutation({
    mutationFn: (body: { moduleName: string; isActive: boolean }) => licenseApi.upsertModule(mod.moduleCode, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['license', 'modules'] })
      setEditing(false)
    },
  })

  return (
    <tr className="border-b border-line">
      <td className="py-2 font-mono text-xs">{mod.moduleCode}</td>
      <td className="py-2">
        {editing ? (
          <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
        ) : (
          mod.moduleName
        )}
      </td>
      <td className="py-2">
        <input
          type="checkbox"
          checked={mod.isActive}
          disabled={!canManage || mutation.isPending}
          onChange={() => mutation.mutate({ moduleName: mod.moduleName, isActive: !mod.isActive })}
        />
      </td>
      {canManage && (
        <td className="py-2">
          {editing ? (
            <div className="flex gap-2">
              <button
                type="button"
                className="text-xs text-brand-action hover:underline"
                disabled={mutation.isPending}
                onClick={() => mutation.mutate({ moduleName: name, isActive: mod.isActive })}
              >
                {t('common.save')}
              </button>
              <button
                type="button"
                className="text-xs text-ink-muted hover:underline"
                onClick={() => {
                  setEditing(false)
                  setName(mod.moduleName)
                }}
              >
                {t('common.cancel')}
              </button>
            </div>
          ) : (
            <button type="button" className="text-xs text-brand-action hover:underline" onClick={() => setEditing(true)}>
              {t('common.edit')}
            </button>
          )}
        </td>
      )}
    </tr>
  )
}

function ModulesPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['license', 'modules'],
    queryFn: () => licenseApi.getModules(),
    staleTime: 30_000,
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <table className="w-full max-w-2xl text-sm">
      <thead>
        <tr className="border-b border-line text-left text-ink-muted">
          <th className="py-2">{t('license.modules.moduleCode')}</th>
          <th className="py-2">{t('license.modules.moduleName')}</th>
          <th className="py-2">{t('license.modules.active')}</th>
          {canManage && <th className="py-2"></th>}
        </tr>
      </thead>
      <tbody>
        {(data ?? []).map((m) => (
          <ModuleRow key={m.moduleCode} mod={m} canManage={canManage} />
        ))}
        {(data ?? []).length === 0 && (
          <tr>
            <td colSpan={canManage ? 4 : 3} className="py-4 text-ink-muted">
              {t('license.modules.noModules')}
            </td>
          </tr>
        )}
      </tbody>
    </table>
  )
}

interface PlanFormValues {
  planCode: string
  planName: string
  description: string
  maxUsers: string
  isActive: boolean
}

function PlanForm({
  modules,
  initial,
  onCancel,
  onSaved,
}: {
  modules: LicenseModule[]
  initial: LicensePlan | null
  onCancel: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const isEdit = !!initial
  const [moduleCodes, setModuleCodes] = useState<string[]>(initial?.moduleCodes ?? [])
  const { register, handleSubmit } = useForm<PlanFormValues>({
    defaultValues: {
      planCode: initial?.planCode ?? '',
      planName: initial?.planName ?? '',
      description: initial?.description ?? '',
      maxUsers: initial?.maxUsers != null ? String(initial.maxUsers) : '',
      isActive: initial?.isActive ?? true,
    },
  })

  const toggleModule = (code: string) => {
    setModuleCodes((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]))
  }

  const mutation = useMutation({
    mutationFn: (values: PlanFormValues) =>
      licenseApi.upsertPlan(values.planCode, {
        planName: values.planName,
        description: values.description || undefined,
        maxUsers: values.maxUsers ? Number(values.maxUsers) : undefined,
        isActive: values.isActive,
        moduleCodes,
      }),
    onSuccess: onSaved,
  })

  return (
    <form
      onSubmit={handleSubmit((v) => mutation.mutate(v))}
      className="max-w-md space-y-3 border-t border-line pt-4"
    >
      <h2 className="text-sm font-semibold text-ink">
        {isEdit ? t('license.plans.editPlan') : t('license.plans.addPlan')}
      </h2>
      <label className="block text-sm text-ink-secondary">
        {t('license.plans.planCode')}
        <input
          type="text"
          {...register('planCode', { required: true })}
          disabled={isEdit}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('license.plans.planName')}
        <input type="text" {...register('planName', { required: true })} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('license.plans.description')}
        <input type="text" {...register('description')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-ink-secondary">
        {t('license.plans.maxUsers')}
        <input type="number" min={0} {...register('maxUsers')} className="input mt-1 w-full" />
      </label>
      <label className="flex items-center gap-2 text-sm text-ink-secondary">
        <input type="checkbox" {...register('isActive')} />
        {t('license.plans.active')}
      </label>

      <div>
        <p className="mb-1 text-sm font-medium text-ink-secondary">{t('license.plans.modules')}</p>
        <div className="grid grid-cols-2 gap-1">
          {modules.map((m) => (
            <label key={m.moduleCode} className="flex items-center gap-2 text-sm text-ink-secondary">
              <input
                type="checkbox"
                checked={moduleCodes.includes(m.moduleCode)}
                onChange={() => toggleModule(m.moduleCode)}
              />
              {m.moduleName} ({m.moduleCode})
            </label>
          ))}
        </div>
      </div>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('common.failedToSave')}</p>}

      <div className="flex gap-3 pt-2">
        <button type="submit" disabled={mutation.isPending} className="btn-primary">
          {mutation.isPending ? t('common.saving') : t('common.save')}
        </button>
        <button type="button" onClick={onCancel} className="btn-outline">
          {t('common.cancel')}
        </button>
      </div>
    </form>
  )
}

function PlansPanel({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editingPlan, setEditingPlan] = useState<LicensePlan | null>(null)
  const [creating, setCreating] = useState(false)

  const { data: plans, isLoading } = useQuery({
    queryKey: ['license', 'plans'],
    queryFn: () => licenseApi.getPlans(),
    staleTime: 30_000,
  })
  const { data: modules } = useQuery({
    queryKey: ['license', 'modules'],
    queryFn: () => licenseApi.getModules(),
    staleTime: 30_000,
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['license', 'plans'] })
    setEditingPlan(null)
    setCreating(false)
  }

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <div className="space-y-6">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-line text-left text-ink-muted">
            <th className="py-2">{t('license.plans.planCode')}</th>
            <th className="py-2">{t('license.plans.planName')}</th>
            <th className="py-2">{t('license.plans.maxUsers')}</th>
            <th className="py-2">{t('license.plans.modules')}</th>
            <th className="py-2">{t('common.status')}</th>
            {canManage && <th className="py-2"></th>}
          </tr>
        </thead>
        <tbody>
          {(plans ?? []).map((p) => (
            <tr key={p.id} className="border-b border-line">
              <td className="py-2 font-mono text-xs">{p.planCode}</td>
              <td className="py-2">{p.planName}</td>
              <td className="py-2">{p.maxUsers ?? '—'}</td>
              <td className="py-2 text-xs text-ink-muted">{p.moduleCodes.join(', ') || '—'}</td>
              <td className="py-2">
                <StatusBadge
                  status={p.isActive ? 'Active' : 'Cancelled'}
                  label={p.isActive ? t('common.activate') : t('common.deactivate')}
                />
              </td>
              {canManage && (
                <td className="py-2">
                  <button
                    type="button"
                    className="text-xs text-brand-action hover:underline"
                    onClick={() => setEditingPlan(p)}
                  >
                    {t('common.edit')}
                  </button>
                </td>
              )}
            </tr>
          ))}
          {(plans ?? []).length === 0 && (
            <tr>
              <td colSpan={canManage ? 6 : 5} className="py-4 text-ink-muted">
                {t('license.plans.noPlans')}
              </td>
            </tr>
          )}
        </tbody>
      </table>

      {canManage && !creating && !editingPlan && (
        <button type="button" className="btn-primary" onClick={() => setCreating(true)}>
          {t('license.plans.addPlan')}
        </button>
      )}

      {canManage && (creating || editingPlan) && (
        <PlanForm
          modules={modules ?? []}
          initial={editingPlan}
          onCancel={() => {
            setCreating(false)
            setEditingPlan(null)
          }}
          onSaved={invalidate}
        />
      )}
    </div>
  )
}

interface IssueRenewFormValues {
  maxUsers: string
  startsAt: string
  expiresAt: string
  notes: string
}

function IssueOrRenewForm({
  tenantId,
  mode,
  plans,
  modules,
  current,
  onCancel,
  onSaved,
}: {
  tenantId: string
  mode: 'issue' | 'renew'
  plans: LicensePlan[]
  modules: LicenseModule[]
  current: TenantLicense | null
  onCancel: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const [planId, setPlanId] = useState(current?.planId ?? '')
  const [moduleCodes, setModuleCodes] = useState<string[]>(current?.moduleCodes ?? [])
  const { register, handleSubmit } = useForm<IssueRenewFormValues>({
    defaultValues: {
      maxUsers: current?.maxUsers != null ? String(current.maxUsers) : '',
      startsAt: '',
      expiresAt: current?.expiresAt ? current.expiresAt.slice(0, 10) : '',
      notes: '',
    },
  })

  const toggleModule = (code: string) => {
    setModuleCodes((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]))
  }

  const applyPlan = (id: string) => {
    setPlanId(id)
    const plan = plans.find((p) => p.id === id)
    if (plan) {
      setModuleCodes(plan.moduleCodes)
    }
  }

  const mutation = useMutation({
    mutationFn: (values: IssueRenewFormValues) => {
      const maxUsers = values.maxUsers ? Number(values.maxUsers) : undefined
      const expiresAt = values.expiresAt ? new Date(values.expiresAt).toISOString() : undefined
      const notes = values.notes.trim() ? values.notes.trim() : undefined

      if (mode === 'issue') {
        return licenseApi.issueTenantLicense(tenantId, {
          planId: planId || undefined,
          moduleCodes,
          maxUsers,
          startsAt: values.startsAt ? new Date(values.startsAt).toISOString() : undefined,
          expiresAt,
          notes,
        })
      }
      return licenseApi.renewTenantLicense(tenantId, { expiresAt, maxUsers, moduleCodes, notes })
    },
    onSuccess: onSaved,
  })

  return (
    <form
      onSubmit={handleSubmit((v) => mutation.mutate(v))}
      className="max-w-lg space-y-3 border-t border-line pt-4"
    >
      <h2 className="text-sm font-semibold text-ink">
        {mode === 'issue' ? t('license.tenant.issueTitle') : t('license.tenant.renewTitle')}
      </h2>

      <label className="block text-sm text-ink-secondary">
        {t('license.plans.title')}
        <select className="input mt-1 w-full" value={planId} onChange={(e) => applyPlan(e.target.value)}>
          <option value="">{t('license.tenant.customPlan')}</option>
          {plans.map((p) => (
            <option key={p.id} value={p.id}>
              {p.planName}
            </option>
          ))}
        </select>
      </label>

      {mode === 'issue' && (
        <label className="block text-sm text-ink-secondary">
          {t('license.tenant.startsAt')}
          <input type="date" {...register('startsAt')} className="input mt-1 w-full" />
        </label>
      )}

      <label className="block text-sm text-ink-secondary">
        {t('license.tenant.expiresAt')}
        <input type="date" {...register('expiresAt')} className="input mt-1 w-full" />
      </label>

      <label className="block text-sm text-ink-secondary">
        {t('license.plans.maxUsers')}
        <input type="number" min={0} {...register('maxUsers')} className="input mt-1 w-full" />
      </label>

      <div>
        <p className="mb-1 text-sm font-medium text-ink-secondary">{t('license.plans.modules')}</p>
        <div className="grid grid-cols-2 gap-1">
          {modules.map((m) => (
            <label key={m.moduleCode} className="flex items-center gap-2 text-sm text-ink-secondary">
              <input
                type="checkbox"
                checked={moduleCodes.includes(m.moduleCode)}
                onChange={() => toggleModule(m.moduleCode)}
              />
              {m.moduleName} ({m.moduleCode})
            </label>
          ))}
        </div>
      </div>

      <label className="block text-sm text-ink-secondary">
        {t('license.tenant.notes')}
        <textarea {...register('notes')} className="input mt-1 w-full" rows={2} />
      </label>

      {mutation.isError && <p className="text-xs text-danger-soft-text">{t('common.failedToSave')}</p>}

      <div className="flex gap-3 pt-2">
        <button type="submit" disabled={mutation.isPending} className="btn-primary">
          {mutation.isPending ? t('common.saving') : t('common.save')}
        </button>
        <button type="button" onClick={onCancel} className="btn-outline">
          {t('common.cancel')}
        </button>
      </div>
    </form>
  )
}

function TenantLicensePanel({ tenantId, canIssue }: { tenantId: string; canIssue: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [formMode, setFormMode] = useState<'none' | 'issue' | 'renew'>('none')
  const [suspendConfirm, setSuspendConfirm] = useState(false)
  const [revokeConfirm, setRevokeConfirm] = useState(false)

  const { data: license, isLoading } = useQuery({
    queryKey: ['license', 'tenant', tenantId],
    queryFn: () => licenseApi.getTenantLicense(tenantId),
    enabled: !!tenantId,
    staleTime: 15_000,
  })

  const { data: plans } = useQuery({
    queryKey: ['license', 'plans'],
    queryFn: () => licenseApi.getPlans(),
    enabled: canIssue,
    staleTime: 30_000,
  })

  const { data: modules } = useQuery({
    queryKey: ['license', 'modules'],
    queryFn: () => licenseApi.getModules(),
    enabled: canIssue,
    staleTime: 30_000,
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['license', 'tenant', tenantId] })
    setFormMode('none')
    setSuspendConfirm(false)
    setRevokeConfirm(false)
  }

  const suspendMutation = useMutation({
    mutationFn: () => licenseApi.suspendTenantLicense(tenantId),
    onSuccess: invalidate,
  })
  const revokeMutation = useMutation({
    mutationFn: () => licenseApi.revokeTenantLicense(tenantId),
    onSuccess: invalidate,
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.featureFlags.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  let badgeStatus = 'Active'
  let badgeLabel = t('license.tenant.status.Active')
  if (license) {
    if (license.status === 'Revoked') {
      badgeStatus = 'Cancelled'
      badgeLabel = t('license.tenant.status.Revoked')
    } else if (license.status === 'Suspended') {
      badgeStatus = 'Pending'
      badgeLabel = t('license.tenant.status.Suspended')
    } else if (!license.isCurrentlyValid) {
      badgeStatus = 'Refunded'
      badgeLabel = t('license.tenant.status.Expired')
    }
  }

  return (
    <div className="space-y-6">
      {license ? (
        <div className="card max-w-2xl space-y-3 p-4">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold text-ink">{t('license.tenant.currentLicense')}</h2>
            <StatusBadge status={badgeStatus} label={badgeLabel} />
          </div>
          <dl className="grid grid-cols-2 gap-2 text-sm">
            <dt className="text-ink-muted">{t('license.tenant.licenseKey')}</dt>
            <dd className="font-mono text-xs">{license.licenseKey}</dd>
            <dt className="text-ink-muted">{t('license.plans.maxUsers')}</dt>
            <dd>{license.maxUsers ?? '—'}</dd>
            <dt className="text-ink-muted">{t('license.tenant.issuedAt')}</dt>
            <dd>{new Date(license.issuedAt).toLocaleString()}</dd>
            <dt className="text-ink-muted">{t('license.tenant.startsAt')}</dt>
            <dd>{new Date(license.startsAt).toLocaleDateString()}</dd>
            <dt className="text-ink-muted">{t('license.tenant.expiresAt')}</dt>
            <dd>{license.expiresAt ? new Date(license.expiresAt).toLocaleDateString() : t('license.tenant.perpetual')}</dd>
            <dt className="text-ink-muted">{t('license.tenant.issuedBy')}</dt>
            <dd>{license.issuedBy}</dd>
            {license.notes && (
              <>
                <dt className="text-ink-muted">{t('license.tenant.notes')}</dt>
                <dd>{license.notes}</dd>
              </>
            )}
          </dl>
          <div>
            <p className="mb-1 text-sm font-medium text-ink-secondary">{t('license.plans.modules')}</p>
            <div className="flex flex-wrap gap-1">
              {license.moduleCodes.length === 0 && <span className="text-xs text-ink-muted">—</span>}
              {license.moduleCodes.map((code) => (
                <span key={code} className="badge bg-surface-muted text-ink-secondary">
                  {code}
                </span>
              ))}
            </div>
          </div>

          {canIssue && formMode === 'none' && (
            <div className="flex flex-wrap gap-3 border-t border-line pt-3">
              <button type="button" className="btn-primary" onClick={() => setFormMode('renew')}>
                {t('license.tenant.renew')}
              </button>
              {license.status !== 'Suspended' && (
                <button type="button" className="btn-outline" onClick={() => setSuspendConfirm(true)}>
                  {t('license.tenant.suspend')}
                </button>
              )}
              {license.status !== 'Revoked' && (
                <button type="button" className="btn-danger" onClick={() => setRevokeConfirm(true)}>
                  {t('license.tenant.revoke')}
                </button>
              )}
            </div>
          )}
        </div>
      ) : (
        <p className="text-sm text-ink-muted">{t('license.tenant.noLicense')}</p>
      )}

      {canIssue && !license && formMode === 'none' && (
        <button type="button" className="btn-primary" onClick={() => setFormMode('issue')}>
          {t('license.tenant.issue')}
        </button>
      )}

      {canIssue && (formMode === 'issue' || formMode === 'renew') && (
        <IssueOrRenewForm
          tenantId={tenantId}
          mode={formMode}
          plans={plans ?? []}
          modules={modules ?? []}
          current={license ?? null}
          onCancel={() => setFormMode('none')}
          onSaved={invalidate}
        />
      )}

      <ConfirmDialog
        open={suspendConfirm}
        title={t('license.tenant.suspendTitle')}
        message={t('license.tenant.suspendConfirm')}
        confirmLabel={t('license.tenant.suspend')}
        onConfirm={() => suspendMutation.mutate()}
        onCancel={() => setSuspendConfirm(false)}
      />
      <ConfirmDialog
        open={revokeConfirm}
        title={t('license.tenant.revokeTitle')}
        message={t('license.tenant.revokeConfirm')}
        confirmLabel={t('license.tenant.revoke')}
        onConfirm={() => revokeMutation.mutate()}
        onCancel={() => setRevokeConfirm(false)}
        danger
      />
    </div>
  )
}

export function LicensePage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canManageModules = hasPermission('license:modules:manage')
  const canViewPlans = hasPermission('license:plans:view')
  const canManagePlans = hasPermission('license:plans:manage')
  const canViewTenantLicense = hasPermission('license:tenant:view')
  const canIssue = hasPermission('license:tenant:issue')
  const canViewTenants = hasPermission('identity:tenants:view')

  const TABS: { key: Tab; label: string }[] = [
    ...(canManageModules ? [{ key: 'modules' as Tab, label: t('license.modules.tab') }] : []),
    ...(canViewPlans ? [{ key: 'plans' as Tab, label: t('license.plans.tab') }] : []),
    ...(canViewTenantLicense || canIssue ? [{ key: 'tenant' as Tab, label: t('license.tenant.tab') }] : []),
  ]

  const [tab, setTab] = useState<Tab>(TABS[0]?.key ?? 'tenant')
  const [selectedTenantId, setSelectedTenantId] = useState<string>('')

  const { data: tenants } = useQuery({
    queryKey: ['identity', 'tenants'],
    queryFn: () => tenantsApi.list(),
    enabled: canViewTenants,
    staleTime: 60_000,
  })

  const effectiveTenantId = canViewTenants && selectedTenantId ? selectedTenantId : user?.tenantId ?? ''

  if (TABS.length === 0) {
    return <p className="text-sm text-ink-muted">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('license.title')}</h1>

      <div className="flex gap-2 border-b border-line">
        {TABS.map((x) => (
          <button
            key={x.key}
            type="button"
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

      {tab === 'modules' && canManageModules && <ModulesPanel canManage={canManageModules} />}
      {tab === 'plans' && canViewPlans && <PlansPanel canManage={canManagePlans} />}
      {tab === 'tenant' && (canViewTenantLicense || canIssue) && (
        <div className="space-y-4">
          {canViewTenants && (
            <div className="max-w-xs">
              <label className="mb-1 block text-sm font-medium text-ink-secondary">{t('license.tenant.tenant')}</label>
              <select
                className="input"
                value={selectedTenantId}
                onChange={(e) => setSelectedTenantId(e.target.value)}
              >
                <option value="">{t('identity.users.myTenant')}</option>
                {(tenants ?? []).map((tn) => (
                  <option key={tn.id} value={tn.id}>
                    {tn.tenantName}
                  </option>
                ))}
              </select>
            </div>
          )}
          <TenantLicensePanel tenantId={effectiveTenantId} canIssue={canIssue} />
        </div>
      )}
    </div>
  )
}
