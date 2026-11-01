import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { barcodeMappingsApi } from '@/api/barcodeMappings.api'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, FormField, Input, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

export function BarcodeResolvePage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      setNewBarcode('')
      setNewProductId('')
    },
  })

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">{t('inventory.barcode.title')}</h1>
      <p className="max-w-2xl text-sm text-ink-secondary">{t('inventory.barcode.description')}</p>

      <div className="card max-w-xl p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('inventory.barcode.resolve')}</h2>
        <form
          className="flex items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            setSearchedCode(code.trim())
          }}
        >
          <FormField className="flex-1" label={t('inventory.barcode.codePlaceholder')}>
            <Input autoFocus value={code} onChange={(e) => setCode(e.target.value)} />
          </FormField>
          <Button type="submit" disabled={!code.trim()}>
            {t('common.search')}
          </Button>
        </form>

        {searchedCode && (
          <div className="mt-4 border-t border-line pt-4 text-sm" aria-live="polite">
            {isLoading ? (
              <Spinner />
            ) : result ? (
              <dl className="space-y-2">
                <div className="flex justify-between">
                  <dt className="text-ink-secondary">{t('orders.product')}</dt>
                  <dd className="font-medium">{result.productName}</dd>
                </div>
                <div className="flex justify-between">
                  <dt className="text-ink-secondary">{t('inventory.available')}</dt>
                  <dd className="font-semibold text-success-soft-text">{result.availableQuantity}</dd>
                </div>
                {result.lotId && (
                  <div className="flex justify-between">
                    <dt className="text-ink-secondary">{t('inventory.lotNumber')}</dt>
                    <dd className="font-mono text-xs">{result.lotId}</dd>
                  </div>
                )}
                {result.serialNumber && (
                  <div className="flex justify-between">
                    <dt className="text-ink-secondary">{t('inventory.serialNumber')}</dt>
                    <dd className="font-mono text-xs">{result.serialNumber}</dd>
                  </div>
                )}
              </dl>
            ) : (
              isFetched && <Alert variant="warning">{t('inventory.barcode.notFound')}</Alert>
            )}
          </div>
        )}
      </div>

      {hasPermission('inventory:manage') && (
        <div className="card max-w-xl p-5">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('inventory.barcode.addMapping')}</h2>
          <form
            className="space-y-3"
            onSubmit={(e) => {
              e.preventDefault()
              if (newBarcode.trim() && newProductId.trim()) createMutation.mutate()
            }}
          >
            <div className="grid grid-cols-2 gap-3">
              <FormField label={t('inventory.barcode.codePlaceholder')} required>
                <Input value={newBarcode} onChange={(e) => setNewBarcode(e.target.value)} />
              </FormField>
              <FormField label={t('inventory.barcode.productIdPlaceholder')} required>
                <Input value={newProductId} onChange={(e) => setNewProductId(e.target.value)} />
              </FormField>
            </div>
            {createMutation.isError && <Alert variant="danger">{serverMessage(createMutation.error, t('common.failedToSave'))}</Alert>}
            <Button type="submit" disabled={!newBarcode.trim() || !newProductId.trim()} loading={createMutation.isPending}>
              {t('common.save')}
            </Button>
          </form>
        </div>
      )}
    </div>
  )
}
