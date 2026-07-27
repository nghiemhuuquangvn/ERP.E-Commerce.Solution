import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { barcodeMappingsApi } from '@/api/barcodeMappings.api'
import { usePermission } from '@/hooks/usePermission'

export function BarcodeResolvePage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [code, setCode] = useState('')
  const [searchedCode, setSearchedCode] = useState<string | null>(null)
  const [newBarcode, setNewBarcode] = useState('')
  const [newProductId, setNewProductId] = useState('')

  const { data: result, isLoading, isFetched } = useQuery({
    queryKey: ['barcode', searchedCode],
    queryFn: () => barcodeMappingsApi.resolve(searchedCode!),
    enabled: Boolean(searchedCode),
  })

  const createMutation = useMutation({
    mutationFn: () => barcodeMappingsApi.create({ barcode: newBarcode, productId: newProductId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['barcode'] })
      setNewBarcode('')
      setNewProductId('')
    },
  })

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">{t('inventory.barcode.title')}</h1>
      <p className="max-w-2xl text-sm text-gray-500">{t('inventory.barcode.description')}</p>

      <div className="card max-w-xl p-5">
        <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('inventory.barcode.resolve')}</h2>
        <div className="flex gap-2">
          <input
            className="input flex-1"
            placeholder={t('inventory.barcode.codePlaceholder')}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && setSearchedCode(code.trim())}
          />
          <button className="btn-primary" onClick={() => setSearchedCode(code.trim())}>
            {t('common.search')}
          </button>
        </div>

        {searchedCode && (
          <div className="mt-4 border-t border-gray-100 pt-4 text-sm">
            {isLoading ? (
              <p className="text-gray-400">{t('common.loading')}</p>
            ) : result ? (
              <dl className="space-y-2">
                <div className="flex justify-between">
                  <dt className="text-gray-500">{t('orders.product')}</dt>
                  <dd className="font-medium">{result.productName}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-gray-500">{t('inventory.available')}</dt>
                  <dd className="font-semibold text-green-700">{result.availableQuantity}</dd>
                </div>
                {result.lotId && (
                  <div className="flex justify-between">
                    <dt className="text-gray-500">{t('inventory.lotNumber')}</dt>
                    <dd className="font-mono text-xs">{result.lotId}</dd>
                  </div>
                )}
                {result.serialNumber && (
                  <div className="flex justify-between">
                    <dt className="text-gray-500">{t('inventory.serialNumber')}</dt>
                    <dd className="font-mono text-xs">{result.serialNumber}</dd>
                  </div>
                )}
              </dl>
            ) : (
              isFetched && <p className="text-xs text-gray-400">{t('inventory.barcode.notFound')}</p>
            )}
          </div>
        )}
      </div>

      {hasPermission('inventory:manage') && (
        <div className="card max-w-xl p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('inventory.barcode.addMapping')}</h2>
          <div className="grid grid-cols-2 gap-3">
            <input className="input" placeholder={t('inventory.barcode.codePlaceholder')} value={newBarcode} onChange={(e) => setNewBarcode(e.target.value)} />
            <input className="input" placeholder={t('inventory.barcode.productIdPlaceholder')} value={newProductId} onChange={(e) => setNewProductId(e.target.value)} />
          </div>
          {createMutation.isError && <p className="mt-2 text-xs text-red-600">{t('common.failedToSave')}</p>}
          <button
            className="btn-primary mt-3"
            disabled={!newBarcode.trim() || !newProductId.trim() || createMutation.isPending}
            onClick={() => createMutation.mutate()}
          >
            {createMutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </div>
  )
}
