import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { brandsApi } from '@/api/product/brands.api'
import type { Brand } from '@/api/product/brands.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'

interface BrandFormValues {
  code: string
  name: string
  logoUrl: string
}

function BrandModal({ brand, onClose }: { brand: Brand | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!brand

  const { register, handleSubmit, formState: { errors } } = useForm<BrandFormValues>({
    defaultValues: {
      code: brand?.code ?? '',
      name: brand?.name ?? '',
      logoUrl: brand?.logoUrl ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: async (values: BrandFormValues) => {
      const logoUrl = values.logoUrl.trim() || undefined
      if (isEdit) {
        await brandsApi.update(brand!.id, { name: values.name, logoUrl })
      } else {
        await brandsApi.create({ code: values.code, name: values.name, logoUrl })
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'brands'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('products.brands.editTitle') : t('products.brands.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="brand-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="brand-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.code')} required={!isEdit} error={errors.code ? t('common.required') : undefined}>
          <Input disabled={isEdit} {...register('code', { required: !isEdit })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('products.brands.logoUrl')}>
          <Input {...register('logoUrl')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('products.brands.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function BrandsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all')
  const [modalState, setModalState] = useState<{ open: boolean; brand: Brand | null }>({
    open: false,
    brand: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Brand | null>(null)

  // GetBrandsQuery has no pagination — it always returns the full matching list.
  const isActiveParam = activeFilter === 'all' ? undefined : activeFilter === 'active'
  const { data, isLoading } = useQuery({
    queryKey: ['product', 'brands', { isActiveParam }],
    queryFn: () => brandsApi.list({ isActive: isActiveParam }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('product:brands:manage')

  const toggleMutation = useMutation({
    mutationFn: (b: Brand) => (b.isActive ? brandsApi.deactivate(b.id) : brandsApi.activate(b.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'brands'] })
      toast.success(t('common.saved'))
      setConfirmTarget(null)
    },
    onError: (e) => {
      setConfirmTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'logoUrl',
        label: t('products.brands.logoUrl'),
        render: (b: Brand) => b.logoUrl ?? '—',
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (b: Brand) => (
          <StatusBadge
            status={b.isActive ? 'Active' : 'Inactive'}
            label={b.isActive ? t('products.brands.active') : t('products.brands.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (b: Brand) =>
          canManage ? (
            <div className="flex gap-3">
              <Button variant="link" className="text-xs"
                onClick={() => setModalState({ open: true, brand: b })}>
                {t('common.edit')}
              </Button>
              {b.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(b)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(b)}>
                  {t('common.activate')}
                </Button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('products.brands.title')}</h1>
        {canManage && (
          <Button onClick={() => setModalState({ open: true, brand: null })}>
            {t('products.brands.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('common.status')}
        value={activeFilter}
        onChange={(e) => setActiveFilter(e.target.value as 'all' | 'active' | 'inactive')}
      >
        <option value="all">{t('common.all')}</option>
        <option value="active">{t('products.brands.active')}</option>
        <option value="inactive">{t('products.brands.inactive')}</option>
      </Select>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          activeFilter !== 'all' ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => setActiveFilter('all')}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />

      {modalState.open && (
        <BrandModal brand={modalState.brand} onClose={() => setModalState({ open: false, brand: null })} />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('products.brands.deactivateTitle')}
        message={t('products.brands.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
