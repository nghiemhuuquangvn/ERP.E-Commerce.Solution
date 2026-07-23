import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { ProductImage, UploadProductImageRequest } from '@/api/products.api'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePermission } from '@/hooks/usePermission'

interface ImageFormValues {
  imageUrl: string
  altText: string
  variantId: string
  setAsPrimary: boolean
}

export function ProductImagesTab({ productId }: { productId: string }) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [removeTarget, setRemoveTarget] = useState<ProductImage | null>(null)
  const canEdit = hasPermission('products:edit')

  const { data: images, isLoading } = useQuery({
    queryKey: ['products', productId, 'images'],
    queryFn: () => productsApi.listImages(productId),
  })

  const { data: variants } = useQuery({
    queryKey: ['products', productId, 'variants'],
    queryFn: () => productsApi.listVariants(productId),
  })

  const { register, handleSubmit, reset, formState: { errors } } = useForm<ImageFormValues>({
    defaultValues: { imageUrl: '', altText: '', variantId: '', setAsPrimary: false },
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['products', productId, 'images'] })

  const uploadMutation = useMutation({
    mutationFn: (values: ImageFormValues) => {
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

      {canEdit && (
        <form
          onSubmit={handleSubmit((v) => uploadMutation.mutate(v))}
          className="card grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 lg:grid-cols-4"
        >
          <div className="sm:col-span-2 lg:col-span-2">
            <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.images.imageUrl')}</label>
            <input className="input" {...register('imageUrl', { required: true })} placeholder="https://…" />
            {errors.imageUrl && <p className="mt-1 text-xs text-red-600">{t('products.images.imageUrlRequired')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.images.altText')}</label>
            <input className="input" {...register('altText')} />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-gray-700">{t('products.images.variant')}</label>
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
            <button type="submit" disabled={uploadMutation.isPending} className="btn-primary">
              {uploadMutation.isPending ? t('common.saving') : t('products.images.upload')}
            </button>
          </div>
          {uploadMutation.isError && (
            <p className="sm:col-span-2 lg:col-span-4 text-xs text-red-600">{t('common.failedToSave')}</p>
          )}
        </form>
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
