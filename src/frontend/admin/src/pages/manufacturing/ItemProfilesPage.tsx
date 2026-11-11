import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type ItemProfileListItem, type ItemProfileValues } from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ProductPickerModal } from '@/components/shared/ProductPickerModal'
import { NoPlant, PageHeader, PlantSelector, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Button, Checkbox, FormField, Input, Modal, Select, Skeleton, useToast } from '@/components/ui'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const num = (v: unknown) => (v === '' || v == null ? null : Number(v))

function ProfileModal({ plantId, itemId, onClose }: { plantId: string; itemId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const { data, isLoading } = useQuery({ queryKey: ['mfg', 'profile', plantId, itemId], queryFn: () => mfgApi.getProfile(itemId, plantId) })
  const { register, handleSubmit, watch, formState: { errors } } = useForm<ItemProfileValues>({ values: data?.values })
  const policy = watch('lotSizingPolicy')
  const fqc = watch('requiresFqc')
  // P4-3: the FQC plans (Approved) of this item; the profile points at the series
  const { data: fqcPlans = [] } = useQuery({ queryKey: ['mfg', 'inspection-plans', plantId, 'Fqc', itemId], queryFn: () => mfgApi.listInspectionPlans(plantId, { type: 'Fqc', itemId, status: 'Approved' }), enabled: !!fqc })

  const save = useMutation({
    mutationFn: (v: ItemProfileValues) => mfgApi.saveProfile(itemId, plantId, {
      ...v,
      fixedLeadTimeDays: Number(v.fixedLeadTimeDays), purchaseLeadTimeDays: Number(v.purchaseLeadTimeDays), safetyStockQty: Number(v.safetyStockQty), standardLotSize: Number(v.standardLotSize), shelfLifeDays: Number(v.shelfLifeDays),
      fixedLotQty: num(v.fixedLotQty), minOrderQty: num(v.minOrderQty), orderMultiple: num(v.orderMultiple), periodDays: num(v.periodDays),
      defaultIssueMethod: v.defaultIssueMethod || null, fqcInspectionPlanId: v.requiresFqc ? v.fqcInspectionPlanId || null : null,
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'profiles'] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const req = (field: keyof ItemProfileValues) => (errors[field] ? t('common.required') : undefined)

  return (
    <Modal
      open
      size="lg"
      title={data ? `${data.itemCode || data.itemId.slice(0, 8)} ${data.itemName}` : t('mfg.profiles.title')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="profile-form" loading={save.isPending} disabled={!data}>{t('common.save')}</Button></>}
    >
      {isLoading || !data ? <Skeleton className="h-48 w-full" /> : (
        <form id="profile-form" onSubmit={handleSubmit((v) => save.mutate(v))} className="space-y-4">
          {error && <Alert variant="danger">{error}</Alert>}
          {data.isDefault && <Alert variant="info">{t('mfg.profiles.defaults')}</Alert>}
          <div className="grid gap-4 sm:grid-cols-3">
            <FormField label={t('mfg.profiles.makeOrBuy')}>
              <Select {...register('makeOrBuy')}>{['Make', 'Buy', 'Both'].map((m) => <option key={m} value={m}>{t(`mfg.profiles.${m}`)}</option>)}</Select>
            </FormField>
            <FormField label={t('mfg.profiles.fixedLeadTimeDays')} error={req('fixedLeadTimeDays')}><Input type="number" min={0} {...register('fixedLeadTimeDays', { required: true, min: 0 })} /></FormField>
            <FormField label={t('mfg.profiles.purchaseLeadTimeDays')} error={req('purchaseLeadTimeDays')}><Input type="number" min={0} {...register('purchaseLeadTimeDays', { required: true, min: 0 })} /></FormField>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <FormField label={t('mfg.profiles.lotSizingPolicy')} className="sm:col-span-3">
              <Select {...register('lotSizingPolicy')}>{['LotForLot', 'FixedQty', 'MinMultiple', 'PeriodOrder'].map((p) => <option key={p} value={p}>{t(`mfg.profiles.${p}`)}</option>)}</Select>
            </FormField>
            {policy === 'FixedQty' && <FormField label={t('mfg.profiles.fixedLotQty')} required error={req('fixedLotQty')}><Input type="number" step="any" min={0} {...register('fixedLotQty', { required: true })} /></FormField>}
            {policy === 'MinMultiple' && (
              <>
                <FormField label={t('mfg.profiles.minOrderQty')} required error={req('minOrderQty')}><Input type="number" step="any" min={0} {...register('minOrderQty', { required: true })} /></FormField>
                <FormField label={t('mfg.profiles.orderMultiple')} required error={req('orderMultiple')}><Input type="number" step="any" min={0} {...register('orderMultiple', { required: true })} /></FormField>
              </>
            )}
            {policy === 'PeriodOrder' && <FormField label={t('mfg.profiles.periodDays')} hint={t('mfg.profiles.periodHint')} required error={req('periodDays')}><Input type="number" min={1} max={90} {...register('periodDays', { required: true })} /></FormField>}
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <FormField label={t('mfg.profiles.safetyStockQty')} error={req('safetyStockQty')}><Input type="number" step="any" min={0} {...register('safetyStockQty', { required: true, min: 0 })} /></FormField>
            <FormField label={t('mfg.profiles.standardLotSize')} error={req('standardLotSize')}><Input type="number" step="any" min={0} {...register('standardLotSize', { required: true, min: 0.000001 })} /></FormField>
            <FormField label={t('mfg.profiles.shelfLifeDays')} error={req('shelfLifeDays')}><Input type="number" min={0} {...register('shelfLifeDays', { required: true, min: 0 })} /></FormField>
            <FormField label={t('mfg.profiles.defaultIssueMethod')}>
              <Select {...register('defaultIssueMethod')}><option value="">{t('mfg.profiles.plantDefault')}</option><option value="Manual">{t('mfg.issue.Manual')}</option><option value="Backflush">{t('mfg.issue.Backflush')}</option></Select>
            </FormField>
            <div className="flex items-end"><Checkbox label={t('mfg.profiles.requiresFqc')} {...register('requiresFqc')} /></div>
          </div>
          {fqc && (
            <FormField label={t('mfg.qc.profile.fqcPlan')} hint={t('mfg.qc.profile.fqcPlanHint')}>
              <Select {...register('fqcInspectionPlanId')}>
                <option value="">{t('common.none')}</option>
                {data?.values.fqcInspectionPlanId && !fqcPlans.some((p) => p.id === data.values.fqcInspectionPlanId) && <option value={data.values.fqcInspectionPlanId}>{t('mfg.qc.routing.currentPlan')}</option>}
                {fqcPlans.map((p) => <option key={p.id} value={p.id}>{p.code} (v{p.version})</option>)}
              </Select>
            </FormField>
          )}
          {fqc && !watch('fqcInspectionPlanId') && <Alert variant="warning">{t('mfg.qc.profile.noPlanWarning')}</Alert>}
          <dl className="grid grid-cols-2 gap-2 rounded-md bg-surface-muted p-3 text-sm">
            <dt className="text-ink-secondary">{t('mfg.profiles.lowLevelCode')}</dt><dd className="font-mono">{data.lowLevelCode}</dd>
            <dt className="text-ink-secondary">{t('mfg.profiles.standardCost')}</dt><dd className="font-mono">{data.standardCost}</dd>
          </dl>
        </form>
      )}
    </Modal>
  )
}

/** S03: how each item is planned in the plant (BA §5.3). */
export function ItemProfilesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:item-profile:manage')
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [picker, setPicker] = useState(false)
  const [itemId, setItemId] = useState<string | null>(null)
  const { data, isLoading } = useQuery({ queryKey: ['mfg', 'profiles', plantId, search, page], queryFn: () => mfgApi.listProfiles(plantId!, search, page, pageSize), enabled: !!plantId })

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.itemProfiles')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader
        title={t('mfg.nav.itemProfiles')}
        hint={t('mfg.profiles.hint')}
        actions={<><PlantSelector />{canManage && <Button onClick={() => setPicker(true)}>{t('mfg.profiles.add')}</Button>}</>}
      />
      <div className="mb-3 max-w-sm"><Input type="search" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} /></div>
      <DataTable
        data={(data?.items ?? []).map((p) => ({ id: p.itemId, ...p }))}
        isLoading={isLoading}
        emptyMessage={t('mfg.profiles.empty')}
        columns={[
          { key: 'itemCode', label: t('common.code'), render: (p: ItemProfileListItem) => <span className="font-mono text-xs">{p.itemCode}</span> },
          { key: 'itemName', label: t('common.name') },
          { key: 'makeOrBuy', label: t('mfg.profiles.makeOrBuy'), render: (p: ItemProfileListItem) => t(`mfg.profiles.${p.makeOrBuy}`) },
          { key: 'lotSizingPolicy', label: t('mfg.profiles.lotSizingPolicy'), render: (p: ItemProfileListItem) => t(`mfg.profiles.${p.lotSizingPolicy}`) },
          { key: 'lowLevelCode', label: t('mfg.profiles.lowLevelCode'), align: 'right' as const },
          { key: 'actions', label: t('common.actions'), render: (p: ItemProfileListItem) => <Button variant="link" size="sm" onClick={() => setItemId(p.itemId)}>{canManage ? t('common.edit') : t('common.view')}</Button> },
        ]}
      />
      {data && <Pagination page={page} pageSize={pageSize} totalCount={data.total} onPageChange={setPage} />}
      <ProductPickerModal open={picker} multiple={false} onClose={() => setPicker(false)} onConfirm={(products) => { setPicker(false); if (products[0]) setItemId(products[0].id) }} />
      {itemId && plantId && <ProfileModal plantId={plantId} itemId={itemId} onClose={() => setItemId(null)} />}
    </div>
  )
}
