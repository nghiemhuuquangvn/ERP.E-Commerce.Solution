import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { categoriesApi } from '@/api/product/categories.api'
import type { Category } from '@/api/product/categories.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'

interface CategoryFormValues {
  name: string
  code: string
  parentCategoryId: string
}

function CategoryModal({
  category,
  categories,
  onClose,
}: {
  category: Category | null
  categories: Category[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!category

  // Exclude self from the parent picker — the backend doesn't validate against
  // self-parenting, so this is enforced client-side only.
  const parentOptions = categories.filter((c) => c.id !== category?.id)

  const { register, handleSubmit, formState: { errors } } = useForm<CategoryFormValues>({
    defaultValues: {
      name: category?.name ?? '',
      code: category?.code ?? '',
      parentCategoryId: category?.parentCategoryId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: async (values: CategoryFormValues) => {
      const code = values.code.trim() || undefined
      const parentCategoryId = values.parentCategoryId.trim() || undefined
      if (isEdit) {
        // Note: UpdateCategoryCommand only applies ParentCategoryId when it has a
        // value — there is no way to un-parent a category (move it back to root)
        // through this API today.
        await categoriesApi.update(category!.id, { name: values.name, code, parentCategoryId })
      } else {
        await categoriesApi.create({ name: values.name, code, parentCategoryId })
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'categories'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('products.categories.editTitle') : t('products.categories.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code')} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('products.categories.parentCategory')}
            </label>
            <select className="input" {...register('parentCategoryId')}>
              <option value="">{t('common.none')}</option>
              {parentOptions.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
            {isEdit && (
              <p className="mt-1 text-xs text-gray-400">{t('products.categories.parentCannotClear')}</p>
            )}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('products.categories.failedToSave')}</p>
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

export function CategoriesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const [modalState, setModalState] = useState<{ open: boolean; category: Category | null }>({
    open: false,
    category: null,
  })

  // No dedicated "list all categories" endpoint exists server-side (GetCategoriesQuery
  // only returns root-level or a single parent's direct children) — categoriesApi.listAll()
  // walks the tree level-by-level client-side to build a flat list for this page.
  const { data, isLoading } = useQuery({
    queryKey: ['product', 'categories'],
    queryFn: () => categoriesApi.listAll(),
    staleTime: 30_000,
  })

  const categories = data ?? []
  const canManage = hasPermission('products:edit')

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs', render: (c: Category) => c.code ?? '—' },
      { key: 'name', label: t('common.name') },
      {
        key: 'parentCategoryId',
        label: t('products.categories.parentCategory'),
        render: (c: Category) => categories.find((p) => p.id === c.parentCategoryId)?.name ?? '—',
      },
      {
        // Read-only: no Create/Update command exposes SortOrder today, so it can
        // only ever be its default value (0) via this API.
        key: 'sortOrder',
        label: t('products.categories.sortOrder'),
      },
      {
        key: 'actions',
        label: '',
        render: (c: Category) =>
          canManage ? (
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setModalState({ open: true, category: c })}
            >
              {t('common.edit')}
            </button>
          ) : null,
      },
    ],
    [canManage, categories, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('products.categories.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, category: null })}>
            {t('products.categories.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={categories} isLoading={isLoading} />

      {modalState.open && (
        <CategoryModal
          category={modalState.category}
          categories={categories}
          onClose={() => setModalState({ open: false, category: null })}
        />
      )}
    </div>
  )
}
