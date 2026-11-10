import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Order, type Plant } from '@/api/manufacturing/mfg.api'
import { warehousesApi } from '@/api/warehouses.api'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { Alert, Button, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { todayIso } from './shared'

interface Form { quantity: string; dueDate: string; priority: string; bomId: string; routingId: string; outputWarehouseId: string; notes: string }

interface OrderFormModalProps {
  plant: Plant
  /** Null = a new order; an order = change that Draft. */
  order: Order | null
  onClose: () => void
  onSaved: (order: Order) => void
}

/** New order / change a Draft (BR-PO-03..05). The server's warnings come back with the order and are shown by the caller's page. */
export function OrderFormModal({ plant, order, onClose, onSaved }: OrderFormModalProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const [item, setItem] = useState<{ id: string; label: string } | null>(order ? { id: order.itemId, label: `${order.itemCode} ${order.itemName}` } : null)
  const [picker, setPicker] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<Form>({
    defaultValues: {
      quantity: order ? String(order.quantity) : '', dueDate: order?.dueDate ?? todayIso(), priority: String(order?.priority ?? 3), bomId: order?.bomId ?? '',
      routingId: order?.routingId ?? '', outputWarehouseId: order?.outputWarehouseId ?? '', notes: order?.notes ?? '',
    },
  })
  const { data: boms = [] } = useQuery({ queryKey: ['mfg', 'boms', plant.id, item?.id, 'Approved'], queryFn: () => mfgApi.listBoms(plant.id, item!.id, 'Approved'), enabled: !!item })
  const { data: routings = [] } = useQuery({ queryKey: ['mfg', 'routings', plant.id, item?.id, 'Approved'], queryFn: () => mfgApi.listRoutings(plant.id, item!.id, 'Approved'), enabled: !!item })
  const { data: warehouses = [] } = useQuery({ queryKey: ['warehouses'], queryFn: () => warehousesApi.list() })
  const plantWarehouses = [plant.finishedGoodsWarehouseId, plant.rawMaterialWarehouseId, plant.quarantineWarehouseId, plant.scrapWarehouseId].filter((x): x is string => !!x)

  const save = useMutation({
    mutationFn: (f: Form) => {
      const body = {
        quantity: Number(f.quantity), dueDate: f.dueDate, priority: f.priority ? Number(f.priority) : null, bomId: f.bomId || null, routingId: f.routingId || null,
        outputWarehouseId: f.outputWarehouseId || null, notes: f.notes.trim() || null,
      }
      return order ? mfgApi.updateOrder(order.id, body) : mfgApi.createOrder({ plantId: plant.id, itemId: item!.id, ...body })
    },
    onSuccess: (saved) => { toast.success(t('common.saved')); onSaved(saved) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  const submit = handleSubmit((f) => {
    if (!item) { setError(t('mfg.order.form.itemRequired')); return }
    setError(null)
    save.mutate(f)
  })

  return (
    <>
      <Modal
        open
        size="lg"
        title={order ? t('mfg.order.form.editTitle', { no: order.orderNo }) : t('mfg.order.form.newTitle')}
        onClose={onClose}
        closeOnEsc={!save.isPending}
        footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="order-form" loading={save.isPending}>{t('common.save')}</Button></>}
      >
        <form id="order-form" onSubmit={submit} className="space-y-4" noValidate>
          {error && <Alert variant="danger">{error}</Alert>}
          <FormField label={t('mfg.order.item')} required>
            <div className="flex items-center gap-3">
              <span className="min-w-0 flex-1 truncate text-sm text-ink">{item?.label ?? t('mfg.order.form.noItem')}</span>
              {!order && <Button type="button" variant="outline" size="sm" onClick={() => setPicker(true)}>{t('mfg.order.form.pickItem')}</Button>}
            </div>
          </FormField>
          <div className="grid gap-4 sm:grid-cols-3">
            <FormField label={t('mfg.order.quantity')} required error={errors.quantity?.message}>
              <Input type="number" step="any" min="0" {...register('quantity', { required: t('mfg.order.form.quantityRequired'), validate: (v) => Number(v) > 0 || t('mfg.order.form.quantityRequired') })} />
            </FormField>
            <FormField label={t('mfg.order.dueDate')} required error={errors.dueDate?.message}>
              <Input type="date" min={todayIso()} {...register('dueDate', { required: t('mfg.order.form.dueRequired') })} />
            </FormField>
            <FormField label={t('mfg.order.priority')} hint={t('mfg.order.form.priorityHint')}>
              <Select {...register('priority')}>{[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{n}</option>)}</Select>
            </FormField>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label={t('mfg.order.bom')} hint={t('mfg.order.form.versionHint')}>
              <Select {...register('bomId')}><option value="">{t('mfg.order.form.byDate')}</option>{boms.map((b) => <option key={b.id} value={b.id}>v{b.version} ({b.effectiveFrom} → {b.effectiveTo ?? '…'})</option>)}</Select>
            </FormField>
            <FormField label={t('mfg.order.routing')} hint={t('mfg.order.form.versionHint')}>
              <Select {...register('routingId')}><option value="">{t('mfg.order.form.byDate')}</option>{routings.map((r) => <option key={r.id} value={r.id}>v{r.version} ({r.effectiveFrom} → {r.effectiveTo ?? '…'})</option>)}</Select>
            </FormField>
          </div>
          <FormField label={t('mfg.order.outputWarehouse')} hint={t('mfg.order.form.warehouseHint')}>
            <Select {...register('outputWarehouseId')}>
              <option value="">{t('mfg.order.form.plantDefault')}</option>
              {plantWarehouses.map((id) => <option key={id} value={id}>{warehouses.find((w) => w.id === id)?.name ?? id}</option>)}
            </Select>
          </FormField>
          <FormField label={t('mfg.order.notes')}><Textarea rows={2} maxLength={500} {...register('notes')} /></FormField>
        </form>
      </Modal>
      <ProductPickerModal open={picker} multiple={false} onClose={() => setPicker(false)} onConfirm={(p) => { setPicker(false); if (p[0]) setItem({ id: p[0].id, label: `${p[0].sku} ${p[0].name}` }) }} />
    </>
  )
}
