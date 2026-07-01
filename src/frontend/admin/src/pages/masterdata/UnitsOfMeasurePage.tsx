import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { unitsOfMeasureApi, UNIT_TYPE } from '@/api/masterdata/unitsOfMeasure.api'
import type { UnitOfMeasure } from '@/api/masterdata/unitsOfMeasure.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

interface UomFormValues {
  code: string
  name: string
  symbol: string
  unitType: string
  conversionFactor: string
  baseUnitId: string
}

function UnitOfMeasureModal({
  uom,
  onClose,
}: {
  uom: UnitOfMeasure | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!uom
  const { register, handleSubmit, formState: { errors } } = useForm<UomFormValues>({
    defaultValues: {
      code: uom?.code ?? '',
      name: uom?.name ?? '',
      symbol: uom?.symbol ?? '',
      unitType: String(UNIT_TYPE.Count),
      conversionFactor: uom ? String(uom.conversionFactor) : '1',
      baseUnitId: uom?.baseUnitId ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: UomFormValues) => {
      if (isEdit) {
        return unitsOfMeasureApi.update(uom!.id, {
          name: values.name,
          symbol: values.symbol,
          conversionFactor: Number(values.conversionFactor),
        })
      }
      return unitsOfMeasureApi.create({
        code: values.code,
        name: values.name,
        symbol: values.symbol,
        unitType: Number(values.unitType),
        baseUnitId: values.baseUnitId.trim() || null,
        conversionFactor: Number(values.conversionFactor),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'units-of-measure'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.unitsOfMeasure.editTitle') : t('masterdata.unitsOfMeasure.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
              <input className="input" {...register('code', { required: true })} />
              {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.unitsOfMeasure.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.unitsOfMeasure.symbol')}</label>
            <input className="input" {...register('symbol', { required: true })} />
            {errors.symbol && <p className="mt-1 text-xs text-red-600">{t('masterdata.unitsOfMeasure.symbol')} is required.</p>}
          </div>
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.unitsOfMeasure.unitType')}</label>
              <select className="input" {...register('unitType')}>
                <option value={UNIT_TYPE.Weight}>{t('masterdata.unitsOfMeasure.unitTypes.weight')}</option>
                <option value={UNIT_TYPE.Volume}>{t('masterdata.unitsOfMeasure.unitTypes.volume')}</option>
                <option value={UNIT_TYPE.Length}>{t('masterdata.unitsOfMeasure.unitTypes.length')}</option>
                <option value={UNIT_TYPE.Count}>{t('masterdata.unitsOfMeasure.unitTypes.count')}</option>
                <option value={UNIT_TYPE.Area}>{t('masterdata.unitsOfMeasure.unitTypes.area')}</option>
              </select>
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.unitsOfMeasure.conversionFactor')}
            </label>
            <input
              type="number"
              step="any"
              className="input"
              {...register('conversionFactor', { required: true })}
            />
          </div>
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('masterdata.unitsOfMeasure.baseUnitIdOptional')}
              </label>
              <input className="input" {...register('baseUnitId')} />
            </div>
          )}
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.unitsOfMeasure.failedToSave')}</p>
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

export function UnitsOfMeasurePage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [modalState, setModalState] = useState<{ open: boolean; uom: UnitOfMeasure | null }>({
    open: false,
    uom: null,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['masterdata', 'units-of-measure', { page, pageSize }],
    queryFn: () => unitsOfMeasureApi.list({ page, pageSize }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('masterdata:units-of-measure:manage')

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'symbol', label: t('masterdata.unitsOfMeasure.symbol') },
      { key: 'unitType', label: t('masterdata.unitsOfMeasure.unitType') },
      { key: 'conversionFactor', label: t('masterdata.unitsOfMeasure.conversionFactor'), className: 'text-right' },
      {
        key: 'actions',
        label: '',
        render: (u: UnitOfMeasure) =>
          canManage ? (
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setModalState({ open: true, uom: u })}
            >
              {t('common.edit')}
            </button>
          ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.unitsOfMeasure.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, uom: null })}>
            {t('masterdata.unitsOfMeasure.add')}
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

      {modalState.open && (
        <UnitOfMeasureModal
          uom={modalState.uom}
          onClose={() => setModalState({ open: false, uom: null })}
        />
      )}
    </div>
  )
}
