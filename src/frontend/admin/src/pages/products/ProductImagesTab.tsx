import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductImage, UploadProductImageRequest } from '@/api/products.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, FormField, Input, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

// Matches ProductImageFileConstraints on the backend (ERP.PRD.Application/Features/Images/Commands).
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024

interface UrlFormValues {
  imageUrl: string
  altText: string
  variantId: string
  setAsPrimary: boolean
}

function validateImageFile(file: File): 'invalidFileType' | 'fileTooLarge' | null {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) return 'invalidFileType'
  if (file.size > MAX_IMAGE_SIZE_BYTES) return 'fileTooLarge'
  return null
}

export function ProductImagesTab({ productId }: { productId: string }) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const canEdit = hasPermission('products:edit')

  const [removeTarget, setRemoveTarget] = useState<ProductImage | null>(null)
  const [showUrlForm, setShowUrlForm] = useState(false)
  const [isDragging, setIsDragging] = useState(false)
  const [fileError, setFileError] = useState<'invalidFileType' | 'fileTooLarge' | null>(null)
  const [replaceTargetId, setReplaceTargetId] = useState<string | null>(null)
  const [pendingAltText, setPendingAltText] = useState('')
  const [pendingVariantId, setPendingVariantId] = useState('')
  const [pendingSetAsPrimary, setPendingSetAsPrimary] = useState(false)

  const fileInputRef = useRef<HTMLInputElement>(null)
  const replaceInputRef = useRef<HTMLInputElement>(null)

  const { data: images, isLoading } = useQuery({
    queryKey: ['products', productId, 'images'],
    queryFn: () => productsApi.listImages(productId),
  })

  const { data: variants } = useQuery({
    queryKey: ['products', productId, 'variants'],
    queryFn: () => productsApi.listVariants(productId),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['products', productId, 'images'] })
    toast.success(t('common.saved'))
  }

  const uploadFileMutation = useMutation({
    mutationFn: (file: File) =>
      productsApi.uploadImageFile(productId, file, {
        altText: pendingAltText.trim() || undefined,
        variantId: pendingVariantId || undefined,
        setAsPrimary: pendingSetAsPrimary,
      }),
    onSuccess: () => {
      invalidate()
      setPendingAltText('')
      setPendingVariantId('')
      setPendingSetAsPrimary(false)
    },
  })

  const replaceMutation = useMutation({
    mutationFn: ({ image, file }: { image: ProductImage; file: File }) =>
      productsApi.updateImage(productId, image.id, {
        file,
        sortOrder: image.sortOrder,
        altText: image.altText ?? undefined,
      }),
    onSuccess: () => {
      invalidate()
      setReplaceTargetId(null)
    },
  })

  const { register, handleSubmit, reset, formState: { errors } } = useForm<UrlFormValues>({
    defaultValues: { imageUrl: '', altText: '', variantId: '', setAsPrimary: false },
  })

  const uploadUrlMutation = useMutation({
    mutationFn: (values: UrlFormValues) => {
      const body: UploadProductImageRequest = {
        imageUrl: values.imageUrl.trim(),
        altText: values.altText.trim() || undefined,
        variantId: values.variantId || undefined,
        setAsPrimary: values.setAsPrimary,
      }
      return productsApi.uploadImage(productId, body)
    },
    onSuccess: () => {
      invalidate()
      reset()
    },
  })

  const setPrimaryMutation = useMutation({
    mutationFn: (imageId: string) => productsApi.setPrimaryImage(productId, imageId),
    onSuccess: invalidate,
  })

  const removeMutation = useMutation({
    mutationFn: (imageId: string) => productsApi.removeImage(productId, imageId),
    onSuccess: () => {
      invalidate()
      setRemoveTarget(null)
    },
  })

  const sortedImages = [...(images ?? [])].sort((a, b) => a.sortOrder - b.sortOrder)

  const handleFiles = (files: FileList | null) => {
    const file = files?.[0]
    if (!file) return
    const error = validateImageFile(file)
    if (error) {
      setFileError(error)
      return
    }
    setFileError(null)
    uploadFileMutation.mutate(file)
  }

  const handleReplaceFiles = (files: FileList | null, image: ProductImage) => {
    const file = files?.[0]
    if (!file) return
    const error = validateImageFile(file)
    if (error) {
      setFileError(error)
      return
    }
    setFileError(null)
    replaceMutation.mutate({ image, file })
  }

  const columns = [
    {
      key: 'preview',
      label: t('products.images.preview'),
      render: (img: ProductImage) => (
        <img
          src={img.imageUrl}
          alt={img.altText ?? ''}
          className="h-12 w-12 rounded object-cover"
          onError={(e) => {
            ;(e.target as HTMLImageElement).style.visibility = 'hidden'
          }}
        />
      ),
    },
    { key: 'altText', label: t('products.images.altText'), render: (img: ProductImage) => img.altText ?? '—' },
    { key: 'sortOrder', label: t('products.images.sortOrder'), align: 'right' as const },
    {
      key: 'isPrimary',
      label: t('common.status'),
      render: (img: ProductImage) =>
        img.isPrimary ? <span className="badge bg-success-soft text-success-soft-text">{t('products.images.primary')}</span> : null,
    },
    {
      key: 'actions',
      label: '',
      render: (img: ProductImage) =>
        canEdit ? (
          <div className="flex gap-3">
            {!img.isPrimary && (
              <Button variant="link" className="text-xs" onClick={() => setPrimaryMutation.mutate(img.id)}>
                {t('products.images.setPrimary')}
              </Button>
            )}
            <Button
              variant="link"
              className="text-xs"
              disabled={replaceMutation.isPending && replaceTargetId === img.id}
              onClick={() => {
                setReplaceTargetId(img.id)
                replaceInputRef.current?.click()
              }}
            >
              {replaceMutation.isPending && replaceTargetId === img.id
                ? t('common.saving')
                : t('products.images.replace')}
            </Button>
            <Button variant="link-danger" className="text-xs" onClick={() => setRemoveTarget(img)}>
              {t('common.delete')}
            </Button>
          </div>
        ) : null,
    },
  ]

  const openFilePicker = () => fileInputRef.current?.click()

  return (
    <div className="space-y-4">
      <DataTable columns={columns} data={sortedImages} isLoading={isLoading} />

      {/* Hidden input reused by every row's "Replace" action, targeting replaceTargetId */}
      <input
        ref={replaceInputRef}
        type="file"
        accept={ALLOWED_IMAGE_TYPES.join(',')}
        className="hidden"
        aria-hidden="true"
        tabIndex={-1}
        onChange={(e) => {
          const target = sortedImages.find((i) => i.id === replaceTargetId)
          if (target) handleReplaceFiles(e.target.files, target)
          e.target.value = ''
        }}
      />

      {canEdit && (
        <div className="space-y-3">
          <div className="card grid grid-cols-1 gap-3 p-4 sm:grid-cols-3">
            <FormField label={t('products.images.altText')}>
              <Input value={pendingAltText} onChange={(e) => setPendingAltText(e.target.value)} />
            </FormField>
            <FormField label={t('products.images.variant')}>
              <Select value={pendingVariantId} onChange={(e) => setPendingVariantId(e.target.value)}>
                <option value="">{t('common.none')}</option>
                {(variants ?? []).map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.sku}
                  </option>
                ))}
              </Select>
            </FormField>
            <div className="flex items-end">
              <Checkbox
                label={t('products.images.setPrimary')}
                checked={pendingSetAsPrimary}
                onChange={(e) => setPendingSetAsPrimary(e.target.checked)}
              />
            </div>
          </div>

          <div
            role="button"
            tabIndex={0}
            aria-label={t('products.images.dragDropHint')}
            className={`card flex cursor-pointer flex-col items-center justify-center gap-2 border-2 border-dashed p-8 text-center transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-action ${
              isDragging ? 'border-brand-action bg-info-soft' : 'border-line-strong'
            }`}
            onClick={openFilePicker}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                openFilePicker()
              }
            }}
            onDragOver={(e) => {
              e.preventDefault()
              setIsDragging(true)
            }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={(e) => {
              e.preventDefault()
              setIsDragging(false)
              handleFiles(e.dataTransfer.files)
            }}
          >
            <input
              ref={fileInputRef}
              type="file"
              accept={ALLOWED_IMAGE_TYPES.join(',')}
              className="hidden"
              aria-hidden="true"
              tabIndex={-1}
              onClick={(e) => e.stopPropagation()}
              onChange={(e) => {
                handleFiles(e.target.files)
                e.target.value = ''
              }}
            />
            <p className="text-sm text-ink-secondary">{t('products.images.dragDropHint')}</p>
            {uploadFileMutation.isPending && <p className="text-xs text-ink-secondary">{t('common.saving')}</p>}
          </div>

          {fileError && <Alert variant="danger">{t(`products.images.${fileError}`)}</Alert>}
          {(uploadFileMutation.isError || replaceMutation.isError) && (
            <Alert variant="danger">
              {serverMessage(uploadFileMutation.error ?? replaceMutation.error, t('common.failedToSave'))}
            </Alert>
          )}

          <Button variant="link" className="text-xs" onClick={() => setShowUrlForm((v) => !v)}>
            {showUrlForm ? t('common.cancel') : t('products.images.orPasteUrl')}
          </Button>

          {showUrlForm && (
            <form
              onSubmit={handleSubmit((v) => uploadUrlMutation.mutate(v))}
              className="card grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-4"
            >
              <FormField
                className="sm:col-span-2 lg:col-span-2"
                label={t('products.images.imageUrl')}
                required
                error={errors.imageUrl ? t('products.images.imageUrlRequired') : undefined}
              >
                <Input {...register('imageUrl', { required: true })} placeholder="https://…" />
              </FormField>
              <FormField label={t('products.images.altText')}>
                <Input {...register('altText')} />
              </FormField>
              <FormField label={t('products.images.variant')}>
                <Select {...register('variantId')}>
                  <option value="">{t('common.none')}</option>
                  {(variants ?? []).map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.sku}
                    </option>
                  ))}
                </Select>
              </FormField>
              <div className="flex items-end gap-2">
                <Checkbox label={t('products.images.setPrimary')} {...register('setAsPrimary')} />
              </div>
              <div className="flex justify-end sm:col-span-2 lg:col-span-4">
                <Button type="submit" loading={uploadUrlMutation.isPending}>
                  {t('products.images.upload')}
                </Button>
              </div>
              {uploadUrlMutation.isError && (
                <Alert variant="danger" className="sm:col-span-2 lg:col-span-4">
                  {serverMessage(uploadUrlMutation.error, t('common.failedToSave'))}
                </Alert>
              )}
            </form>
          )}
        </div>
      )}

      <ConfirmDialog
        open={!!removeTarget}
        title={t('products.images.removeTitle')}
        message={t('products.images.removeConfirm')}
        confirmLabel={t('common.delete')}
        danger
        onConfirm={() => removeTarget && removeMutation.mutate(removeTarget.id)}
        onCancel={() => setRemoveTarget(null)}
      />
    </div>
  )
}
