import { useState, useMemo } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useNavigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ordersApi } from '@/api/orders.api'
import { productsApi } from '@/api/products.api'

interface DraftLine {
  key: number
  productId: string
  quantity: number
  unitPrice: number
}

let nextKey = 1

export function OrderFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [customerId, setCustomerId] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([{ key: nextKey++, productId: '', quantity: 1, unitPrice: 0 }])
  const [error, setError] = useState<string | null>(null)

  const { data: productsPage } = useQuery({
    queryKey: ['products', 'picker'],
    queryFn: () => productsApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
  })
  const products = productsPage?.items ?? []

  const mutation = useMutation({
    mutationFn: ordersApi.create,
    onSuccess: (order) => navigate(`/orders/${order.id}`),
  })

  const total = useMemo(
    () => lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0),
    [lines],
  )

  const updateLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const handleProductChange = (key: number, productId: string) => {
    const product = products.find((p) => p.id === productId)
    updateLine(key, { productId, unitPrice: product?.price ?? 0 })
  }

  const addLine = () => setLines((prev) => [...prev, { key: nextKey++, productId: '', quantity: 1, unitPrice: 0 }])
  const removeLine = (key: number) => setLines((prev) => prev.filter((l) => l.key !== key))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!customerId.trim()) {
      setError(t('orders.form.customerRequired'))
      return
    }
    const validLines = lines.filter((l) => l.productId && l.quantity > 0)
    if (validLines.length === 0) {
      setError(t('orders.form.lineRequired'))
      return
    }
    mutation.mutate({
      customerId: customerId.trim(),
      lines: validLines.map((l) => ({ productId: l.productId, quantity: l.quantity, unitPrice: l.unitPrice })),
    })
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/orders" className="text-sm text-gray-500 hover:text-gray-700">
          {t('orders.backToOrders')}
        </Link>
        <h1 className="text-xl font-semibold">{t('orders.newOrder')}</h1>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.orderInfo')}</h2>
          <div className="max-w-sm">
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.form.customerId')}</label>
            <input
              className="input"
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
              placeholder={t('orders.form.customerIdPlaceholder')}
            />
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
                <th className="pb-2 text-right">{t('orders.unitPrice')}</th>
                <th className="pb-2 text-right">{t('orders.total')}</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {lines.map((line) => (
                <tr key={line.key}>
                  <td className="py-2">
                    <select
                      className="input"
                      value={line.productId}
                      onChange={(e) => handleProductChange(line.key, e.target.value)}
                    >
                      <option value="">{t('common.select')}</option>
                      {products.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name}
                        </option>
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
                    <input
                      type="number"
                      min={0}
                      step="0.01"
                      className="input w-28 text-right"
                      value={line.unitPrice}
                      onChange={(e) => updateLine(line.key, { unitPrice: Number(e.target.value) })}
                    />
                  </td>
                  <td className="py-2 text-right font-medium">
                    {(line.quantity * line.unitPrice).toFixed(2)}
                  </td>
                  <td className="py-2 text-right">
                    {lines.length > 1 && (
                      <button
                        type="button"
                        onClick={() => removeLine(line.key)}
                        className="text-xs text-red-600 hover:underline"
                      >
                        {t('orders.form.removeLine')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-gray-200">
                <td colSpan={3} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
                <td className="pt-3 text-right font-semibold text-gray-900">{total.toFixed(2)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>

        {error && <p className="text-sm text-red-600">{error}</p>}
        {mutation.isError && <p className="text-sm text-red-600">{t('common.failedToSave')}</p>}

        <div className="flex justify-end gap-3">
          <Link to="/orders" className="btn-outline">
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
