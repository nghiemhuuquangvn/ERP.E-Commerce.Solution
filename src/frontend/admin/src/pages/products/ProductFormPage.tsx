import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AxiosError } from 'axios'
import {
  productsApi,
  PRODUCT_TYPE,
  PRODUCT_STATUS,
  PRODUCT_STATUS_LABELS,
} from '@/api/products.api'
import { categoriesApi } from '@/api/product/categories.api'
import { brandsApi } from '@/api/product/brands.api'
import { unitsOfMeasureApi } from '@/api/masterdata/unitsOfMeasure.api'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'
import { ProductImagesTab } from './ProductImagesTab'
import { ProductVariantsTab } from './ProductVariantsTab'
import { ProductUomConversionsTab } from './ProductUomConversionsTab'

const PRODUCT_TYPE_OPTIONS = [
  { value: PRODUCT_TYPE.Physical, labelKey: 'products.types.physical' },
  { value: PRODUCT_TYPE.Digital, labelKey: 'products.types.digital' },
  { value: PRODUCT_TYPE.Service, labelKey: 'products.types.service' },
  { value: PRODUCT_TYPE.Bundle, labelKey: 'products.types.bundle' },
]

function extractErrorMessage(error: unknown): string | null {
  const axiosError = error as AxiosError<{ message?: string }>
  return axiosError?.response?.data?.message ?? null
}

type DetailTab = 'images' | 'variants' | 'uomConversions'

export function ProductFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const { id } = useParams<{ id: string }>()
  const isEdit = Boolean(id)
  const canEdit = hasPermission('products:edit')
  const canCreate = hasPermission('products:create')

  const { data: product, isLoading: isLoadingProduct } = useQuery({
    queryKey: ['products', id],
    queryFn: () => productsApi.get(id!),
    enabled: isEdit,
  })

  // listAll() walks the full category tree client-side (no single "all categories"
  // endpoint exists) — needed here since a product can be assigned to a category at
  // any depth, not just root-level ones.
  const { data: categories } = useQuery({
    queryKey: ['product', 'categories', 'picker'],
    queryFn: () => categoriesApi.listAll(),
    staleTime: 60_000,
  })

  const { data: brands } = useQuery({
    queryKey: ['product', 'brands', 'picker'],
    queryFn: () => brandsApi.list({ isActive: true }),
    staleTime: 60_000,
  })

  const { data: uomPage } = useQuery({
    queryKey: ['mst', 'unitsOfMeasure', 'picker'],
    queryFn: () => unitsOfMeasureApi.list({ pageSize: 200 }),
    staleTime: 60_000,
  })
  const uoms = uomPage?.items ?? []

  // Base fields — Name/Sku/ProductType/CategoryId only apply at creation time (there is
  // no generic PUT /products/{id}; see products.api.ts), so they're read-only once editing.
  const [name, setName] = useState('')
  const [sku, setSku] = useState('')
  const [productType, setProductType] = useState<number>(PRODUCT_TYPE.Physical)
  const [categoryId, setCategoryId] = useState('')
  const [description, setDescription] = useState('')
  const [brandId, setBrandId] = useState('')
  const [baseUomId, setBaseUomId] = useState('')
  const [slug, setSlug] = useState('')
  const [metaTitle, setMetaTitle] = useState('')
  const [metaDescription, setMetaDescription] = useState('')
  const [formError, setFormError] = useState<string | null>(null)
  const [discontinueConfirmOpen, setDiscontinueConfirmOpen] = useState(false)
  const [activeTab, setActiveTab] = useState<DetailTab>('images')

  useEffect(() => {
    if (!product) return
    setName(product.name)
    setSku(product.sku)
    setProductType(product.productType)
    setCategoryId(product.categoryId ?? '')
    setDescription(product.description ?? '')
    setBrandId(product.brandId ?? '')
    setBaseUomId(product.baseUomId ?? '')
  }, [product])

  const createMutation = useMutation({
    mutationFn: async () => {
      const created = await productsApi.create({
        name: name.trim(),
        sku: sku.trim().toUpperCase(),
        description: description.trim() || undefined,
        productType,
        categoryId: categoryId || null,
        brandId: brandId || null,
        baseUomId: baseUomId || null,
      })

      if (slug.trim() || metaTitle.trim() || metaDescription.trim()) {
        await productsApi.setSeo(created.productId, {
          slug: slug.trim() || null,
          metaTitle: metaTitle.trim() || null,
          metaDescription: metaDescription.trim() || null,
        })
      }

      return created
    },
    onSuccess: (created) => navigate(`/products/${created.productId}`),
  })

  // Composite save: dispatches only the granular calls needed for whichever detail
  // fields changed, since the API has no single update endpoint for the product as a whole.
  const saveDetailsMutation = useMutation({
    mutationFn: async () => {
      if (!product) return
      const calls: Promise<unknown>[] = []

      if (description.trim() && description.trim() !== (product.description ?? '')) {
        calls.push(productsApi.updateDescription(product.id, description.trim()))
      }
      if ((brandId || null) !== (product.brandId ?? null)) {
        calls.push(productsApi.setBrand(product.id, brandId || null))
      }
      if ((baseUomId || null) !== (product.baseUomId ?? null)) {
        calls.push(productsApi.setBaseUom(product.id, baseUomId || null))
      }
      calls.push(
        productsApi.setSeo(product.id, {
          slug: slug.trim() || null,
          metaTitle: metaTitle.trim() || null,
          metaDescription: metaDescription.trim() || null,
        }),
      )

      await Promise.all(calls)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products', id] }),
  })

  const submitForReviewMutation = useMutation({
    mutationFn: () => productsApi.submitForReview(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products', id] }),
  })

  const publishMutation = useMutation({
    mutationFn: () => productsApi.publish(id!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['products', id] }),
  })

  const discontinueMutation = useMutation({
    mutationFn: () => productsApi.discontinue(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', id] })
      setDiscontinueConfirmOpen(false)
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setFormError(null)

    if (!isEdit) {
      if (!name.trim() || name.trim().length < 3 || !sku.trim()) {
        setFormError(t('products.form.requiredFields'))
        return
      }
      createMutation.mutate()
    } else {
      saveDetailsMutation.mutate()
    }
  }

  const status = product?.status
  const canSubmitForReview = isEdit && status === PRODUCT_STATUS.Draft
  const canPublish =
    isEdit &&
    (status === PRODUCT_STATUS.Draft || status === PRODUCT_STATUS.PendingReview) &&
    (product?.variantCount ?? 0) > 0
  const canDiscontinue = isEdit && status !== undefined && status !== PRODUCT_STATUS.Discontinued
  const isDiscontinued = isEdit && status === PRODUCT_STATUS.Discontinued

  if (isEdit && isLoadingProduct) {
    return <div className="text-gray-400">{t('common.loading')}</div>
  }
  if (isEdit && !product) {
    return <div className="text-gray-400">{t('products.notFound')}</div>
  }

  const gated = isEdit ? canEdit : canCreate
  if (!gated) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/products" className="text-sm text-gray-500 hover:text-gray-700">
          {t('products.backToProducts')}
        </Link>
        <h1 className="text-xl font-semibold">
          {isEdit ? product!.name : t('products.newProduct')}
        </h1>
        {isEdit && <StatusBadge status={PRODUCT_STATUS_LABELS[status!] ?? String(status)} />}
      </div>

      <form onSubmit={handleSubmit} className="card max-w-3xl space-y-6 p-5">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.name')}</label>
            <input
              className="input"
              value={name}
              disabled={isEdit}
              onChange={(e) => setName(e.target.value)}
            />
            {isEdit && <p className="mt-1 text-xs text-gray-400">{t('products.form.notEditableAfterCreate')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.sku')}</label>
            <input
              className="input font-mono"
              value={sku}
              disabled={isEdit}
              onChange={(e) => setSku(e.target.value)}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.productType')}</label>
            <select
              className="input"
              value={productType}
              disabled={isEdit}
              onChange={(e) => setProductType(Number(e.target.value))}
            >
              {PRODUCT_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.labelKey)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.category')}</label>
            <select
              className="input"
              value={categoryId}
              disabled={isEdit}
              onChange={(e) => setCategoryId(e.target.value)}
            >
              <option value="">{t('common.none')}</option>
              {(categories ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.brand')}</label>
            <select className="input" value={brandId} onChange={(e) => setBrandId(e.target.value)}>
              <option value="">{t('common.none')}</option>
              {(brands ?? []).map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.baseUom')}</label>
            <select className="input" value={baseUomId} onChange={(e) => setBaseUomId(e.target.value)}>
              <option value="">{t('common.none')}</option>
              {uoms.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name} ({u.symbol})
                </option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.description')}</label>
          <textarea
            className="input"
            rows={3}
            maxLength={2000}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="border-t border-gray-100 pt-4">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">
            {t('products.seo.title')}
          </h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.seo.slug')}</label>
              <input className="input" value={slug} onChange={(e) => setSlug(e.target.value)} maxLength={256} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.seo.metaTitle')}</label>
              <input
                className="input"
                value={metaTitle}
                onChange={(e) => setMetaTitle(e.target.value)}
                maxLength={200}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.seo.metaDescription')}</label>
              <input
                className="input"
                value={metaDescription}
                onChange={(e) => setMetaDescription(e.target.value)}
                maxLength={320}
              />
            </div>
          </div>
        </div>

        {formError && <p className="text-sm text-red-600">{formError}</p>}
        {(createMutation.isError || saveDetailsMutation.isError) && (
          <p className="text-sm text-red-600">
            {extractErrorMessage(createMutation.error ?? saveDetailsMutation.error) ?? t('common.failedToSave')}
          </p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <Link to="/products" className="btn-outline">
            {t('common.cancel')}
          </Link>
          <button
            type="submit"
            disabled={createMutation.isPending || saveDetailsMutation.isPending}
            className="btn-primary"
          >
            {createMutation.isPending || saveDetailsMutation.isPending
              ? t('common.saving')
              : t('common.save')}
          </button>
        </div>
      </form>

      {isEdit && product && (
        <div className="card max-w-3xl space-y-3 p-5">
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide">
            {t('products.statusActions.title')}
          </h2>
          <div className="flex flex-wrap gap-3">
            {canSubmitForReview && (
              <button
                className="btn-outline"
                disabled={submitForReviewMutation.isPending}
                onClick={() => submitForReviewMutation.mutate()}
              >
                {t('products.statusActions.submitForReview')}
              </button>
            )}
            {canPublish && (
              <button
                className="btn-primary"
                disabled={publishMutation.isPending}
                onClick={() => publishMutation.mutate()}
              >
                {t('products.statusActions.publish')}
              </button>
            )}
            {status !== PRODUCT_STATUS.Draft &&
              status !== PRODUCT_STATUS.PendingReview &&
              (product.variantCount ?? 0) === 0 && (
                <p className="self-center text-xs text-gray-400">{t('products.statusActions.publishNeedsVariant')}</p>
              )}
            {canDiscontinue && (
              <button
                className="btn-danger"
                disabled={discontinueMutation.isPending}
                onClick={() => setDiscontinueConfirmOpen(true)}
              >
                {t('products.statusActions.discontinue')}
              </button>
            )}
            {isDiscontinued && (
              <button className="btn-outline" disabled title={t('products.statusActions.reactivateUnavailable')}>
                {t('products.statusActions.reactivate')}
              </button>
            )}
          </div>
          {isDiscontinued && (
            <p className="text-xs text-gray-400">{t('products.statusActions.reactivateUnavailable')}</p>
          )}
          {(submitForReviewMutation.isError || publishMutation.isError || discontinueMutation.isError) && (
            <p className="text-sm text-red-600">
              {extractErrorMessage(
                submitForReviewMutation.error ?? publishMutation.error ?? discontinueMutation.error,
              ) ?? t('common.failedToSave')}
            </p>
          )}
        </div>
      )}

      {isEdit && product && (
        <div className="max-w-3xl space-y-4">
          <div className="flex gap-1 border-b border-gray-200">
            {(
              [
                { key: 'images', labelKey: 'products.tabs.images' },
                { key: 'variants', labelKey: 'products.tabs.variants' },
                { key: 'uomConversions', labelKey: 'products.tabs.uomConversions' },
              ] as { key: DetailTab; labelKey: string }[]
            ).map((tab) => (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium ${
                  activeTab === tab.key
                    ? 'border-primary-600 text-primary-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                {t(tab.labelKey)}
              </button>
            ))}
          </div>

          {activeTab === 'images' && <ProductImagesTab productId={product.id} />}
          {activeTab === 'variants' && <ProductVariantsTab productId={product.id} />}
          {activeTab === 'uomConversions' && <ProductUomConversionsTab productId={product.id} />}
        </div>
      )}

      <ConfirmDialog
        open={discontinueConfirmOpen}
        title={t('products.statusActions.discontinueTitle')}
        message={t('products.statusActions.discontinueConfirm', { name: product?.name })}
        confirmLabel={t('products.statusActions.discontinue')}
        danger
        onConfirm={() => discontinueMutation.mutate()}
        onCancel={() => setDiscontinueConfirmOpen(false)}
      />
    </div>
  )
}
