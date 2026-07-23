import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  suppliersApi,
  type Address,
  type CreateSupplierRequest,
  type UpdateSupplierRequest,
} from '@/api/suppliers.api'
import { paymentTermsApi } from '@/api/masterdata/paymentTerms.api'
import { productsApi, type Product } from '@/api/products.api'
import { usePermission } from '@/hooks/usePermission'

type Tab = 'details' | 'ratings' | 'items'

const EMPTY_ADDRESS: Address = { street: '', city: '', state: '', postalCode: '', country: '' }

/** Resolves a bare productId into a display name; falls back to the id itself. */
function ProductNameCell({ productId }: { productId: string }) {
  const { data } = useQuery({
    queryKey: ['product', productId],
    queryFn: () => productsApi.get(productId),
    staleTime: 5 * 60_000,
  })
  return <>{data?.name ?? productId}</>
}

function ProductPicker({
  value,
  valueLabel,
  onChange,
}: {
  value: string
  valueLabel?: string
  onChange: (product: Product) => void
}) {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  // ProductsController has no `search` param on the paginated list endpoint — free-text
  // search is the separate, non-paginated GET /v1/products/search endpoint (see
  // products.api.ts's `search` doc comment).
  const { data } = useQuery({
    queryKey: ['products', 'picker', search],
    queryFn: () => productsApi.search(search),
    enabled: search.trim().length >= 2,
    staleTime: 30_000,
  })

  return (
    <div className="space-y-1">
      <input
        className="input"
        placeholder={t('purchasing.suppliers.items.searchProductPlaceholder')}
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />
      {value && (
        <p className="text-xs text-gray-500">
          {t('purchasing.suppliers.items.selectedProduct')}: <strong>{valueLabel ?? value}</strong>
        </p>
      )}
      {data && data.length > 0 && (
        <div className="max-h-40 overflow-y-auto rounded border border-gray-200">
          {data.map((p) => (
            <button
              type="button"
              key={p.id}
              onClick={() => {
                onChange(p)
                setSearch('')
              }}
              className="block w-full px-3 py-1.5 text-left text-sm hover:bg-gray-50"
            >
              {p.name} <span className="text-gray-400">({p.sku})</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export function SupplierFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const isEdit = Boolean(id)

  const [tab, setTab] = useState<Tab>('details')

  // --- Main details form state ---
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [contactPerson, setContactPerson] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [address, setAddress] = useState<Address>(EMPTY_ADDRESS)
  const [taxCode, setTaxCode] = useState('')
  const [currencyCode, setCurrencyCode] = useState('USD')
  const [paymentTermDaysOnCreate, setPaymentTermDaysOnCreate] = useState(30)
  const [error, setError] = useState<string | null>(null)

  // --- Payment term section state (edit mode only) ---
  const [paymentTermId, setPaymentTermId] = useState<string>('')
  const [paymentTermDays, setPaymentTermDays] = useState(30)

  // --- Ratings tab state ---
  const [newRating, setNewRating] = useState(5)
  const [newComment, setNewComment] = useState('')

  // --- Items tab state ---
  const [itemProductId, setItemProductId] = useState('')
  const [itemProductLabel, setItemProductLabel] = useState('')
  const [itemVariantId, setItemVariantId] = useState('')
  const [itemCode, setItemCode] = useState('')
  const [itemPrice, setItemPrice] = useState(0)
  const [itemLeadTime, setItemLeadTime] = useState(0)
  const [editingItemId, setEditingItemId] = useState<string | null>(null)

  const { data: supplier } = useQuery({
    queryKey: ['supplier', id],
    queryFn: () => suppliersApi.get(id!),
    enabled: isEdit,
  })

  useEffect(() => {
    if (!supplier) return
    setCode(supplier.code)
    setName(supplier.name)
    setContactPerson(supplier.contactPerson ?? '')
    setEmail(supplier.email ?? '')
    setPhone(supplier.phone ?? '')
    setAddress(supplier.address ?? EMPTY_ADDRESS)
    setTaxCode(supplier.taxCode ?? '')
    setCurrencyCode(supplier.currencyCode)
    setPaymentTermId(supplier.paymentTermId ?? '')
    setPaymentTermDays(supplier.paymentTermDays)
  }, [supplier])

  const { data: paymentTerms } = useQuery({
    queryKey: ['masterdata', 'paymentTerms', 'forSupplierForm'],
    queryFn: () => paymentTermsApi.list({ pageSize: 100 }),
    enabled: isEdit,
    staleTime: 60_000,
  })

  const saveMutation = useMutation({
    mutationFn: () => {
      if (isEdit) {
        const body: UpdateSupplierRequest = {
          name: name.trim(),
          contactPerson: contactPerson.trim() || undefined,
          email: email.trim() || undefined,
          phone: phone.trim() || undefined,
          address,
          taxCode: taxCode.trim() || undefined,
          currencyCode: currencyCode.trim() || undefined,
        }
        return suppliersApi.update(id!, body)
      }
      const body: CreateSupplierRequest = {
        code: code.trim(),
        name: name.trim(),
        contactPerson: contactPerson.trim() || undefined,
        email: email.trim() || undefined,
        phone: phone.trim() || undefined,
        address,
        taxCode: taxCode.trim() || undefined,
        paymentTermDays: paymentTermDaysOnCreate,
        currencyCode: currencyCode.trim() || undefined,
      }
      return suppliersApi.create(body)
    },
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
      if (!isEdit) navigate(`/purchasing/suppliers/${result.id}`)
      else queryClient.invalidateQueries({ queryKey: ['supplier', id] })
    },
  })

  const paymentTermMutation = useMutation({
    mutationFn: () =>
      suppliersApi.setPaymentTerm(id!, {
        paymentTermId: paymentTermId || null,
        paymentTermDays,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier', id] })
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (!name.trim() || (!isEdit && !code.trim())) {
      setError(t('purchasing.suppliers.form.requiredFields'))
      return
    }
    saveMutation.mutate()
  }

  const handlePaymentTermSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    paymentTermMutation.mutate()
  }

  // --- Ratings ---
  const { data: ratingsResult } = useQuery({
    queryKey: ['supplier', id, 'ratings'],
    queryFn: () => suppliersApi.getRatings(id!),
    enabled: isEdit && tab === 'ratings',
  })

  const rateMutation = useMutation({
    mutationFn: () => suppliersApi.rate(id!, { rating: newRating, comment: newComment.trim() || undefined }),
    onSuccess: () => {
      setNewComment('')
      setNewRating(5)
      queryClient.invalidateQueries({ queryKey: ['supplier', id, 'ratings'] })
    },
  })

  // --- Items ---
  const { data: items } = useQuery({
    queryKey: ['supplier', id, 'items'],
    queryFn: () => suppliersApi.listItems(id!),
    enabled: isEdit && tab === 'items',
  })

  const resetItemForm = () => {
    setEditingItemId(null)
    setItemProductId('')
    setItemProductLabel('')
    setItemVariantId('')
    setItemCode('')
    setItemPrice(0)
    setItemLeadTime(0)
  }

  const itemMutation = useMutation({
    mutationFn: () => {
      const body = {
        productId: itemProductId,
        variantId: itemVariantId.trim() || undefined,
        supplierItemCode: itemCode.trim() || undefined,
        lastPurchasePrice: itemPrice,
        leadTimeDays: itemLeadTime,
      }
      return editingItemId
        ? suppliersApi.updateItem(id!, editingItemId, body)
        : suppliersApi.createItem(id!, body)
    },
    onSuccess: () => {
      resetItemForm()
      queryClient.invalidateQueries({ queryKey: ['supplier', id, 'items'] })
    },
  })

  const handleItemSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!itemProductId) return
    itemMutation.mutate()
  }

  const canEditDetails = hasPermission('supplier:update')
  const canRate = hasPermission('supplier:rate')

  const tabs = useMemo(() => {
    const list: { key: Tab; labelKey: string }[] = [
      { key: 'details', labelKey: 'purchasing.suppliers.tabs.details' },
    ]
    if (isEdit) {
      list.push({ key: 'ratings', labelKey: 'purchasing.suppliers.tabs.ratings' })
      list.push({ key: 'items', labelKey: 'purchasing.suppliers.tabs.items' })
    }
    return list
  }, [isEdit])

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/purchasing/suppliers" className="text-sm text-gray-500 hover:text-gray-700">
          {t('purchasing.suppliers.backToSuppliers')}
        </Link>
        <h1 className="text-xl font-semibold">
          {isEdit ? t('purchasing.suppliers.editTitle') : t('purchasing.suppliers.addTitle')}
        </h1>
      </div>

      {tabs.length > 1 && (
        <div className="flex gap-1 border-b border-gray-200">
          {tabs.map((tb) => (
            <button
              key={tb.key}
              onClick={() => setTab(tb.key)}
              className={`px-4 py-2 text-sm font-medium ${
                tab === tb.key
                  ? 'border-b-2 border-primary-600 text-primary-600'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              {t(tb.labelKey)}
            </button>
          ))}
        </div>
      )}

      {tab === 'details' && (
        <div className="space-y-6">
          <form onSubmit={handleSubmit} className="card max-w-2xl space-y-4 p-5">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.code')}
                </label>
                <input
                  className="input"
                  value={code}
                  disabled={isEdit}
                  onChange={(e) => setCode(e.target.value)}
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.name')}
                </label>
                <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.contactPerson')}
                </label>
                <input
                  className="input"
                  value={contactPerson}
                  onChange={(e) => setContactPerson(e.target.value)}
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.email')}
                </label>
                <input
                  type="email"
                  className="input"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.phone')}
                </label>
                <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.taxCode')}
                </label>
                <input className="input" value={taxCode} onChange={(e) => setTaxCode(e.target.value)} />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('purchasing.suppliers.currencyCode')}
                </label>
                <input
                  className="input"
                  value={currencyCode}
                  onChange={(e) => setCurrencyCode(e.target.value.toUpperCase())}
                />
              </div>
              {!isEdit && (
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.paymentTermDays')}
                  </label>
                  <input
                    type="number"
                    min={0}
                    className="input"
                    value={paymentTermDaysOnCreate}
                    onChange={(e) => setPaymentTermDaysOnCreate(Number(e.target.value))}
                  />
                </div>
              )}
            </div>

            <fieldset className="space-y-3 rounded border border-gray-200 p-4">
              <legend className="px-1 text-sm font-medium text-gray-700">
                {t('purchasing.suppliers.address.title')}
              </legend>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600">
                    {t('purchasing.suppliers.address.street')}
                  </label>
                  <input
                    className="input"
                    value={address.street}
                    onChange={(e) => setAddress({ ...address, street: e.target.value })}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600">
                    {t('purchasing.suppliers.address.city')}
                  </label>
                  <input
                    className="input"
                    value={address.city}
                    onChange={(e) => setAddress({ ...address, city: e.target.value })}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600">
                    {t('purchasing.suppliers.address.state')}
                  </label>
                  <input
                    className="input"
                    value={address.state}
                    onChange={(e) => setAddress({ ...address, state: e.target.value })}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600">
                    {t('purchasing.suppliers.address.postalCode')}
                  </label>
                  <input
                    className="input"
                    value={address.postalCode}
                    onChange={(e) => setAddress({ ...address, postalCode: e.target.value })}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600">
                    {t('purchasing.suppliers.address.country')}
                  </label>
                  <input
                    className="input"
                    value={address.country}
                    onChange={(e) => setAddress({ ...address, country: e.target.value })}
                  />
                </div>
              </div>
            </fieldset>

            {error && <p className="text-sm text-red-600">{error}</p>}
            {saveMutation.isError && <p className="text-sm text-red-600">{t('common.failedToSave')}</p>}

            <div className="flex justify-end gap-3 pt-2">
              <Link to="/purchasing/suppliers" className="btn-outline">
                {t('common.cancel')}
              </Link>
              {(!isEdit || canEditDetails) && (
                <button type="submit" disabled={saveMutation.isPending} className="btn-primary">
                  {saveMutation.isPending ? t('common.saving') : t('common.save')}
                </button>
              )}
            </div>
          </form>

          {isEdit && (
            <form onSubmit={handlePaymentTermSubmit} className="card max-w-2xl space-y-4 p-5">
              <h2 className="text-base font-semibold text-gray-900">
                {t('purchasing.suppliers.paymentTerm.title')}
              </h2>
              <p className="text-xs text-gray-500">{t('purchasing.suppliers.paymentTerm.hint')}</p>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.paymentTerm.paymentTerm')}
                  </label>
                  <select
                    className="input"
                    value={paymentTermId}
                    onChange={(e) => setPaymentTermId(e.target.value)}
                  >
                    <option value="">{t('purchasing.suppliers.paymentTerm.none')}</option>
                    {paymentTerms?.items
                      .filter((pt) => pt.isActive)
                      .map((pt) => (
                        <option key={pt.id} value={pt.id}>
                          {pt.code} — {pt.name} ({pt.netDays}d)
                        </option>
                      ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.paymentTermDays')}
                  </label>
                  <input
                    type="number"
                    min={0}
                    className="input"
                    value={paymentTermDays}
                    onChange={(e) => setPaymentTermDays(Number(e.target.value))}
                  />
                </div>
              </div>
              {paymentTermMutation.isError && (
                <p className="text-sm text-red-600">{t('common.failedToSave')}</p>
              )}
              <div className="flex justify-end pt-2">
                {canEditDetails && (
                  <button type="submit" disabled={paymentTermMutation.isPending} className="btn-primary">
                    {paymentTermMutation.isPending
                      ? t('common.saving')
                      : t('purchasing.suppliers.paymentTerm.save')}
                  </button>
                )}
              </div>
            </form>
          )}
        </div>
      )}

      {tab === 'ratings' && isEdit && (
        <div className="max-w-2xl space-y-6">
          <div className="card p-5">
            <div className="mb-4 flex items-center gap-4">
              <div>
                <p className="text-2xl font-semibold text-gray-900">
                  {ratingsResult?.averageRating ?? 0}
                  <span className="text-sm font-normal text-gray-500"> / 5</span>
                </p>
                <p className="text-xs text-gray-500">
                  {t('purchasing.suppliers.ratings.totalCount', {
                    count: ratingsResult?.totalRatings ?? 0,
                  })}
                </p>
              </div>
            </div>
            <ul className="divide-y divide-gray-100">
              {(ratingsResult?.ratings ?? []).map((r) => (
                <li key={r.id} className="py-3">
                  <div className="flex items-center justify-between">
                    <span className="font-medium text-gray-900">{r.rating} / 5</span>
                    <span className="text-xs text-gray-400">{r.ratedBy}</span>
                  </div>
                  {r.comment && <p className="mt-1 text-sm text-gray-600">{r.comment}</p>}
                </li>
              ))}
              {(!ratingsResult || ratingsResult.ratings.length === 0) && (
                <li className="py-6 text-center text-sm text-gray-400">
                  {t('purchasing.suppliers.ratings.empty')}
                </li>
              )}
            </ul>
          </div>

          {canRate && (
            <form
              onSubmit={(e) => {
                e.preventDefault()
                rateMutation.mutate()
              }}
              className="card space-y-4 p-5"
            >
              <h3 className="text-sm font-semibold text-gray-900">
                {t('purchasing.suppliers.ratings.addTitle')}
              </h3>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.ratings.rating')}
                  </label>
                  <select
                    className="input"
                    value={newRating}
                    onChange={(e) => setNewRating(Number(e.target.value))}
                  >
                    {[1, 2, 3, 4, 5].map((n) => (
                      <option key={n} value={n}>
                        {n}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.ratings.comment')}
                  </label>
                  <input className="input" value={newComment} onChange={(e) => setNewComment(e.target.value)} />
                </div>
              </div>
              {rateMutation.isError && <p className="text-sm text-red-600">{t('common.failedToSave')}</p>}
              <div className="flex justify-end">
                <button type="submit" disabled={rateMutation.isPending} className="btn-primary">
                  {rateMutation.isPending ? t('common.saving') : t('purchasing.suppliers.ratings.submit')}
                </button>
              </div>
            </form>
          )}
        </div>
      )}

      {tab === 'items' && isEdit && (
        <div className="max-w-3xl space-y-6">
          <div className="card overflow-x-auto p-0">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                    {t('purchasing.suppliers.items.product')}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                    {t('purchasing.suppliers.items.itemCode')}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                    {t('purchasing.suppliers.items.lastPurchasePrice')}
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                    {t('purchasing.suppliers.items.leadTimeDays')}
                  </th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {(items ?? []).map((item) => (
                  <tr key={item.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3">
                      <ProductNameCell productId={item.productId} />
                    </td>
                    <td className="px-4 py-3">{item.supplierItemCode ?? '—'}</td>
                    <td className="px-4 py-3">{item.lastPurchasePrice}</td>
                    <td className="px-4 py-3">{item.leadTimeDays}</td>
                    <td className="px-4 py-3">
                      {canEditDetails && (
                        <button
                          className="text-xs text-primary-600 hover:underline"
                          onClick={() => {
                            setEditingItemId(item.id)
                            setItemProductId(item.productId)
                            setItemProductLabel(item.productId)
                            setItemVariantId(item.variantId ?? '')
                            setItemCode(item.supplierItemCode ?? '')
                            setItemPrice(item.lastPurchasePrice)
                            setItemLeadTime(item.leadTimeDays)
                          }}
                        >
                          {t('common.edit')}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {(!items || items.length === 0) && (
                  <tr>
                    <td colSpan={5} className="py-8 text-center text-gray-400">
                      {t('common.noRecordsFound')}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {canEditDetails && (
            <form onSubmit={handleItemSubmit} className="card space-y-4 p-5">
              <h3 className="text-sm font-semibold text-gray-900">
                {editingItemId
                  ? t('purchasing.suppliers.items.editTitle')
                  : t('purchasing.suppliers.items.addTitle')}
              </h3>
              <ProductPicker
                value={itemProductId}
                valueLabel={itemProductLabel}
                onChange={(p) => {
                  setItemProductId(p.id)
                  setItemProductLabel(p.name)
                }}
              />
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.items.variantId')}
                  </label>
                  <input
                    className="input"
                    value={itemVariantId}
                    onChange={(e) => setItemVariantId(e.target.value)}
                    placeholder={t('purchasing.suppliers.items.variantIdPlaceholder')}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.items.itemCode')}
                  </label>
                  <input className="input" value={itemCode} onChange={(e) => setItemCode(e.target.value)} />
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.items.lastPurchasePrice')}
                  </label>
                  <input
                    type="number"
                    min={0}
                    step="0.01"
                    className="input"
                    value={itemPrice}
                    onChange={(e) => setItemPrice(Number(e.target.value))}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    {t('purchasing.suppliers.items.leadTimeDays')}
                  </label>
                  <input
                    type="number"
                    min={0}
                    className="input"
                    value={itemLeadTime}
                    onChange={(e) => setItemLeadTime(Number(e.target.value))}
                  />
                </div>
              </div>
              {itemMutation.isError && <p className="text-sm text-red-600">{t('common.failedToSave')}</p>}
              <div className="flex justify-end gap-3">
                {editingItemId && (
                  <button type="button" onClick={resetItemForm} className="btn-outline">
                    {t('common.cancel')}
                  </button>
                )}
                <button type="submit" disabled={itemMutation.isPending || !itemProductId} className="btn-primary">
                  {itemMutation.isPending ? t('common.saving') : t('common.save')}
                </button>
              </div>
            </form>
          )}
        </div>
      )}
    </div>
  )
}
