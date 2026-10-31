import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { loyaltyApi } from '@/api/promotion/promotion.api'
import type { LoyaltyAccount } from '@/api/promotion/promotion.api'
import { Alert, Button, FormField, Input, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const errorText = (e: unknown, fallback: string) =>
  (e as { response?: { status?: number; data?: { detail?: string; message?: string } } })?.response?.data?.detail ??
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback

/** Look a customer's loyalty account up by user id and adjust the points by hand (customer service). */
export function LoyaltyPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const canManage = hasPermission('promotions:manage')
  const [userId, setUserId] = useState('')
  const [account, setAccount] = useState<LoyaltyAccount | null>(null)
  const [points, setPoints] = useState(100)
  const [reason, setReason] = useState('')
  const validId = GUID.test(userId.trim())

  const lookup = useMutation({
    mutationFn: () => loyaltyApi.get(userId.trim()),
    onSuccess: setAccount,
    onError: () => setAccount(null),
  })
  const earn = useMutation({
    mutationFn: () => loyaltyApi.earn(userId.trim(), { points, reason: reason.trim() }),
    onSuccess: (a) => { setAccount(a); queryClient.invalidateQueries({ queryKey: ['promotion', 'loyalty'] }); toast.success(t('promotion.loyalty.done')) },
  })
  const redeem = useMutation({
    mutationFn: () => loyaltyApi.redeem(userId.trim(), { points }),
    onSuccess: (a) => { setAccount(a); toast.success(t('promotion.loyalty.done')) },
  })

  const notFound = (lookup.error as { response?: { status?: number } } | null)?.response?.status === 404

  return (
    <div className="max-w-2xl space-y-4">
      <h1 className="text-xl font-semibold">{t('promotion.loyalty.title')}</h1>

      <div className="card space-y-3 p-4">
        <div className="flex items-end gap-3">
          <FormField className="flex-1" label={t('promotion.loyalty.userId')} error={userId && !validId ? t('promotion.loyalty.invalidId') : undefined}>
            <Input value={userId} onChange={(e) => setUserId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" />
          </FormField>
          <Button disabled={!validId} loading={lookup.isPending} onClick={() => lookup.mutate()}>{t('promotion.loyalty.lookup')}</Button>
        </div>
        {notFound && <Alert variant="info">{t('promotion.loyalty.noAccount')}</Alert>}
        {lookup.isError && !notFound && <Alert variant="danger">{errorText(lookup.error, t('promotion.loyalty.failed'))}</Alert>}
      </div>

      {account && (
        <div className="card space-y-4 p-4">
          <dl className="grid grid-cols-2 gap-3 text-sm md:grid-cols-4">
            <div><dt className="text-ink-secondary">{t('promotion.loyalty.tier')}</dt><dd className="font-semibold">{t(`promotion.tier.${account.tier}`)}</dd></div>
            <div><dt className="text-ink-secondary">{t('promotion.loyalty.available')}</dt><dd className="font-semibold">{account.availablePoints}</dd></div>
            <div><dt className="text-ink-secondary">{t('promotion.loyalty.total')}</dt><dd className="font-semibold">{account.totalPoints}</dd></div>
            <div>
              <dt className="text-ink-secondary">{t('promotion.loyalty.toNext')}</dt>
              <dd className="font-semibold">{account.nextTier ? `${account.pointsToNextTier} → ${t(`promotion.tier.${account.nextTier}`)}` : '—'}</dd>
            </div>
          </dl>

          {canManage && (
            <div className="grid grid-cols-3 items-end gap-3 border-t border-line pt-4">
              <FormField label={t('promotion.loyalty.points')}>
                <Input type="number" min={1} value={points} onChange={(e) => setPoints(Number(e.target.value))} />
              </FormField>
              <FormField className="col-span-2" label={t('promotion.loyalty.reason')}>
                <Input value={reason} onChange={(e) => setReason(e.target.value)} />
              </FormField>
              <Button disabled={points < 1 || !reason.trim()} loading={earn.isPending} onClick={() => earn.mutate()}>{t('promotion.loyalty.earn')}</Button>
              <Button variant="outline" disabled={points < 1} loading={redeem.isPending} onClick={() => redeem.mutate()}>{t('promotion.loyalty.redeem')}</Button>
            </div>
          )}
          {(earn.isError || redeem.isError) && <Alert variant="danger">{errorText(earn.error ?? redeem.error, t('promotion.loyalty.failed'))}</Alert>}
        </div>
      )}
    </div>
  )
}
