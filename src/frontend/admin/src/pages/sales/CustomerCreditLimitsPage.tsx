import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { customerSalesProfilesApi } from '@/api/customerSalesProfiles.api'
import { usePermission } from '@/hooks/usePermission'

export function CustomerCreditLimitsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [customerId, setCustomerId] = useState('')
  const [searchedId, setSearchedId] = useState<string | null>(null)
  const [creditLimitInput, setCreditLimitInput] = useState('')

  const { data: profile, isLoading } = useQuery({
    queryKey: ['customerSalesProfile', searchedId],
    queryFn: () => customerSalesProfilesApi.get(searchedId!),
    enabled: Boolean(searchedId),
  })

  const saveMutation = useMutation({
    mutationFn: (creditLimit: number) => customerSalesProfilesApi.setCreditLimit(searchedId!, creditLimit),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['customerSalesProfile', searchedId] }),
  })

  const handleSearch = () => {
    const trimmed = customerId.trim()
    if (!trimmed) return
    setSearchedId(trimmed)
    setCreditLimitInput('')
  }

  const fmt = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">{t('creditLimits.title')}</h1>
      <p className="max-w-2xl text-sm text-gray-500">{t('creditLimits.description')}</p>

      <div className="card max-w-xl p-5">
        <div className="flex gap-2">
          <input
            className="input flex-1"
            placeholder={t('creditLimits.customerIdPlaceholder')}
            value={customerId}
            onChange={(e) => setCustomerId(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          />
          <button onClick={handleSearch} className="btn-primary">
            {t('common.search')}
          </button>
        </div>

        {searchedId && (
          <div className="mt-5 space-y-4 border-t border-gray-100 pt-4">
            {isLoading ? (
              <p className="text-sm text-gray-400">{t('common.loading')}</p>
            ) : (
              <>
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500">{t('creditLimits.currentBalance')}</span>
                  <span className="font-semibold text-gray-900">{fmt(profile?.currentBalance ?? 0)}</span>
                </div>
                {!profile && <p className="text-xs text-gray-400">{t('creditLimits.noProfile')}</p>}

                {hasPermission('sales_order:approve') && (
                  <div>
                    <label className="mb-1 block text-sm font-medium text-gray-700">{t('creditLimits.creditLimit')}</label>
                    <div className="flex gap-2">
                      <input
                        type="number"
                        min={0}
                        step="0.01"
                        className="input flex-1"
                        placeholder={profile ? String(profile.creditLimit) : '0'}
                        value={creditLimitInput}
                        onChange={(e) => setCreditLimitInput(e.target.value)}
                      />
                      <button
                        disabled={!creditLimitInput.trim() || saveMutation.isPending}
                        onClick={() => saveMutation.mutate(Number(creditLimitInput))}
                        className="btn-primary"
                      >
                        {saveMutation.isPending ? t('common.saving') : t('common.save')}
                      </button>
                    </div>
                    {saveMutation.isSuccess && <p className="mt-1 text-xs text-green-600">{t('common.saved')}</p>}
                  </div>
                )}
              </>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
