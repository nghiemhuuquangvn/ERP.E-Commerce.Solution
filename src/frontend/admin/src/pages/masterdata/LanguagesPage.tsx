import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { languagesApi, type SupportedLanguage } from '@/api/masterdata/languages.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface LanguageFormValues {
  code: string
  name: string
  nativeName: string
  isDefault: boolean
  sortOrder: number
}

function CreateLanguageModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<LanguageFormValues>({
    defaultValues: { isDefault: false, sortOrder: 0 },
  })

  const mutation = useMutation({
    mutationFn: (v: LanguageFormValues) =>
      languagesApi.create({
        code: v.code,
        name: v.name,
        nativeName: v.nativeName,
        isDefault: !!v.isDefault,
        sortOrder: Number(v.sortOrder),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'languages'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('masterdata.languages.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" placeholder="vi-VN" {...register('code', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.languages.name')}</label>
            <input className="input" placeholder="Vietnamese" {...register('name', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.languages.nativeName')}</label>
            <input
              className="input"
              placeholder="Tiếng Việt"
              {...register('nativeName', { required: true })}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.languages.sortOrder')}</label>
            <input className="input" type="number" {...register('sortOrder')} />
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input type="checkbox" {...register('isDefault')} />
            {t('masterdata.languages.defaultLanguage')}
          </label>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.languages.failedToSave')}</p>
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

export function LanguagesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('masterdata:languages:manage')
  const { page, pageSize, setPage } = usePagination()
  const [modalOpen, setModalOpen] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'languages', { page, pageSize }],
    queryFn: () => languagesApi.list({ page, pageSize }),
    staleTime: 60_000,
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('masterdata.languages.name') },
      { key: 'nativeName', label: t('masterdata.languages.nativeName') },
      { key: 'sortOrder', label: t('masterdata.languages.sortOrder'), className: 'text-right' },
      {
        key: 'isDefault',
        label: t('masterdata.languages.default'),
        render: (l: SupportedLanguage) =>
          l.isDefault ? <StatusBadge status="Active" label={t('masterdata.languages.default')} /> : null,
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (l: SupportedLanguage) => (
          <StatusBadge
            status={l.isActive ? 'Active' : 'Discontinued'}
            label={l.isActive ? t('masterdata.positions.active') : t('masterdata.positions.inactive')}
          />
        ),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.languages.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {t('masterdata.languages.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      {modalOpen && <CreateLanguageModal onClose={() => setModalOpen(false)} />}
    </div>
  )
}
