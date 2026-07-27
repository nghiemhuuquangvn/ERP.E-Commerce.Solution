import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useNavigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { transferOrdersApi } from '@/api/transferOrders.api'
import { locationsApi } from '@/api/locations.api'
import { productsApi } from '@/api/products.api'

interface DraftLine {
  key: number
  productId: string
  quantity: number
}

let nextKey = 1

export function TransferOrderFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [fromLocationId, setFromLocationId] = useState('')
  const [toLocationId, setToLocationId] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([{ key: nextKey++, productId: '', quantity: 1 }])
  const [error, setError] = useState<string | null>(null)

  const { data: locations } = useQuery({ queryKey: ['locations'], queryFn: () => locationsApi.list(true), staleTime: 60_000 })
  const { data: productsPage } = useQuery({
    queryKey: ['products', 'picker'],
    queryFn: () => productsApi.list({ skip: 0, take: 100 }),
    staleTime: 60_000,
  })
  const products = productsPage?.products ?? []

  const mutation = useMutation({
    mutationFn: transferOrdersApi.create,
    onSuccess: () => navigate('/inventory/transfers'),
  })

  const addLine = () => setLines((prev) => [...prev, { key: nextKey++, productId: '', quantity: 1 }])
  const removeLine = (key: number) => setLines((prev) => prev.filter((l) => l.key !== key))
  const updateLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!fromLocationId || !toLocationId) {
      setError(t('inventory.transfers.locationsRequired'))
      return
    }
    if (fromLocationId === toLocationId) {
      setError(t('inventory.transfers.sameLocationError'))
      return
    }
    const validLines = lines.filter((l) => l.productId && l.quantity > 0)
    if (validLines.length === 0) {
      setError(t('orders.form.lineRequired'))
      return
    }
    mutation.mutate({
      fromLocationId,
      toLocationId,
      lines: validLines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
      notes: notes || undefined,
    })
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/inventory/transfers" className="text-sm text-gray-500 hover:text-gray-700">
          {t('inventory.transfers.backToTransfers')}
        </Link>
        <h1 className="text-xl font-semibold">{t('inventory.transfers.new')}</h1>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="card p-5">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.transfers.from')}</label>
              <select className="input" value={fromLocationId} onChange={(e) => setFromLocationId(e.target.value)}>
                <option value="">{t('common.select')}</option>
                {(locations ?? []).map((l) => (
                  <option key={l.locationId} value={l.locationId}>{l.locationCode}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.transfers.to')}</label>
              <select className="input" value={toLocationId} onChange={(e) => setToLocationId(e.target.value)}>
                <option value="">{t('common.select')}</option>
                {(locations ?? []).map((l) => (
                  <option key={l.locationId} value={l.locationId}>{l.locationCode}</option>
                ))}
              </select>
            </div>
            <div className="col-span-2">
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.notes')}</label>
              <input className="input" value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
        </div>

        <div className="card p-5">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.lineItems')}</h2>
            <button type="button" onClick={addLine} className="text-xs text-primary-600 hover:underline">
              {t('orders.form.addLine')}
            </button>
          </div>
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                <th className="pb-2">{t('orders.product')}</th>
                <th className="pb-2 text-right">{t('orders.qty')}</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {lines.map((line) => (
                <tr key={line.key}>
                  <td className="py-2">
                    <select className="input" value={line.productId} onChange={(e) => updateLine(line.key, { productId: e.target.value })}>
                      <option value="">{t('common.select')}</option>
                      {products.map((p) => (
                        <option key={p.id} value={p.id}>{p.name}</option>
                      ))}
                    </select>
                  </td>
                  <td className="py-2 text-right">
                    <input
                      type="number"
                      min={1}
                      className="input w-20 text-right"
                      value={line.quantity}
                      onChange={(e) => updateLine(line.key, { quantity: Number(e.target.value) })}
                    />
                  </td>
                  <td className="py-2 text-right">
                    {lines.length > 1 && (
                      <button type="button" onClick={() => removeLine(line.key)} className="text-xs text-red-600 hover:underline">
                        {t('orders.form.removeLine')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}
        {mutation.isError && <p className="text-sm text-red-600">{t('common.failedToSave')}</p>}

        <div className="flex justify-end gap-3">
          <Link to="/inventory/transfers" className="btn-outline">
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
