import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useNavigate, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { transferOrdersApi } from '@/api/transferOrders.api'
import { locationsApi } from '@/api/locations.api'
import { productsApi } from '@/api/products.api'
import { Alert, Button, FormField, Input, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface DraftLine {
  key: number
  productId: string
  quantity: number
}

let nextKey = 1

export function TransferOrderFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const toast = useToast()
  const [fromLocationId, setFromLocationId] = useState('')
  const [toLocationId, setToLocationId] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([{ key: nextKey++, productId: '', quantity: 1 }])
  const [errors, setErrors] = useState<{ from?: string; to?: string; lines?: string }>({})

  const { data: locations } = useQuery({ queryKey: ['locations'], queryFn: () => locationsApi.list(true), staleTime: 60_000 })
  const { data: productsPage } = useQuery({
    queryKey: ['products', 'picker'],
    queryFn: () => productsApi.list({ skip: 0, take: 100 }),
    staleTime: 60_000,
  })
  const products = productsPage?.products ?? []

  const mutation = useMutation({
    mutationFn: transferOrdersApi.create,
    onSuccess: () => {
      toast.success(t('common.saved'))
      navigate('/inventory/transfers')
    },
  })

  const addLine = () => setLines((prev) => [...prev, { key: nextKey++, productId: '', quantity: 1 }])
  const removeLine = (key: number) => setLines((prev) => prev.filter((l) => l.key !== key))
  const updateLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)))

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    const next: { from?: string; to?: string; lines?: string } = {}
    if (!fromLocationId) next.from = t('common.required')
    if (!toLocationId) next.to = t('common.required')
    if (fromLocationId && toLocationId && fromLocationId === toLocationId) next.to = t('inventory.transfers.sameLocationError')
    const validLines = lines.filter((l) => l.productId && l.quantity > 0)
    if (validLines.length === 0) next.lines = t('orders.form.lineRequired')
    setErrors(next)
    if (next.from || next.to || next.lines) return
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
        <Link to="/inventory/transfers" className="text-sm text-ink-secondary hover:text-ink">
          {t('inventory.transfers.backToTransfers')}
        </Link>
        <h1 className="text-xl font-semibold">{t('inventory.transfers.new')}</h1>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="card p-5">
          <div className="grid grid-cols-2 gap-4">
            <FormField label={t('inventory.transfers.from')} required error={errors.from}>
              <Select value={fromLocationId} onChange={(e) => setFromLocationId(e.target.value)}>
                <option value="">{t('common.select')}</option>
                {(locations ?? []).map((l) => (
                  <option key={l.locationId} value={l.locationId}>{l.locationCode}</option>
                ))}
              </Select>
            </FormField>
            <FormField label={t('inventory.transfers.to')} required error={errors.to}>
              <Select value={toLocationId} onChange={(e) => setToLocationId(e.target.value)}>
                <option value="">{t('common.select')}</option>
                {(locations ?? []).map((l) => (
                  <option key={l.locationId} value={l.locationId}>{l.locationCode}</option>
                ))}
              </Select>
            </FormField>
            <FormField className="col-span-2" label={t('common.notes')}>
              <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
            </FormField>
          </div>
        </div>

        <div className="card p-5">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('orders.lineItems')}</h2>
            <Button variant="link" className="text-xs" onClick={addLine}>
              {t('orders.form.addLine')}
            </Button>
          </div>
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
                <th className="pb-2">{t('orders.product')}</th>
                <th className="pb-2 text-right">{t('orders.qty')}</th>
                <th className="pb-2"><span className="sr-only">{t('common.actions')}</span></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {lines.map((line) => (
                <tr key={line.key}>
                  <td className="py-2 pr-2">
                    <Select aria-label={t('orders.product')} value={line.productId} onChange={(e) => updateLine(line.key, { productId: e.target.value })}>
                      <option value="">{t('common.select')}</option>
                      {products.map((p) => (
                        <option key={p.id} value={p.id}>{p.name}</option>
                      ))}
                    </Select>
                  </td>
                  <td className="py-2 text-right">
                    <Input
                      type="number"
                      min={0.000001}
                      step="any"
                      className="ml-auto w-24 text-right"
                      aria-label={t('orders.qty')}
                      value={line.quantity}
                      onChange={(e) => updateLine(line.key, { quantity: Number(e.target.value) })}
                    />
                  </td>
                  <td className="py-2 text-right">
                    {lines.length > 1 && (
                      <Button variant="link-danger" className="text-xs" onClick={() => removeLine(line.key)}>
                        {t('orders.form.removeLine')}
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {errors.lines && <p role="alert" className="mt-2 text-xs text-danger-soft-text">{errors.lines}</p>}
        </div>

        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}

        <div className="flex justify-end gap-3">
          <Link to="/inventory/transfers" className="btn-outline">
            {t('common.cancel')}
          </Link>
          <Button type="submit" loading={mutation.isPending}>
            {t('common.save')}
          </Button>
        </div>
      </form>
    </div>
  )
}
