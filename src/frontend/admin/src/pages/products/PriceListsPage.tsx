import { useMemo, useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  pricingApi,
  PriceListChannel,
  PRICE_LIST_CHANNEL_OPTIONS,
  type PriceListDetail,
} from '@/api/product/pricing.api'
import { currenciesApi } from '@/api/masterdata/currencies.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

type Tab = 'lists' | 'items'

interface ItemFormValues {
  variantId: string
  basePrice: string
  discountedPrice: string
  minQuantity: string
}

interface CreateFormValues {
  name: string
  description: string
  effectiveDate: string
  expiryDate: string
  channel: PriceListChannel
  currencyId: string
  items: ItemFormValues[]
}

const emptyItem: ItemFormValues = { variantId: '', basePrice: '', discountedPrice: '', minQuantity: '1' }

function CreatePriceListModal({
  currencies,
  onClose,
  onCreated,
}: {
  currencies: { id: string; code: string; name: string }[]
  onClose: () => void
  onCreated: (id: string, name: string) => void
}) {
  const { t } = useTranslation()
  const toast = useToast()

  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateFormValues>({
    defaultValues: {
      name: '',
      description: '',
      effectiveDate: '',
      expiryDate: '',
      channel: PriceListChannel.Retail,
      currencyId: '',
      items: [emptyItem],
    },
  })

  const { fields, append, remove } = useFieldArray({ control, name: 'items' })

  const mutation = useMutation({
    mutationFn: (values: CreateFormValues) =>
      pricingApi.create({
        name: values.name,
        description: values.description.trim() || undefined,
        effectiveDate: values.effectiveDate,
        expiryDate: values.expiryDate.trim() || undefined,
        channel: Number(values.channel) as PriceListChannel,
        currencyId: values.currencyId.trim() || undefined,
        items: values.items.map((i) => ({
          variantId: i.variantId,
          basePrice: parseFloat(i.basePrice) || 0,
          discountedPrice: i.discountedPrice.trim() ? parseFloat(i.discountedPrice) : undefined,
          minQuantity: parseInt(i.minQuantity, 10) || 1,
        })),
      }),
    onSuccess: (result) => {
      onCreated(result.priceListId, result.name)
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="lg"
      title={t('products.priceLists.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="price-list-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="price-list-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
            <Input {...register('name', { required: true })} />
          </FormField>
          <FormField label={t('products.priceLists.channel')}>
            <Select {...register('channel', { valueAsNumber: true })}>
              {PRICE_LIST_CHANNEL_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </Select>
          </FormField>
        </div>
        <FormField label={t('products.priceLists.description')}>
          <Textarea rows={2} {...register('description')} />
        </FormField>
        <div className="grid grid-cols-3 gap-4">
          <FormField label={t('products.priceLists.effectiveDate')} required error={errors.effectiveDate ? t('common.required') : undefined}>
            <Input type="date" {...register('effectiveDate', { required: true })} />
          </FormField>
          <FormField label={t('products.priceLists.expiryDate')}>
            <Input type="date" {...register('expiryDate')} />
          </FormField>
          <FormField label={t('products.priceLists.currency')}>
            <Select {...register('currencyId')}>
              <option value="">{t('products.priceLists.tenantDefaultCurrency')}</option>
              {currencies.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.code} — {c.name}
                </option>
              ))}
            </Select>
          </FormField>
        </div>

        <fieldset>
          <div className="mb-2 flex items-center justify-between">
            <legend className="text-sm font-medium text-ink">{t('products.priceLists.items')}</legend>
            <Button variant="outline" size="sm" onClick={() => append(emptyItem)}>
              {t('products.priceLists.addItem')}
            </Button>
          </div>
          <p className="mb-2 text-xs text-ink-secondary">{t('products.priceLists.variantIdHint')}</p>
          <div className="space-y-2">
            {fields.map((field, index) => (
              <div key={field.id} className="grid grid-cols-12 items-start gap-2">
                <div className="col-span-4">
                  <Input
                    aria-label={t('products.priceLists.variantId')}
                    aria-invalid={errors.items?.[index]?.variantId ? true : undefined}
                    placeholder={t('products.priceLists.variantId') ?? ''}
                    {...register(`items.${index}.variantId` as const, { required: true })}
                  />
                </div>
                <div className="col-span-2">
                  <Input
                    type="number"
                    step="0.01"
                    aria-label={t('products.priceLists.basePrice')}
                    aria-invalid={errors.items?.[index]?.basePrice ? true : undefined}
                    placeholder={t('products.priceLists.basePrice') ?? ''}
                    {...register(`items.${index}.basePrice` as const, { required: true })}
                  />
                </div>
                <div className="col-span-2">
                  <Input
                    type="number"
                    step="0.01"
                    aria-label={t('products.priceLists.discountedPrice')}
                    placeholder={t('products.priceLists.discountedPrice') ?? ''}
                    {...register(`items.${index}.discountedPrice` as const)}
                  />
                </div>
                <div className="col-span-2">
                  <Input
                    type="number"
                    min={1}
                    step="1"
                    aria-label={t('products.priceLists.minQuantity')}
                    placeholder={t('products.priceLists.minQuantity') ?? ''}
                    {...register(`items.${index}.minQuantity` as const)}
                  />
                </div>
                <div className="col-span-2">
                  <Button
                    variant="link-danger"
                    className="text-xs"
                    disabled={fields.length <= 1}
                    onClick={() => remove(index)}
                  >
                    {t('common.delete')}
                  </Button>
                </div>
              </div>
            ))}
          </div>
          {(errors.items?.some?.((i) => i?.variantId || i?.basePrice)) && (
            <p role="alert" className="mt-2 text-xs text-danger-soft-text">{t('common.required')}</p>
          )}
        </fieldset>

        {mutation.isError && (
          <Alert variant="danger">{serverMessage(mutation.error, t('products.priceLists.failedToSave'))}</Alert>
        )}
      </form>
    </Modal>
  )
}

function ChannelCurrencyForm({
  priceList,
  currencies,
  canManage,
}: {
  priceList: PriceListDetail
  currencies: { id: string; code: string; name: string }[]
  canManage: boolean
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit } = useForm<{ channel: PriceListChannel; currencyId: string }>({
    defaultValues: {
      channel: priceList.channel,
      currencyId: priceList.currencyId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: { channel: PriceListChannel; currencyId: string }) =>
      pricingApi.setChannel(priceList.id, {
        channel: Number(values.channel) as PriceListChannel,
        currencyId: values.currencyId.trim() || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'priceList', priceList.id] })
      toast.success(t('common.saved'))
    },
  })

  return (
    <form
      onSubmit={handleSubmit((v) => mutation.mutate(v))}
      className="flex flex-wrap items-end gap-3 rounded-md border border-line p-3"
    >
      <FormField label={t('products.priceLists.channel')}>
        <Select disabled={!canManage} {...register('channel', { valueAsNumber: true })}>
          {PRICE_LIST_CHANNEL_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </Select>
      </FormField>
      <FormField label={t('products.priceLists.currency')}>
        <Select disabled={!canManage} {...register('currencyId')}>
          <option value="">{t('products.priceLists.tenantDefaultCurrency')}</option>
          {currencies.map((c) => (
            <option key={c.id} value={c.id}>
              {c.code} — {c.name}
            </option>
          ))}
        </Select>
      </FormField>
      {canManage && (
        <Button type="submit" loading={mutation.isPending}>
          {mutation.isPending ? t('common.saving') : t('products.priceLists.updateChannel')}
        </Button>
      )}
      {mutation.isError && <Alert variant="danger" className="w-full">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
    </form>
  )
}

export function PriceListsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('products:edit')
  const [tab, setTab] = useState<Tab>('lists')
  const [createOpen, setCreateOpen] = useState(false)

  // The backend has no "list all price lists" endpoint (PricingController only
  // exposes Create / GetById / SetChannel) — so this page tracks price lists it
  // has created or looked up, client-side, for this session only.
  const [known, setKnown] = useState<{ id: string; name: string }[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [lookupId, setLookupId] = useState('')

  const { data: currenciesPage } = useQuery({
    queryKey: ['masterdata', 'currencies', 'picker'],
    queryFn: () => currenciesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })
  const currencies = currenciesPage?.items ?? []

  const detailQuery = useQuery({
    queryKey: ['product', 'priceList', selectedId],
    queryFn: () => pricingApi.get(selectedId),
    enabled: !!selectedId,
    staleTime: 10_000,
  })

  function handleLookup() {
    const id = lookupId.trim()
    if (!id) return
    setSelectedId(id)
    if (!known.some((k) => k.id === id)) {
      setKnown((prev) => [...prev, { id, name: id }])
    }
  }

  function handleCreated(id: string, name: string) {
    setKnown((prev) => [...prev, { id, name }])
    setSelectedId(id)
    setLookupId(id)
  }

  const itemColumns = useMemo(
    () => [
      { key: 'variantId', label: t('products.priceLists.variantId'), className: 'font-mono text-xs' },
      {
        key: 'basePrice',
        label: t('products.priceLists.basePrice'),
        align: 'right' as const,
        render: (i: PriceListDetail['items'][number]) => i.basePrice.toFixed(2),
      },
      {
        key: 'discountedPrice',
        label: t('products.priceLists.discountedPrice'),
        align: 'right' as const,
        render: (i: PriceListDetail['items'][number]) =>
          i.discountedPrice != null ? i.discountedPrice.toFixed(2) : '—',
      },
      { key: 'minQuantity', label: t('products.priceLists.minQuantity'), align: 'right' as const },
      {
        key: 'effectivePrice',
        label: t('products.priceLists.effectivePrice'),
        align: 'right' as const,
        render: (i: PriceListDetail['items'][number]) => i.effectivePrice.toFixed(2),
      },
    ],
    [t],
  )

  // DataTable requires rows with an `id`; PriceListItemDto has none (VariantId +
  // MinQuantity together are the natural key), so synthesize one for rendering.
  const itemRows = (detailQuery.data?.items ?? []).map((i) => ({
    ...i,
    id: `${i.variantId}:${i.minQuantity}`,
  }))

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('products.priceLists.title')}</h1>
        {canManage && tab === 'lists' && (
          <Button onClick={() => setCreateOpen(true)}>
            {t('products.priceLists.add')}
          </Button>
        )}
      </div>

      <div className="flex gap-2 border-b border-line">
        {(
          [
            { key: 'lists' as const, label: t('products.priceLists.listsTab') },
            { key: 'items' as const, label: t('products.priceLists.itemsTab') },
          ]
        ).map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key ? 'border-b-2 border-brand-action text-brand-action' : 'text-ink-secondary hover:text-ink'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-end gap-2">
        <FormField label={t('products.priceLists.lookupId')}>
          <Input className="w-80"
            value={lookupId}
            onChange={(e) => setLookupId(e.target.value)}
            placeholder={t('products.priceLists.lookupIdPlaceholder') ?? ''}
          />
        </FormField>
        <Button variant="outline" onClick={handleLookup}>
          {t('products.priceLists.load')}
        </Button>
        {known.length > 0 && (
          <Select
            className="max-w-xs"
        aria-label={t('products.priceLists.recentlyViewed')}
            value={selectedId}
            onChange={(e) => {
              setSelectedId(e.target.value)
              setLookupId(e.target.value)
            }}
          >
            <option value="">{t('products.priceLists.recentlyViewed')}</option>
            {known.map((k) => (
              <option key={k.id} value={k.id}>
                {k.name}
              </option>
            ))}
          </Select>
        )}
      </div>

      {detailQuery.isError && (
        <Alert variant="danger">{t('products.priceLists.notFound')}</Alert>
      )}

      {detailQuery.data && tab === 'lists' && (
        <div className="card space-y-3 p-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-semibold">{detailQuery.data.name}</h2>
            <StatusBadge
              status={detailQuery.data.isActive ? 'Active' : 'Inactive'}
              label={detailQuery.data.isActive ? t('products.priceLists.active') : t('products.priceLists.inactive')}
            />
          </div>
          {detailQuery.data.description && <p className="text-sm text-ink-secondary">{detailQuery.data.description}</p>}
          <div className="grid grid-cols-2 gap-2 text-sm text-ink-secondary">
            <span>
              {t('products.priceLists.effectiveDate')}: {detailQuery.data.effectiveDate.slice(0, 10)}
            </span>
            <span>
              {t('products.priceLists.expiryDate')}: {detailQuery.data.expiryDate?.slice(0, 10) ?? '—'}
            </span>
          </div>
          <ChannelCurrencyForm priceList={detailQuery.data} currencies={currencies} canManage={canManage} />
        </div>
      )}

      {tab === 'items' && (
        <>
          {!detailQuery.data ? (
            <EmptyState title={t('products.priceLists.pickListFirst')} />
          ) : (
            <>
              <p className="text-xs text-ink-secondary">{t('products.priceLists.itemsReadOnlyNote')}</p>
              <DataTable columns={itemColumns} data={itemRows} isLoading={detailQuery.isLoading} />
            </>
          )}
        </>
      )}

      {createOpen && (
        <CreatePriceListModal
          currencies={currencies}
          onClose={() => setCreateOpen(false)}
          onCreated={handleCreated}
        />
      )}
    </div>
  )
}
