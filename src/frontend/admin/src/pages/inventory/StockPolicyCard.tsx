import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { inventoryApi } from '@/api/inventory.api'
import type { Inventory } from '@/api/inventory.api'
import { Alert, Badge, Button, FormField, Input, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

/**
 * Replenishment policy of one product (ROADMAP P1-9): when available stock plus goods on order is at or below the minimum,
 * Purchasing proposes an order that brings it up to the maximum. The safety stock is the part of the minimum kept for surprises.
 */
export function StockPolicyCard({ inventory, canEdit }: { inventory: Inventory; canEdit: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [min, setMin] = useState(inventory.minStock)
  const [max, setMax] = useState(inventory.maxStock)
  const [safety, setSafety] = useState(inventory.safetyStock)

  useEffect(() => {
    setMin(inventory.minStock)
    setMax(inventory.maxStock)
    setSafety(inventory.safetyStock)
  }, [inventory.minStock, inventory.maxStock, inventory.safetyStock])

  const mutation = useMutation({
    mutationFn: () => inventoryApi.updateStockPolicy(inventory.productId, { minStock: min, maxStock: max, safetyStock: safety }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inventory'] })
      toast.success(t('common.saved'))
    },
  })

  const valid = safety >= 0 && min >= safety && max > min
  const dirty = min !== inventory.minStock || max !== inventory.maxStock || safety !== inventory.safetyStock

  return (
    <div className="card space-y-3 p-5">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-ink">{t('inventory.policy.title')}</h2>
        {inventory.isLowStock && <Badge variant="warning">{t('inventory.policy.belowMinimum')}</Badge>}
      </div>
      <p className="text-xs text-ink-secondary">{t('inventory.policy.hint')}</p>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {([
          ['minStock', min, setMin],
          ['safetyStock', safety, setSafety],
          ['maxStock', max, setMax],
        ] as const).map(([key, value, setter]) => (
          <FormField key={key} label={t(`inventory.policy.${key}`)}>
            <Input type="number" min={0} step="any" value={value} disabled={!canEdit} onChange={(e) => setter(Number(e.target.value))} />
          </FormField>
        ))}
      </div>
      {!valid && <Alert variant="warning">{t('inventory.policy.invalid')}</Alert>}
      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      {canEdit && (
        <div className="flex justify-end">
          <Button disabled={!valid || !dirty} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {t('common.save')}
          </Button>
        </div>
      )}
    </div>
  )
}
