import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { flashSaleApi } from '@/api/promotion/promotion.api'
import type { FlashSale, FlashSaleInput } from '@/api/promotion/promotion.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Alert, Button, EmptyState, FormField, Input, Modal, useToast } from '@/components/ui'
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const patch = (i: number, p: Partial<Row>) => setRows((all) => all.map((r, idx) => (idx === i ? { ...r, ...p } : r)))

  return (
    <Modal
      open
      size="lg"
      title={t('promotion.flashSales.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={!valid} loading={mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <FormField className="col-span-2" label={t('common.name')} required>
            <Input value={name} onChange={(e) => setName(e.target.value)} />
          </FormField>
          <FormField className="col-span-2" label={t('promotion.promotions.description')}>
            <Input value={description} onChange={(e) => setDescription(e.target.value)} />
          </FormField>
          <FormField label={t('promotion.flashSales.start')} required>
            <Input type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} />
          </FormField>
          <FormField label={t('promotion.flashSales.end')} required>
            <Input type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} />
          </FormField>
        </div>

        <div>
          <div className="mb-2 flex items-center justify-between">
            <span className="text-sm font-medium text-ink-secondary">{t('promotion.flashSales.items')}</span>
            <Button variant="outline" onClick={() => setPicking(true)}>{t('promotion.flashSales.addProducts')}</Button>
          </div>
          {rows.length === 0 ? (
            <p className="rounded border border-dashed border-line-strong p-4 text-center text-sm text-ink-secondary">{t('promotion.flashSales.noItems')}</p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs uppercase text-ink-secondary">
                  <th className="py-1">{t('promotion.flashSales.product')}</th>
                  <th>{t('promotion.flashSales.originalPrice')}</th>
                  <th>{t('promotion.flashSales.salePrice')}</th>
                  <th>{t('promotion.flashSales.allocated')}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={r.productId} className="border-t border-line">
                    <td className="py-1 pr-2">{r.productName}</td>
                    <td className="pr-2"><Input type="number" min={0} className="w-28" aria-label={`${r.productName} – ${t('promotion.flashSales.originalPrice')}`} value={r.originalPrice} onChange={(e) => patch(i, { originalPrice: Number(e.target.value) })} /></td>
                    <td className="pr-2"><Input type="number" min={0} className="w-28" aria-label={`${r.productName} – ${t('promotion.flashSales.salePrice')}`} value={r.salePrice} onChange={(e) => patch(i, { salePrice: Number(e.target.value) })} /></td>
                    <td className="pr-2"><Input type="number" min={1} className="w-24" aria-label={`${r.productName} – ${t('promotion.flashSales.allocated')}`} value={r.allocatedQuantity} onChange={(e) => patch(i, { allocatedQuantity: Number(e.target.value) })} /></td>
                    <td><Button variant="link-danger" className="text-xs" onClick={() => setRows((all) => all.filter((_, idx) => idx !== i))}>{t('common.delete')}</Button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p className="mt-1 text-xs text-ink-secondary">{t('promotion.flashSales.priceRule')}</p>
        </div>

        {mutation.isError && <Alert variant="danger">{errorText(mutation.error, t('promotion.flashSales.failedToSave'))}</Alert>}
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
    </Modal>
  )
}

export function FlashSalesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [creating, setCreating] = useState(false)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [target, setTarget] = useState<{ sale: FlashSale; action: 'start' | 'cancel' } | null>(null)

  const canManage = hasPermission('promotions:manage')
  const { data, isLoading } = useQuery({ queryKey: KEY, queryFn: ({ signal }) => flashSaleApi.list(signal), staleTime: 15_000 })

  const move = useMutation({
    mutationFn: ({ sale, action }: { sale: FlashSale; action: 'start' | 'cancel' }) => flashSaleApi[action](sale.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: KEY })
      toast.success(t('common.saved'))
      setTarget(null)
    },
    onError: (e) => {
      toast.error(errorText(e, t('common.failedToSave')))
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
            <Button variant="link" className="text-xs" aria-expanded={expanded === s.id} onClick={() => setExpanded(expanded === s.id ? null : s.id)}>
              {expanded === s.id ? t('promotion.flashSales.hideItems') : t('promotion.flashSales.showItems')}
            </Button>
            {canManage && s.status === 'Scheduled' && (
              <Button variant="link" className="text-xs" onClick={() => setTarget({ sale: s, action: 'start' })}>{t('promotion.flashSales.startNow')}</Button>
            )}
            {canManage && (s.status === 'Scheduled' || s.status === 'Active') && (
              <Button variant="link-danger" className="text-xs" onClick={() => setTarget({ sale: s, action: 'cancel' })}>{t('common.cancel')}</Button>
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
        {canManage && <Button onClick={() => setCreating(true)}>{t('promotion.flashSales.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('promotion.flashSales.empty')}
            description={t('promotion.flashSales.emptyHint')}
            action={canManage ? <Button onClick={() => setCreating(true)}>{t('promotion.flashSales.add')}</Button> : undefined}
          />
        }
      />

      {open && (
        <div className="card p-4">
          <h2 className="mb-2 text-sm font-semibold">{open.name}</h2>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs uppercase text-ink-secondary">
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
                <tr key={i.id} className="border-t border-line">
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
