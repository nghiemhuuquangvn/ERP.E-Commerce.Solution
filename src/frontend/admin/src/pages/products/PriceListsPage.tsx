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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-3xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('products.priceLists.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
              <input className="input" {...register('name', { required: true })} />
              {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('products.priceLists.channel')}
              </label>
              <select className="input" {...register('channel', { valueAsNumber: true })}>
                {PRICE_LIST_CHANNEL_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('products.priceLists.description')}
            </label>
            <textarea className="input" rows={2} {...register('description')} />
          </div>
          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('products.priceLists.effectiveDate')}
              </label>
              <input type="date" className="input" {...register('effectiveDate', { required: true })} />
              {errors.effectiveDate && (
                <p className="mt-1 text-xs text-red-600">{t('products.priceLists.effectiveDate')} is required.</p>
              )}
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('products.priceLists.expiryDate')}
              </label>
              <input type="date" className="input" {...register('expiryDate')} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('products.priceLists.currency')}
              </label>
              <select className="input" {...register('currencyId')}>
                <option value="">{t('products.priceLists.tenantDefaultCurrency')}</option>
                {currencies.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.code} — {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <div className="mb-2 flex items-center justify-between">
              <label className="block text-sm font-medium text-gray-700">{t('products.priceLists.items')}</label>
              <button type="button" className="btn-outline text-xs" onClick={() => append(emptyItem)}>
                {t('products.priceLists.addItem')}
              </button>
            </div>
            <p className="mb-2 text-xs text-gray-400">{t('products.priceLists.variantIdHint')}</p>
            <div className="space-y-2">
              {fields.map((field, index) => (
                <div key={field.id} className="grid grid-cols-12 items-start gap-2">
                  <div className="col-span-4">
                    <input
                      className="input"
                      placeholder={t('products.priceLists.variantId') ?? ''}
                      {...register(`items.${index}.variantId` as const, { required: true })}
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="0.01"
                      className="input"
                      placeholder={t('products.priceLists.basePrice') ?? ''}
                      {...register(`items.${index}.basePrice` as const, { required: true })}
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      step="0.01"
                      className="input"
                      placeholder={t('products.priceLists.discountedPrice') ?? ''}
                      {...register(`items.${index}.discountedPrice` as const)}
                    />
                  </div>
                  <div className="col-span-2">
                    <input
                      type="number"
                      min={1}
                      step="1"
                      className="input"
                      placeholder={t('products.priceLists.minQuantity') ?? ''}
                      {...register(`items.${index}.minQuantity` as const)}
                    />
                  </div>
                  <div className="col-span-2">
                    <button
                      type="button"
                      className="text-xs text-red-600 hover:underline disabled:opacity-30"
                      disabled={fields.length <= 1}
                      onClick={() => remove(index)}
                    >
                      {t('common.delete')}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {mutation.isError && (
            <p className="text-xs text-red-600">{t('products.priceLists.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
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
    },
  })

  return (
    <form
      onSubmit={handleSubmit((v) => mutation.mutate(v))}
      className="flex flex-wrap items-end gap-3 rounded-md border border-gray-200 p-3"
    >
      <div>
        <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.priceLists.channel')}</label>
        <select className="input" disabled={!canManage} {...register('channel', { valueAsNumber: true })}>
          {PRICE_LIST_CHANNEL_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>
      <div>
        <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.priceLists.currency')}</label>
        <select className="input" disabled={!canManage} {...register('currencyId')}>
          <option value="">{t('products.priceLists.tenantDefaultCurrency')}</option>
          {currencies.map((c) => (
            <option key={c.id} value={c.id}>
              {c.code} — {c.name}
            </option>
          ))}
        </select>
      </div>
      {canManage && (
        <button type="submit" disabled={mutation.isPending} className="btn-primary">
          {mutation.isPending ? t('common.saving') : t('products.priceLists.updateChannel')}
        </button>
      )}
      {mutation.isSuccess && <span className="text-xs text-green-600">{t('common.save')} ✓</span>}
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
        render: (i: PriceListDetail['items'][number]) => i.basePrice.toFixed(2),
      },
      {
        key: 'discountedPrice',
        label: t('products.priceLists.discountedPrice'),
        render: (i: PriceListDetail['items'][number]) =>
          i.discountedPrice != null ? i.discountedPrice.toFixed(2) : '—',
      },
      { key: 'minQuantity', label: t('products.priceLists.minQuantity') },
      {
        key: 'effectivePrice',
        label: t('products.priceLists.effectivePrice'),
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
          <button className="btn-primary" onClick={() => setCreateOpen(true)}>
            {t('products.priceLists.add')}
          </button>
        )}
      </div>

      <div className="flex gap-2 border-b border-gray-200">
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
              tab === x.key ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-end gap-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-700">
            {t('products.priceLists.lookupId')}
          </label>
          <input
            className="input w-80"
            value={lookupId}
            onChange={(e) => setLookupId(e.target.value)}
            placeholder={t('products.priceLists.lookupIdPlaceholder') ?? ''}
          />
        </div>
        <button className="btn-outline" onClick={handleLookup}>
          {t('products.priceLists.load')}
        </button>
        {known.length > 0 && (
          <select
            className="input max-w-xs"
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
          </select>
        )}
      </div>

      {detailQuery.isError && (
        <p className="text-sm text-red-600">{t('products.priceLists.notFound')}</p>
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
          {detailQuery.data.description && <p className="text-sm text-gray-600">{detailQuery.data.description}</p>}
          <div className="grid grid-cols-2 gap-2 text-sm text-gray-600">
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
            <p className="text-sm text-gray-400">{t('products.priceLists.pickListFirst')}</p>
          ) : (
            <>
              <p className="text-xs text-gray-400">{t('products.priceLists.itemsReadOnlyNote')}</p>
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
