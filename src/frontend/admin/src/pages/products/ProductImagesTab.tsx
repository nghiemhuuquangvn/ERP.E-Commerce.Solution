import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductImage, UploadProductImageRequest } from '@/api/products.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'

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

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['products', productId, 'images'] })

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

  return (
    <div className="space-y-4">
      <div className="overflow-x-auto rounded-lg border border-gray-200">
        <table className="min-w-full divide-y divide-gray-200 bg-white text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                {t('products.images.preview')}
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                {t('products.images.altText')}
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                {t('products.images.sortOrder')}
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500">
                {t('common.status')}
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold uppercase text-gray-500" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {isLoading ? (
              <tr>
                <td colSpan={5} className="py-8 text-center text-gray-400">
                  {t('common.loading')}
                </td>
              </tr>
            ) : sortedImages.length === 0 ? (
              <tr>
                <td colSpan={5} className="py-8 text-center text-gray-400">
                  {t('common.noRecordsFound')}
                </td>
              </tr>
            ) : (
              sortedImages.map((img) => (
                <tr key={img.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3">
                    <img
                      src={img.imageUrl}
                      alt={img.altText ?? ''}
                      className="h-12 w-12 rounded object-cover"
                      onError={(e) => {
                        ;(e.target as HTMLImageElement).style.visibility = 'hidden'
                      }}
                    />
                  </td>
                  <td className="px-4 py-3">{img.altText ?? '—'}</td>
                  <td className="px-4 py-3">{img.sortOrder}</td>
                  <td className="px-4 py-3">
                    {img.isPrimary && (
                      <span className="badge bg-green-100 text-green-700">{t('products.images.primary')}</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {canEdit && (
                      <div className="flex gap-3">
                        {!img.isPrimary && (
                          <button
                            className="text-xs text-primary-600 hover:underline"
                            onClick={() => setPrimaryMutation.mutate(img.id)}
                          >
                            {t('products.images.setPrimary')}
                          </button>
                        )}
                        <button
                          className="text-xs text-primary-600 hover:underline disabled:opacity-50"
                          disabled={replaceMutation.isPending && replaceTargetId === img.id}
                          onClick={() => {
                            setReplaceTargetId(img.id)
                            replaceInputRef.current?.click()
                          }}
                        >
                          {replaceMutation.isPending && replaceTargetId === img.id
                            ? t('common.saving')
                            : t('products.images.replace')}
                        </button>
                        <button
                          className="text-xs text-red-600 hover:underline"
                          onClick={() => setRemoveTarget(img)}
                        >
                          {t('common.delete')}
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Hidden input reused by every row's "Replace" action, targeting replaceTargetId */}
      <input
        ref={replaceInputRef}
        type="file"
        accept={ALLOWED_IMAGE_TYPES.join(',')}
        className="hidden"
        onChange={(e) => {
          const target = sortedImages.find((i) => i.id === replaceTargetId)
          if (target) handleReplaceFiles(e.target.files, target)
          e.target.value = ''
        }}
      />

      {canEdit && (
        <div className="space-y-3">
          <div className="card grid grid-cols-1 gap-3 p-4 sm:grid-cols-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.images.altText')}</label>
              <input
                className="input"
                value={pendingAltText}
                onChange={(e) => setPendingAltText(e.target.value)}
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.images.variant')}</label>
              <select
                className="input"
                value={pendingVariantId}
                onChange={(e) => setPendingVariantId(e.target.value)}
              >
                <option value="">{t('common.none')}</option>
                {(variants ?? []).map((v) => (
                  <option key={v.id} value={v.id}>
                    {v.sku}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex items-end">
              <label className="flex items-center gap-2 text-xs text-gray-700">
                <input
                  type="checkbox"
                  checked={pendingSetAsPrimary}
                  onChange={(e) => setPendingSetAsPrimary(e.target.checked)}
                />
                {t('products.images.setPrimary')}
              </label>
            </div>
          </div>

          <div
            className={`card flex cursor-pointer flex-col items-center justify-center gap-2 border-2 border-dashed p-8 text-center transition-colors ${
              isDragging ? 'border-primary-500 bg-primary-50' : 'border-gray-300'
            }`}
            onClick={() => fileInputRef.current?.click()}
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
              onChange={(e) => {
                handleFiles(e.target.files)
                e.target.value = ''
              }}
            />
            <p className="text-sm text-gray-600">{t('products.images.dragDropHint')}</p>
            {uploadFileMutation.isPending && <p className="text-xs text-gray-400">{t('common.saving')}</p>}
          </div>

          {fileError && <p className="text-xs text-red-600">{t(`products.images.${fileError}`)}</p>}
          {(uploadFileMutation.isError || replaceMutation.isError) && (
            <p className="text-xs text-red-600">{t('common.failedToSave')}</p>
          )}

          <button
            type="button"
            className="text-xs text-gray-500 hover:text-gray-700 hover:underline"
            onClick={() => setShowUrlForm((v) => !v)}
          >
            {showUrlForm ? t('common.cancel') : t('products.images.orPasteUrl')}
          </button>

          {showUrlForm && (
            <form
              onSubmit={handleSubmit((v) => uploadUrlMutation.mutate(v))}
              className="card grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-4"
            >
              <div className="sm:col-span-2 lg:col-span-2">
                <label className="mb-1 block text-xs font-medium text-gray-700">
                  {t('products.images.imageUrl')}
                </label>
                <input className="input" {...register('imageUrl', { required: true })} placeholder="https://…" />
                {errors.imageUrl && (
                  <p className="mt-1 text-xs text-red-600">{t('products.images.imageUrlRequired')}</p>
                )}
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-700">
                  {t('products.images.altText')}
                </label>
                <input className="input" {...register('altText')} />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-700">
                  {t('products.images.variant')}
                </label>
                <select className="input" {...register('variantId')}>
                  <option value="">{t('common.none')}</option>
                  {(variants ?? []).map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.sku}
                    </option>
                  ))}
                </select>
              </div>
              <div className="flex items-end gap-2">
                <label className="flex items-center gap-2 text-xs text-gray-700">
                  <input type="checkbox" {...register('setAsPrimary')} />
                  {t('products.images.setPrimary')}
                </label>
              </div>
              <div className="sm:col-span-2 lg:col-span-4 flex justify-end">
                <button type="submit" disabled={uploadUrlMutation.isPending} className="btn-primary">
                  {uploadUrlMutation.isPending ? t('common.saving') : t('products.images.upload')}
                </button>
              </div>
              {uploadUrlMutation.isError && (
                <p className="sm:col-span-2 lg:col-span-4 text-xs text-red-600">{t('common.failedToSave')}</p>
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
