import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useNavigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'

export function PurchaseOrderFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [poNumber, setPoNumber] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [expectedDeliveryDate, setExpectedDeliveryDate] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: purchaseOrdersApi.create,
    onSuccess: (po) => navigate(`/orders/purchase/${po.id}`),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!poNumber.trim() || !supplierId.trim()) {
      setError(t('purchaseOrders.form.requiredFields'))
      return
    }
    mutation.mutate({
      poNumber: poNumber.trim(),
      supplierId: supplierId.trim(),
      expectedDeliveryDate: expectedDeliveryDate || undefined,
      notes: notes.trim() || undefined,
    })
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/orders/purchase" className="text-sm text-ink-muted hover:text-ink">
          {t('purchaseOrders.backToPurchaseOrders')}
        </Link>
        <h1 className="text-xl font-semibold">{t('purchaseOrders.newPurchaseOrder')}</h1>
      </div>

      <form onSubmit={handleSubmit} className="card max-w-md space-y-4 p-5">
        <div>
          <label className="mb-1 block text-sm font-medium text-ink-secondary">{t('purchaseOrders.poNumber')}</label>
          <input className="input" value={poNumber} onChange={(e) => setPoNumber(e.target.value)} />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-ink-secondary">{t('purchaseOrders.form.supplierId')}</label>
          <input
            className="input"
            value={supplierId}
            onChange={(e) => setSupplierId(e.target.value)}
            placeholder={t('purchaseOrders.form.supplierIdPlaceholder')}
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-ink-secondary">{t('purchaseOrders.expectedDelivery')}</label>
          <input
            type="date"
            className="input"
            value={expectedDeliveryDate}
            onChange={(e) => setExpectedDeliveryDate(e.target.value)}
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-ink-secondary">{t('purchaseOrders.notes')}</label>
          <textarea className="input" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>

        {error && <p className="text-sm text-danger-soft-text">{error}</p>}
        {mutation.isError && <p className="text-sm text-danger-soft-text">{t('common.failedToSave')}</p>}

        <div className="flex justify-end gap-3 pt-2">
          <Link to="/orders/purchase" className="btn-outline">
            {t('common.cancel')}
          </Link>
          <button type="submit" disabled={mutation.isPending} className="btn-primary">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </div>
  )
}
