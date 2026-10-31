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
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.unitsOfMeasure.editTitle') : t('masterdata.unitsOfMeasure.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="unit-of-measure-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="unit-of-measure-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
            <Input {...register('code', { required: true })} />
          </FormField>
        )}
        <FormField label={t('masterdata.unitsOfMeasure.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.unitsOfMeasure.symbol')} required error={errors.symbol ? t('common.required') : undefined}>
          <Input {...register('symbol', { required: true })} />
        </FormField>
        {!isEdit && (
          <FormField label={t('masterdata.unitsOfMeasure.unitType')}>
            <Select {...register('unitType')}>
              <option value={UNIT_TYPE.Weight}>{t('masterdata.unitsOfMeasure.unitTypes.weight')}</option>
              <option value={UNIT_TYPE.Volume}>{t('masterdata.unitsOfMeasure.unitTypes.volume')}</option>
              <option value={UNIT_TYPE.Length}>{t('masterdata.unitsOfMeasure.unitTypes.length')}</option>
              <option value={UNIT_TYPE.Count}>{t('masterdata.unitsOfMeasure.unitTypes.count')}</option>
              <option value={UNIT_TYPE.Area}>{t('masterdata.unitsOfMeasure.unitTypes.area')}</option>
            </Select>
          </FormField>
        )}
        <FormField label={t('masterdata.unitsOfMeasure.conversionFactor')} required error={errors.conversionFactor ? t('common.required') : undefined}>
          <Input
            type="number"
            step="any"
            {...register('conversionFactor', { required: true })}
          />
        </FormField>
        {!isEdit && (
          <FormField label={t('masterdata.unitsOfMeasure.baseUnitIdOptional')}>
            <Input {...register('baseUnitId')} />
          </FormField>
        )}
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.unitsOfMeasure.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
            <Button variant="link" className="text-xs"
              onClick={() => setModalState({ open: true, uom: u })}>
              {t('common.edit')}
            </Button>
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
          <Button onClick={() => setModalState({ open: true, uom: null })}>
            {t('masterdata.unitsOfMeasure.add')}
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

      {modalState.open && (
        <UnitOfMeasureModal
          uom={modalState.uom}
          onClose={() => setModalState({ open: false, uom: null })}
        />
      )}
    </div>
  )
}
