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
import { Alert, Button, Checkbox, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<LanguageFormValues>({
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.languages.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-language-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="create-language-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input placeholder="vi-VN" {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.languages.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input placeholder="Vietnamese" {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.languages.nativeName')} required error={errors.nativeName ? t('common.required') : undefined}>
          <Input
            placeholder="Tiếng Việt"
            {...register('nativeName', { required: true })}
          />
        </FormField>
        <FormField label={t('masterdata.languages.sortOrder')}>
          <Input type="number" {...register('sortOrder')} />
        </FormField>
        <Checkbox label={t('masterdata.languages.defaultLanguage')} {...register('isDefault')} />
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.languages.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
          <Button onClick={() => setModalOpen(true)}>
            {t('masterdata.languages.add')}
          </Button>
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
