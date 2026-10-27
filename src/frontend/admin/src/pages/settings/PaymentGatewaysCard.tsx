import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { paymentSettingsApi, type GatewayCredential, type GatewayProvider } from '@/api/settings/payment.settings.api'
import { serverMessage } from '@/api/purchasing/procurement.api'

/** What each gateway calls its fields; the labels shown to the admin come from here. */
const FIELDS: Record<GatewayProvider, { publicId: string; access?: string; secret1: string; secret2?: string }> = {
  VNPay: { publicId: 'tmnCode', secret1: 'hashSecret' },
  MoMo: { publicId: 'partnerCode', access: 'accessKey', secret1: 'secretKey' },
  ZaloPay: { publicId: 'appId', secret1: 'key1', secret2: 'key2' },
}

function GatewayCard({ tenantId, credential, canManage }: { tenantId: string; credential: GatewayCredential; canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const fields = FIELDS[credential.provider]
  const [enabled, setEnabled] = useState(credential.enabled)
  const [sandbox, setSandbox] = useState(credential.sandbox)
  const [publicId, setPublicId] = useState(credential.publicId ?? '')
  const [accessKey, setAccessKey] = useState('')
  const [secret1, setSecret1] = useState('')
  const [secret2, setSecret2] = useState('')

  useEffect(() => {
    setEnabled(credential.enabled)
    setSandbox(credential.sandbox)
    setPublicId(credential.publicId ?? '')
  }, [credential])

  const save = useMutation({
    mutationFn: () =>
      paymentSettingsApi.updateGateway(tenantId, credential.provider, {
        enabled,
        sandbox,
        publicId,
        // Empty means "keep the stored value": secrets are never sent back blank.
        accessKey: accessKey || undefined,
        secret1: secret1 || undefined,
        secret2: secret2 || undefined,
      }),
    onSuccess: () => {
      setAccessKey('')
      setSecret1('')
      setSecret2('')
      queryClient.invalidateQueries({ queryKey: ['settings', 'payment', 'gateways', tenantId] })
    },
  })

  const origin = typeof window !== 'undefined' ? window.location.origin : ''
  const code = credential.provider.toLowerCase()
  const secretBox = (label: string, value: string, set: (v: string) => void, isSet: boolean) => (
    <label className="block text-sm text-gray-700">
      {t(`settings.payment.gateways.fields.${label}`)}
      <input
        type="password"
        autoComplete="new-password"
        disabled={!canManage}
        value={value}
        onChange={(e) => set(e.target.value)}
        placeholder={isSet ? t('settings.payment.secretKeyPlaceholderSet') : t('settings.payment.secretKeyPlaceholderUnset')}
        className="input mt-1 w-full"
      />
    </label>
  )

  return (
    <div className="card space-y-3 p-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-900">{credential.provider}</h3>
        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${credential.enabled ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
          {credential.enabled ? t('settings.payment.gateways.on') : t('settings.payment.gateways.off')}
        </span>
      </div>

      <div className="flex gap-6">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
          {t('settings.payment.gateways.enable')}
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} checked={sandbox} onChange={(e) => setSandbox(e.target.checked)} />
          {t('settings.payment.sandboxMode')}
        </label>
      </div>

      <label className="block text-sm text-gray-700">
        {t(`settings.payment.gateways.fields.${fields.publicId}`)}
        <input disabled={!canManage} value={publicId} onChange={(e) => setPublicId(e.target.value)} className="input mt-1 w-full" />
      </label>
      {fields.access && secretBox(fields.access, accessKey, setAccessKey, credential.hasAccessKey)}
      {secretBox(fields.secret1, secret1, setSecret1, credential.hasSecret1)}
      {fields.secret2 && secretBox(fields.secret2, secret2, setSecret2, credential.hasSecret2)}

      {credential.provider !== 'VNPay' && (
        <p className="rounded bg-gray-50 p-2 text-xs text-gray-500">
          {t('settings.payment.gateways.notifyHint')}
          <br />
          <span className="font-mono">{`${origin}/api/v1/payment-gateway/ipn/${code}`}</span>
        </p>
      )}

      {save.isError && <p className="text-xs text-red-600">{serverMessage(save.error, t('settings.payment.failedToSave'))}</p>}
      {save.isSuccess && <p className="text-xs text-green-700">{t('settings.payment.gateways.saved')}</p>}

      {canManage && (
        <div className="flex justify-end">
          <button className="btn-primary" disabled={save.isPending} onClick={() => save.mutate()}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </div>
  )
}

/** The tenant's merchant accounts at VNPay, MoMo and ZaloPay. Secrets are written, never read back. */
export function PaymentGatewaysCard({ tenantId, canManage }: { tenantId: string; canManage: boolean }) {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'payment', 'gateways', tenantId],
    queryFn: () => paymentSettingsApi.getGateways(tenantId),
    enabled: !!tenantId,
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <div className="space-y-3">
      <div>
        <h2 className="text-base font-semibold text-gray-900">{t('settings.payment.gateways.title')}</h2>
        <p className="text-xs text-gray-500">{t('settings.payment.gateways.hint')}</p>
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {(data ?? []).map((c) => (
          <GatewayCard key={c.provider} tenantId={tenantId} credential={c} canManage={canManage} />
        ))}
      </div>
    </div>
  )
}
