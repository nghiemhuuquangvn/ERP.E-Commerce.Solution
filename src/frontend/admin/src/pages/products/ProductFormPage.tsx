import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
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
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Select, Spinner, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { ProductImagesTab } from './ProductImagesTab'
import { ProductVariantsTab } from './ProductVariantsTab'
import { ProductUomConversionsTab } from './ProductUomConversionsTab'
import { ProductAttributesTab } from './ProductAttributesTab'

const PRODUCT_TYPE_OPTIONS = [
  { value: PRODUCT_TYPE.Physical, labelKey: 'products.types.physical' },
  { value: PRODUCT_TYPE.Digital, labelKey: 'products.types.digital' },
  { value: PRODUCT_TYPE.Service, labelKey: 'products.types.service' },
  { value: PRODUCT_TYPE.Bundle, labelKey: 'products.types.bundle' },
]

type DetailTab = 'images' | 'variants' | 'specifications' | 'uomConversions'

export function ProductFormPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
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
  const [isLotControlled, setIsLotControlled] = useState(false)
  const [isSerialControlled, setIsSerialControlled] = useState(false)
  const [slug, setSlug] = useState('')
  const [metaTitle, setMetaTitle] = useState('')
  const [metaDescription, setMetaDescription] = useState('')
  const [fieldErrors, setFieldErrors] = useState<{ name?: string; sku?: string }>({})
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
    setIsLotControlled(Boolean(product.isLotControlled))
    setIsSerialControlled(Boolean(product.isSerialControlled))
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
        isLotControlled,
        isSerialControlled,
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
    onSuccess: (created) => {
      toast.success(t('common.saved'))
      navigate(`/products/${created.productId}`)
    },
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
      if (isLotControlled !== Boolean(product.isLotControlled) || isSerialControlled !== Boolean(product.isSerialControlled)) {
        calls.push(productsApi.setTracking(product.id, isLotControlled, isSerialControlled))
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
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', id] })
      toast.success(t('common.saved'))
    },
  })

  const submitForReviewMutation = useMutation({
    mutationFn: () => productsApi.submitForReview(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', id] })
      toast.success(t('common.saved'))
    },
  })

  const publishMutation = useMutation({
    mutationFn: () => productsApi.publish(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', id] })
      toast.success(t('common.saved'))
    },
  })

  const discontinueMutation = useMutation({
    mutationFn: () => productsApi.discontinue(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products', id] })
      toast.success(t('common.saved'))
      setDiscontinueConfirmOpen(false)
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()

    if (!isEdit) {
      const errs: { name?: string; sku?: string } = {}
      if (!name.trim()) errs.name = t('common.required')
      else if (name.trim().length < 3) errs.name = t('products.form.nameTooShort')
      if (!sku.trim()) errs.sku = t('common.required')
      setFieldErrors(errs)
      if (errs.name || errs.sku) return
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
    return <Spinner />
  }
  if (isEdit && !product) {
    return <EmptyState variant="error" title={t('products.notFound')} />
  }

  const gated = isEdit ? canEdit : canCreate
  if (!gated) {
    return <EmptyState variant="forbidden" title={t('common.noPermission')} />
  }

  const saving = createMutation.isPending || saveDetailsMutation.isPending
  const saveError = createMutation.error ?? saveDetailsMutation.error
  const actionError = submitForReviewMutation.error ?? publishMutation.error ?? discontinueMutation.error

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/products" className="text-sm text-ink-secondary hover:text-ink">
          {t('products.backToProducts')}
        </Link>
        <h1 className="text-xl font-semibold">
          {isEdit ? product!.name : t('products.newProduct')}
        </h1>
        {isEdit && <StatusBadge status={PRODUCT_STATUS_LABELS[status!] ?? String(status)} />}
      </div>

      <form onSubmit={handleSubmit} className="card space-y-6 p-5">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <FormField
            label={t('products.name')}
            required={!isEdit}
            error={fieldErrors.name}
            hint={isEdit ? t('products.form.notEditableAfterCreate') : undefined}
          >
            <Input value={name} disabled={isEdit} onChange={(e) => setName(e.target.value)} />
          </FormField>
          <FormField label={t('products.sku')} required={!isEdit} error={fieldErrors.sku}>
            <Input className="font-mono" value={sku} disabled={isEdit} onChange={(e) => setSku(e.target.value)} />
          </FormField>
          <FormField label={t('products.productType')}>
            <Select value={productType} disabled={isEdit} onChange={(e) => setProductType(Number(e.target.value))}>
              {PRODUCT_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.labelKey)}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label={t('products.category')}>
            <Select value={categoryId} disabled={isEdit} onChange={(e) => setCategoryId(e.target.value)}>
              <option value="">{t('common.none')}</option>
              {(categories ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label={t('products.brand')}>
            <Select value={brandId} onChange={(e) => setBrandId(e.target.value)}>
              <option value="">{t('common.none')}</option>
              {(brands ?? []).map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </Select>
          </FormField>
          <FormField label={t('products.baseUom')}>
            <Select value={baseUomId} onChange={(e) => setBaseUomId(e.target.value)}>
              <option value="">{t('common.none')}</option>
              {uoms.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name} ({u.symbol})
                </option>
              ))}
            </Select>
          </FormField>
        </div>

        <div className="flex flex-wrap gap-6">
          <Checkbox checked={isLotControlled} onChange={(e) => setIsLotControlled(e.target.checked)} label={t('products.lotControlled')} />
          <Checkbox checked={isSerialControlled} onChange={(e) => setIsSerialControlled(e.target.checked)} label={t('products.serialControlled')} />
        </div>

        <FormField label={t('products.description')}>
          <Textarea rows={3} maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} />
        </FormField>

        <div className="border-t border-line pt-4">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">
            {t('products.seo.title')}
          </h2>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <FormField className="sm:col-span-2 lg:col-span-1" label={t('products.seo.slug')}>
              <Input value={slug} onChange={(e) => setSlug(e.target.value)} maxLength={256} />
            </FormField>
            <FormField label={t('products.seo.metaTitle')}>
              <Input value={metaTitle} onChange={(e) => setMetaTitle(e.target.value)} maxLength={200} />
            </FormField>
            <FormField label={t('products.seo.metaDescription')}>
              <Input value={metaDescription} onChange={(e) => setMetaDescription(e.target.value)} maxLength={320} />
            </FormField>
          </div>
        </div>

        {saveError && <Alert variant="danger">{serverMessage(saveError, t('common.failedToSave'))}</Alert>}

        <div className="flex justify-end gap-3 pt-2">
          <Link to="/products" className="btn-outline">
            {t('common.cancel')}
          </Link>
          <Button type="submit" loading={saving}>
            {t('common.save')}
          </Button>
        </div>
      </form>

      <div className="card space-y-3 p-5">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-ink-secondary">
          {t('products.statusActions.title')}
        </h2>
        {!product ? (
          <p className="text-sm text-ink-secondary">{t('products.form.statusActionsUnavailable')}</p>
        ) : (
          <>
          <div className="flex flex-wrap gap-3">
            {canSubmitForReview && (
              <Button variant="outline" loading={submitForReviewMutation.isPending} onClick={() => submitForReviewMutation.mutate()}>
                {t('products.statusActions.submitForReview')}
              </Button>
            )}
            {canPublish && (
              <Button loading={publishMutation.isPending} onClick={() => publishMutation.mutate()}>
                {t('products.statusActions.publish')}
              </Button>
            )}
            {status !== PRODUCT_STATUS.Draft &&
              status !== PRODUCT_STATUS.PendingReview &&
              (product.variantCount ?? 0) === 0 && (
                <p className="self-center text-xs text-ink-secondary">{t('products.statusActions.publishNeedsVariant')}</p>
              )}
            {canDiscontinue && (
              <Button variant="danger" loading={discontinueMutation.isPending} onClick={() => setDiscontinueConfirmOpen(true)}>
                {t('products.statusActions.discontinue')}
              </Button>
            )}
            {isDiscontinued && (
              <Button variant="outline" disabled title={t('products.statusActions.reactivateUnavailable')}>
                {t('products.statusActions.reactivate')}
              </Button>
            )}
          </div>
          {isDiscontinued && (
            <p className="text-xs text-ink-secondary">{t('products.statusActions.reactivateUnavailable')}</p>
          )}
          {actionError && <Alert variant="danger">{serverMessage(actionError, t('common.failedToSave'))}</Alert>}
          </>
        )}
      </div>

      <div className="space-y-4">
        <div role="tablist" className="flex gap-1 border-b border-line">
          {(
            [
              { key: 'images', labelKey: 'products.tabs.images' },
              { key: 'variants', labelKey: 'products.tabs.variants' },
              { key: 'specifications', labelKey: 'products.tabs.specifications' },
              { key: 'uomConversions', labelKey: 'products.tabs.uomConversions' },
            ] as { key: DetailTab; labelKey: string }[]
          ).map((tab) => (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={activeTab === tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium ${
                activeTab === tab.key
                  ? 'border-brand-action text-brand-action'
                  : 'border-transparent text-ink-secondary hover:text-ink'
              }`}
            >
              {t(tab.labelKey)}
            </button>
          ))}
        </div>

        {!product ? (
          <div className="card p-8 text-center text-sm text-ink-secondary">
            {t('products.form.saveFirstToUnlock')}
          </div>
        ) : (
          <div role="tabpanel">
            {activeTab === 'images' && <ProductImagesTab productId={product.id} />}
            {activeTab === 'variants' && (
              <ProductVariantsTab productId={product.id} categoryId={product.categoryId} />
            )}
            {activeTab === 'specifications' && (
              <ProductAttributesTab productId={product.id} categoryId={product.categoryId} />
            )}
            {activeTab === 'uomConversions' && <ProductUomConversionsTab productId={product.id} />}
          </div>
        )}
      </div>

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
