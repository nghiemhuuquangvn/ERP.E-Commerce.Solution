import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { postingGapsApi } from '@/api/accounting/posting-gaps.api'

/** Tells the accountant that some invoices / bills have no journal entry and offers to post them (ROADMAP P1-3b). */
export function PostingGapsBar({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['accounting', 'posting-gaps'], queryFn: () => postingGapsApi.list(), staleTime: 15_000 })
  const repost = useMutation({
    mutationFn: () => postingGapsApi.repost(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['accounting'] }),
  })
  if (!data || data.length === 0) return null
  const failed = repost.data?.filter((g) => !g.posted).length ?? 0
  return (
    <div className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-800">
      <p>{t('accounting.vat.gapsMessage', { count: data.length })}</p>
      {canManage && (
        <div className="mt-2 flex items-center gap-3">
          <button className="btn-outline" disabled={repost.isPending} onClick={() => repost.mutate()}>
            {t('accounting.vat.postMissing')}
          </button>
          {repost.isSuccess && <span className="text-xs">{failed > 0 ? t('accounting.vat.gapsStill', { count: failed }) : t('accounting.vat.gapsDone')}</span>}
        </div>
      )}
    </div>
  )
}
