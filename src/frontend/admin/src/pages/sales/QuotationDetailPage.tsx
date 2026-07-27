import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import axios from 'axios'
import { quotationsApi } from '@/api/quotations.api'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

export function QuotationDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [actionError, setActionError] = useState<string | null>(null)
  const [address, setAddress] = useState({ street: '', city: '', state: '', postalCode: '', country: 'US' })

  const { data: quotation, isLoading } = useQuery({
    queryKey: ['quotations', id],
    queryFn: () => quotationsApi.get(id!),
    enabled: Boolean(id),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['quotations', id] })

  const runAction = (fn: () => Promise<unknown>, onDone?: (result: unknown) => void) => {
    setActionError(null)
    fn()
      .then((result) => {
        invalidate()
        onDone?.(result)
      })
      .catch((err) => {
        const message = axios.isAxiosError(err) ? (err.response?.data as { message?: string })?.message : undefined
        setActionError(message ?? t('common.failedToSave'))
      })
  }

  const sendMutation = useMutation({ mutationFn: () => quotationsApi.send(id!) })
  const acceptMutation = useMutation({ mutationFn: () => quotationsApi.accept(id!) })
  const rejectMutation = useMutation({ mutationFn: () => quotationsApi.reject(id!) })
  const convertMutation = useMutation({ mutationFn: () => quotationsApi.convertToOrder(id!, address) })

  if (isLoading) return <div className="text-gray-400">{t('common.loading')}</div>
  if (!quotation) return <div className="text-gray-400">{t('quotations.notFound')}</div>

  const fmt = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
  const addressComplete = address.street.trim() && address.city.trim() && address.state.trim() && address.postalCode.trim()

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/sales/quotations" className="text-sm text-gray-500 hover:text-gray-700">
          {t('quotations.backToQuotations')}
        </Link>
        <h1 className="text-xl font-semibold">{quotation.quotationNo}</h1>
        <StatusBadge status={quotation.status} />
      </div>

      {actionError && <p className="text-sm text-red-600">{actionError}</p>}

      <div className="grid grid-cols-2 gap-6">
        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.orderInfo')}</h2>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('orders.customer')}</dt>
              <dd className="font-medium">{quotation.customerName}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('quotations.validUntil')}</dt>
              <dd>{format(new Date(quotation.validUntil), 'dd MMM yyyy')}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('orders.total')}</dt>
              <dd className="font-semibold text-gray-900">{fmt(quotation.totalAmount)}</dd>
            </div>
            {quotation.convertedOrderId && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('quotations.convertedOrder')}</dt>
                <dd>
                  <Link to={`/orders/${quotation.convertedOrderId}`} className="text-primary-600 hover:underline">
                    {t('quotations.viewOrder')}
                  </Link>
                </dd>
              </div>
            )}
          </dl>
        </div>

        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.statusActions.title')}</h2>
          <div className="flex flex-wrap gap-2">
            {hasPermission('sales_order:update') && quotation.status === 'Draft' && (
              <button onClick={() => runAction(() => sendMutation.mutateAsync())} className="btn-primary text-sm">
                {t('quotations.send')}
              </button>
            )}
            {hasPermission('sales_order:update') && quotation.status === 'Sent' && (
              <>
                <button onClick={() => runAction(() => acceptMutation.mutateAsync())} className="btn-primary text-sm">
                  {t('quotations.accept')}
                </button>
                <button onClick={() => runAction(() => rejectMutation.mutateAsync())} className="btn-outline text-sm text-red-600">
                  {t('quotations.reject')}
                </button>
              </>
            )}
            {hasPermission('sales_order:create') && quotation.status === 'Accepted' && (
              <div className="w-full space-y-2">
                <p className="text-xs text-gray-500">{t('quotations.convertNeedsAddress')}</p>
                <div className="grid grid-cols-2 gap-2 md:grid-cols-3">
                  <input className="input text-sm" placeholder={t('orders.form.street')} value={address.street} onChange={(e) => setAddress({ ...address, street: e.target.value })} />
                  <input className="input text-sm" placeholder={t('orders.form.city')} value={address.city} onChange={(e) => setAddress({ ...address, city: e.target.value })} />
                  <input className="input text-sm" placeholder={t('orders.form.state')} value={address.state} onChange={(e) => setAddress({ ...address, state: e.target.value })} />
                  <input className="input text-sm" placeholder={t('orders.form.postalCode')} value={address.postalCode} onChange={(e) => setAddress({ ...address, postalCode: e.target.value })} />
                  <input className="input text-sm" placeholder={t('orders.form.country')} value={address.country} onChange={(e) => setAddress({ ...address, country: e.target.value })} />
                </div>
                <button
                  disabled={!addressComplete}
                  onClick={() => runAction(() => convertMutation.mutateAsync(), (order) => navigate(`/orders/${(order as { orderId: string }).orderId}`))}
                  className="btn-primary text-sm"
                >
                  {t('quotations.convertToOrder')}
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="card p-5">
        <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.lineItems')}</h2>
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
              <th className="pb-2">{t('orders.product')}</th>
              <th className="pb-2 text-right">{t('orders.qty')}</th>
              <th className="pb-2 text-right">{t('orders.unitPrice')}</th>
              <th className="pb-2 text-right">{t('orders.total')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {quotation.lineItems.map((line) => (
              <tr key={line.productId}>
                <td className="py-2">{line.productName}</td>
                <td className="py-2 text-right">{line.quantity}</td>
                <td className="py-2 text-right">{fmt(line.unitPrice)}</td>
                <td className="py-2 text-right font-medium">{fmt(line.quantity * line.unitPrice)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-gray-200">
              <td colSpan={3} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
              <td className="pt-3 text-right font-semibold text-gray-900">{fmt(quotation.totalAmount)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  )
}
