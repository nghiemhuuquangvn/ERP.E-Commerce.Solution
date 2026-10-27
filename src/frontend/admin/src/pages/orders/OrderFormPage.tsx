import { useState, useMemo } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useNavigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ordersApi } from '@/api/orders.api'
import type { OrderSource } from '@/api/orders.api'
import type { Product } from '@/api/products.api'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'

interface DraftLine {
  key: number
  productId: string
  productName: string
  quantity: number
  unitPrice: number
}

let nextKey = 1

export function OrderFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [customerId, setCustomerId] = useState('')
  const [customerName, setCustomerName] = useState('')
  const [customerEmail, setCustomerEmail] = useState('')
  const [orderSource, setOrderSource] = useState<OrderSource>('Manual')
  const [salesRepId, setSalesRepId] = useState('')
  const [couponCode, setCouponCode] = useState('')
  const [address, setAddress] = useState({ street: '', ward: '', city: '', state: '', postalCode: '', country: 'US' })
  const [lines, setLines] = useState<DraftLine[]>([
    { key: nextKey++, productId: '', productName: '', quantity: 1, unitPrice: 0 },
  ])
  const [pickerOpen, setPickerOpen] = useState(false)
  const [activeRowKey, setActiveRowKey] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: ordersApi.create,
    onSuccess: (order) => navigate(`/orders/${order.orderId}`),
  })

  const total = useMemo(() => lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0), [lines])

  const updateLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const addLine = () =>
    setLines((prev) => [...prev, { key: nextKey++, productId: '', productName: '', quantity: 1, unitPrice: 0 }])

  const removeLine = (key: number) => setLines((prev) => prev.filter((l) => l.key !== key))

  const openPickerForRow = (key: number) => {
    setActiveRowKey(key)
    setPickerOpen(true)
  }

  const handleProductPicked = (products: Product[]) => {
    const product = products[0]
    if (!product || activeRowKey === null) return
    updateLine(activeRowKey, { productId: product.id, productName: product.name, unitPrice: product.price })
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!customerId.trim() || !customerName.trim() || !customerEmail.trim()) {
      setError(t('orders.form.customerRequired'))
      return
    }
    if (!address.street.trim() || !address.city.trim() || !address.state.trim() || !address.postalCode.trim()) {
      setError(t('orders.form.addressRequired'))
      return
    }
    const validLines = lines.filter((l) => l.productId && l.quantity > 0)
    if (validLines.length === 0) {
      setError(t('orders.form.lineRequired'))
      return
    }
    mutation.mutate({
      customerId: customerId.trim(),
      customerName: customerName.trim(),
      customerEmail: customerEmail.trim(),
      lineItems: validLines.map((l) => ({
        productId: l.productId,
        productName: l.productName,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
      })),
      shippingAddress: address,
      orderSource,
      salesRepId: salesRepId.trim() || undefined,
      couponCode: couponCode.trim() || undefined,
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
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.form.customerId')}</label>
              <input className="input" value={customerId} onChange={(e) => setCustomerId(e.target.value)} placeholder={t('orders.form.customerIdPlaceholder')} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.form.customerName')}</label>
              <input className="input" value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.form.customerEmail')}</label>
              <input className="input" type="email" value={customerEmail} onChange={(e) => setCustomerEmail(e.target.value)} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.orderSource')}</label>
              <select className="input" value={orderSource} onChange={(e) => setOrderSource(e.target.value as OrderSource)}>
                <option value="Manual">Manual</option>
                <option value="Ecommerce">Ecommerce</option>
                <option value="POS">POS</option>
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.salesRep')}</label>
              <input className="input" value={salesRepId} onChange={(e) => setSalesRepId(e.target.value)} placeholder={t('orders.form.salesRepPlaceholder')} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('orders.form.couponCode')}</label>
              <input className="input" value={couponCode} onChange={(e) => setCouponCode(e.target.value)} />
            </div>
          </div>
        </div>

        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.form.shippingAddress')}</h2>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-3">
            <input className="input" placeholder={t('orders.form.street')} value={address.street} onChange={(e) => setAddress({ ...address, street: e.target.value })} />
            <input className="input" placeholder={t('orders.form.ward')} value={address.ward} onChange={(e) => setAddress({ ...address, ward: e.target.value })} />
            <input className="input" placeholder={t('orders.form.city')} value={address.city} onChange={(e) => setAddress({ ...address, city: e.target.value })} />
            <input className="input" placeholder={t('orders.form.state')} value={address.state} onChange={(e) => setAddress({ ...address, state: e.target.value })} />
            <input className="input" placeholder={t('orders.form.postalCode')} value={address.postalCode} onChange={(e) => setAddress({ ...address, postalCode: e.target.value })} />
            <input className="input" placeholder={t('orders.form.country')} value={address.country} onChange={(e) => setAddress({ ...address, country: e.target.value })} />
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
                    {line.productId ? (
                      <div className="flex items-center gap-2">
                        <span>{line.productName}</span>
                        <button
                          type="button"
                          onClick={() => openPickerForRow(line.key)}
                          className="rounded border border-gray-300 px-2 py-0.5 text-xs text-gray-500 hover:bg-gray-50"
                        >
                          …
                        </button>
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() => openPickerForRow(line.key)}
                        className="input w-full text-left text-gray-400"
                      >
                        …
                      </button>
                    )}
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
                  <td className="py-2 text-right font-medium">{(line.quantity * line.unitPrice).toFixed(2)}</td>
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
            <tfoot>
              <tr className="border-t border-gray-200">
                <td colSpan={3} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
                <td className="pt-3 text-right font-semibold text-gray-900">{total.toFixed(2)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
        </div>

        <ProductPickerModal
          open={pickerOpen}
          multiple={false}
          onConfirm={handleProductPicked}
          onClose={() => setPickerOpen(false)}
        />

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
