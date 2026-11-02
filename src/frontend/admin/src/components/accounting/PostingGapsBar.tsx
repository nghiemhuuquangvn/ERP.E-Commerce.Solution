import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { postingGapsApi } from '@/api/accounting/posting-gaps.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Button, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

/** Tells the accountant that some invoices / bills have no journal entry and offers to post them (ROADMAP P1-3b). */
export function PostingGapsBar({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [confirming, setConfirming] = useState(false)
  const { data } = useQuery({ queryKey: ['accounting', 'posting-gaps'], queryFn: () => postingGapsApi.list(), staleTime: 15_000 })
  const repost = useMutation({
    mutationFn: () => postingGapsApi.repost(),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['accounting'] })
      setConfirming(false)
      const failed = result.filter((g) => !g.posted).length
      if (failed > 0) toast.info(t('accounting.vat.gapsStill', { count: failed }))
      else toast.success(t('accounting.vat.gapsDone'))
    },
    onError: (e) => {
      setConfirming(false)
      toast.error(serverMessage(e, t('accounting.vat.failed')))
    },
  })
  if (!data || data.length === 0) return null
  return (
    <Alert variant="warning">
      <p>{t('accounting.vat.gapsMessage', { count: data.length })}</p>
      {canManage && (
        <div className="mt-2">
          <Button variant="outline" size="sm" onClick={() => setConfirming(true)}>
            {t('accounting.vat.postMissing')}
          </Button>
        </div>
      )}
      <ConfirmDialog
        open={confirming}
        title={t('accounting.vat.postMissing')}
        message={t('accounting.vat.postMissingConfirm', { count: data.length })}
        confirmLabel={repost.isPending ? t('common.saving') : t('accounting.vat.postMissing')}
        onConfirm={() => !repost.isPending && repost.mutate()}
        onCancel={() => setConfirming(false)}
      />
    </Alert>
  )
}
