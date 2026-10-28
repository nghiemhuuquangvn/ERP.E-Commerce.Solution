import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { flashSaleApi } from '@/api/promotion/promotion.api'
import type { FlashSale, FlashSaleInput } from '@/api/promotion/promotion.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

const KEY = ['promotion', 'flashSales']
const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`
const toLocalInput = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
const errorText = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { detail?: string; message?: string } } })?.response?.data?.detail ??
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback

interface Row { productId: string; productName: string; originalPrice: number; salePrice: number; allocatedQuantity: number }

function FlashSaleModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [start, setStart] = useState(toLocalInput(new Date(Date.now() + 3600_000)))
  const [end, setEnd] = useState(toLocalInput(new Date(Date.now() + 86_400_000)))
  const [rows, setRows] = useState<Row[]>([])
  const [picking, setPicking] = useState(false)

  const valid = name.trim() && start && end && new Date(end) > new Date(start) && rows.length > 0 &&
    rows.every((r) => r.salePrice > 0 && r.salePrice < r.originalPrice && r.allocatedQuantity > 0)

  const mutation = useMutation({
    mutationFn: () => {
      const body: FlashSaleInput = {
        name: name.trim(),
        description: description.trim() || null,
        startTime: new Date(start).toISOString(),
        endTime: new Date(end).toISOString(),
        items: rows,
      }
      return flashSaleApi.create(body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      onClose()
    },
  })

  const patch = (i: number, p: Partial<Row>) => setRows((all) => all.map((r, idx) => (idx === i ? { ...r, ...p } : r)))
  const label = 'mb-1 block text-sm font-medium text-gray-700'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-3xl space-y-4 p-6">
        <h3 className="text-base font-semibold text-gray-900">{t('promotion.flashSales.addTitle')}</h3>
        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <label className={label}>{t('common.name')}</label>
            <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="col-span-2">
            <label className={label}>{t('promotion.promotions.description')}</label>
            <input className="input" value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div>
            <label className={label}>{t('promotion.flashSales.start')}</label>
            <input type="datetime-local" className="input" value={start} onChange={(e) => setStart(e.target.value)} />
          </div>
          <div>
            <label className={label}>{t('promotion.flashSales.end')}</label>
            <input type="datetime-local" className="input" value={end} onChange={(e) => setEnd(e.target.value)} />
          </div>
        </div>

        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700">{t('promotion.flashSales.items')}</span>
            <button type="button" className="btn-outline" onClick={() => setPicking(true)}>{t('promotion.flashSales.addProducts')}</button>
          </div>
          {rows.length === 0 ? (
            <p className="rounded border border-dashed p-4 text-center text-sm text-gray-400">{t('promotion.flashSales.noItems')}</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs uppercase text-gray-500">
                  <th className="py-1">{t('promotion.flashSales.product')}</th>
                  <th>{t('promotion.flashSales.originalPrice')}</th>
                  <th>{t('promotion.flashSales.salePrice')}</th>
                  <th>{t('promotion.flashSales.allocated')}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={r.productId} className="border-t">
                    <td className="py-1 pr-2">{r.productName}</td>
                    <td className="pr-2"><input type="number" min={0} className="input w-28" value={r.originalPrice} onChange={(e) => patch(i, { originalPrice: Number(e.target.value) })} /></td>
                    <td className="pr-2"><input type="number" min={0} className="input w-28" value={r.salePrice} onChange={(e) => patch(i, { salePrice: Number(e.target.value) })} /></td>
                    <td className="pr-2"><input type="number" min={1} className="input w-24" value={r.allocatedQuantity} onChange={(e) => patch(i, { allocatedQuantity: Number(e.target.value) })} /></td>
                    <td><button type="button" className="text-xs text-red-600 hover:underline" onClick={() => setRows((all) => all.filter((_, idx) => idx !== i))}>{t('common.delete')}</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p className="mt-1 text-xs text-gray-400">{t('promotion.flashSales.priceRule')}</p>
        </div>

        {mutation.isError && <p className="text-xs text-red-600">{errorText(mutation.error, t('promotion.flashSales.failedToSave'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!valid || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </div>

      <ProductPickerModal
        open={picking}
        onClose={() => setPicking(false)}
        onConfirm={(products) => {
          setRows((all) => [
            ...all,
            ...products.filter((p) => !all.some((r) => r.productId === p.id)).map((p) => ({
              productId: p.id, productName: p.name, originalPrice: p.price, salePrice: Math.max(1, Math.round(p.price * 0.9)), allocatedQuantity: 10,
            })),
          ])
          setPicking(false)
        }}
      />
    </div>
  )
}

export function FlashSalesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [creating, setCreating] = useState(false)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [target, setTarget] = useState<{ sale: FlashSale; action: 'start' | 'cancel' } | null>(null)

  const canManage = hasPermission('promotions:manage')
  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: ({ signal }) => flashSaleApi.list(signal), staleTime: 15_000 })

  const move = useMutation({
    mutationFn: ({ sale, action }: { sale: FlashSale; action: 'start' | 'cancel' }) => flashSaleApi[action](sale.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      setTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      { key: 'period', label: t('promotion.promotions.period'), render: (s: FlashSale) => `${new Date(s.startTime).toLocaleString()} – ${new Date(s.endTime).toLocaleString()}` },
      {
        key: 'items',
        label: t('promotion.flashSales.sold'),
        render: (s: FlashSale) => {
          const sold = s.items.reduce((n, i) => n + i.soldQuantity, 0)
          const all = s.items.reduce((n, i) => n + i.allocatedQuantity, 0)
          return `${sold} / ${all}`
        },
      },
      { key: 'status', label: t('common.status'), render: (s: FlashSale) => <StatusBadge status={s.status} label={t(`promotion.flashSaleStatus.${s.status}`)} /> },
      {
        key: 'actions',
        label: '',
        render: (s: FlashSale) => (
          <div className="flex gap-3">
            <button className="text-xs text-primary-600 hover:underline" onClick={() => setExpanded(expanded === s.id ? null : s.id)}>
              {expanded === s.id ? t('promotion.flashSales.hideItems') : t('promotion.flashSales.showItems')}
            </button>
            {canManage && s.status === 'Scheduled' && (
              <button className="text-xs text-green-700 hover:underline" onClick={() => setTarget({ sale: s, action: 'start' })}>{t('promotion.flashSales.startNow')}</button>
            )}
            {canManage && (s.status === 'Scheduled' || s.status === 'Active') && (
              <button className="text-xs text-red-600 hover:underline" onClick={() => setTarget({ sale: s, action: 'cancel' })}>{t('common.cancel')}</button>
            )}
          </div>
        ),
      },
    ],
    [t, canManage, expanded],
  )

  const open = data?.find((s) => s.id === expanded)

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('promotion.flashSales.title')}</h1>
        {canManage && <button className="btn-primary" onClick={() => setCreating(true)}>{t('promotion.flashSales.add')}</button>}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {open && (
        <div className="card p-4">
          <h2 className="mb-2 text-sm font-semibold">{open.name}</h2>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs uppercase text-gray-500">
                <th className="py-1">{t('promotion.flashSales.product')}</th>
                <th>{t('promotion.flashSales.originalPrice')}</th>
                <th>{t('promotion.flashSales.salePrice')}</th>
                <th>{t('promotion.flashSales.discount')}</th>
                <th>{t('promotion.flashSales.sold')}</th>
                <th>{t('promotion.flashSales.remaining')}</th>
              </tr>
            </thead>
            <tbody>
              {open.items.map((i) => (
                <tr key={i.id} className="border-t">
                  <td className="py-1">{i.productName}</td>
                  <td>{money(i.originalPrice)}</td>
                  <td>{money(i.salePrice)}</td>
                  <td>{Math.round(i.discountPercentage)}%</td>
                  <td>{i.soldQuantity} / {i.allocatedQuantity}</td>
                  <td>{i.remainingQuantity}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {creating && <FlashSaleModal onClose={() => setCreating(false)} />}

      <ConfirmDialog
        open={!!target}
        title={target ? t(`promotion.flashSales.${target.action}Title`) : ''}
        message={target ? t(`promotion.flashSales.${target.action}Confirm`, { name: target.sale.name }) : ''}
        danger={target?.action === 'cancel'}
        onConfirm={() => target && move.mutate(target)}
        onCancel={() => setTarget(null)}
      />
    </div>
  )
}
