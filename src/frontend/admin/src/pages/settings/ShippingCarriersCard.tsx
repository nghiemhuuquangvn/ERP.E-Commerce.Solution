import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shippingSettingsApi, type CarrierAccount } from '@/api/settings/shipping.settings.api'
import { serverMessage } from '@/api/purchasing/procurement.api'

/** What each carrier calls its fields; the labels shown to the admin come from `settings.shipping.carriers.fields.*`. */
const FIELDS: Record<string, { publicId?: string; username?: string; secret: string }> = {
  GHTK: { publicId: 'partnerCode', secret: 'apiToken' },
  GHN: { publicId: 'shopId', secret: 'apiToken' },
  VTP: { username: 'loginName', secret: 'password' },
  JNT: { publicId: 'customerCode', secret: 'apiKey' },
}

function CarrierCard({ tenantId, account, canManage }: { tenantId: string; account: CarrierAccount; canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const fields = FIELDS[account.code] ?? FIELDS.GHTK
  const [enabled, setEnabled] = useState(account.enabled)
  const [sandbox, setSandbox] = useState(account.sandbox)
  const [publicId, setPublicId] = useState(account.publicId ?? '')
  const [username, setUsername] = useState(account.username ?? '')
  const [secret, setSecret] = useState('')
  const [webhookSecret, setWebhookSecret] = useState('')

  useEffect(() => {
    setEnabled(account.enabled)
    setSandbox(account.sandbox)
    setPublicId(account.publicId ?? '')
    setUsername(account.username ?? '')
  }, [account])

  const save = useMutation({
    mutationFn: () =>
      shippingSettingsApi.updateCarrierAccount(tenantId, account.code, {
        enabled,
        sandbox,
        publicId: fields.publicId ? publicId : undefined,
        username: fields.username ? username : undefined,
        // Empty means "keep the stored value": secrets are never sent back blank.
        secret: secret || undefined,
        webhookSecret: webhookSecret || undefined,
      }),
    onSuccess: () => {
      setSecret('')
      setWebhookSecret('')
      queryClient.invalidateQueries({ queryKey: ['settings', 'shipping', 'carriers', tenantId] })
    },
  })

  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  const secretBox = (label: string, value: string, set: (v: string) => void, isSet: boolean) => (
    <label className="block text-sm text-gray-700">
      {t(`settings.shipping.carriers.fields.${label}`)}
      <input
        type="password"
        autoComplete="new-password"
        disabled={!canManage || !account.integrated}
        value={value}
        onChange={(e) => set(e.target.value)}
        placeholder={isSet ? t('settings.shipping.carriers.secretSet') : t('settings.shipping.carriers.secretUnset')}
        className="input mt-1 w-full"
      />
    </label>
  )

  const editable = canManage && account.integrated

  return (
    <div className="card space-y-3 p-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-900">{account.name}</h3>
        <span
          className={`rounded-full px-2 py-0.5 text-xs font-medium ${
            !account.integrated ? 'bg-amber-100 text-amber-700' : account.enabled ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'
          }`}
        >
          {!account.integrated
            ? t('settings.shipping.carriers.notIntegrated')
            : account.enabled
              ? t('settings.shipping.carriers.on')
              : t('settings.shipping.carriers.off')}
        </span>
      </div>

      {!account.platformAvailable && <p className="text-xs text-amber-700">{t('settings.shipping.carriers.platformOff')}</p>}
      {!account.integrated && <p className="text-xs text-gray-500">{t('settings.shipping.carriers.notIntegratedHint')}</p>}

      <div className="flex gap-6">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!editable} checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
          {t('settings.shipping.carriers.enable')}
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!editable} checked={sandbox} onChange={(e) => setSandbox(e.target.checked)} />
          {t('settings.shipping.carriers.sandbox')}
        </label>
      </div>

      {fields.publicId && (
        <label className="block text-sm text-gray-700">
          {t(`settings.shipping.carriers.fields.${fields.publicId}`)}
          <input disabled={!editable} value={publicId} onChange={(e) => setPublicId(e.target.value)} className="input mt-1 w-full" />
        </label>
      )}
      {fields.username && (
        <label className="block text-sm text-gray-700">
          {t(`settings.shipping.carriers.fields.${fields.username}`)}
          <input disabled={!editable} value={username} onChange={(e) => setUsername(e.target.value)} className="input mt-1 w-full" />
        </label>
      )}
      {secretBox(fields.secret, secret, setSecret, account.hasSecret)}
      {secretBox('webhookSecret', webhookSecret, setWebhookSecret, account.hasWebhookSecret)}

      {account.integrated && (
        <p className="rounded bg-gray-50 p-2 text-xs text-gray-500">
          {t('settings.shipping.carriers.webhookHint')}
          <br />
          <span className="break-all font-mono">{`${origin}/api/v1/shipping-webhooks/${account.code.toLowerCase()}?hash=<${t('settings.shipping.carriers.fields.webhookSecret')}>`}</span>
        </p>
      )}

      {save.isError && <p className="text-xs text-red-600">{serverMessage(save.error, t('settings.shipping.failedToSave'))}</p>}
      {save.isSuccess && <p className="text-xs text-green-700">{t('settings.shipping.carriers.saved')}</p>}

      {editable && (
        <div className="flex justify-end">
          <button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </div>
  )
}

/** The shop's accounts at GHTK, Viettel Post, GHN and J&T. Secrets are written, never read back. */
export function ShippingCarriersCard({ tenantId, canManage }: { tenantId: string; canManage: boolean }) {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'shipping', 'carriers', tenantId],
    queryFn: () => shippingSettingsApi.getCarrierAccounts(tenantId),
    enabled: !!tenantId,
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <div className="space-y-3">
      <div>
        <h2 className="text-base font-semibold text-gray-900">{t('settings.shipping.carriers.title')}</h2>
        <p className="text-xs text-gray-500">{t('settings.shipping.carriers.hint')}</p>
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2 xl:grid-cols-4">
        {(data ?? []).map((a) => (
          <CarrierCard key={a.code} tenantId={tenantId} account={a} canManage={canManage} />
        ))}
      </div>
    </div>
  )
}
