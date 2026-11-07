import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Plant, type PlantBody } from '@/api/manufacturing/mfg.api'
import { warehousesApi } from '@/api/warehouses.api'
import { usersApi } from '@/api/users.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { TabList } from '@/components/shared/PageTabs'
import { ActiveBadge, PageHeader } from '@/components/manufacturing/shared'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

interface PlantForm {
  code: string
  name: string
  timeZoneId: string
  rawMaterialWarehouseId: string
  finishedGoodsWarehouseId: string
  quarantineWarehouseId: string
  scrapWarehouseId: string
  defaultCalendarId: string
}

function PlantModal({ plant, onClose }: { plant: Plant | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const isEdit = !!plant
  const [error, setError] = useState<string | null>(null)
  const { data: warehouses = [] } = useQuery({ queryKey: ['warehouses'], queryFn: () => warehousesApi.list() })
  const { data: calendars = [] } = useQuery({ queryKey: ['mfg', 'calendars', plant?.id], queryFn: () => mfgApi.listCalendars(plant!.id), enabled: isEdit })
  const { register, handleSubmit, formState: { errors } } = useForm<PlantForm>({
    defaultValues: {
      code: plant?.code ?? '', name: plant?.name ?? '', timeZoneId: plant?.timeZoneId ?? 'Asia/Ho_Chi_Minh',
      rawMaterialWarehouseId: plant?.rawMaterialWarehouseId ?? '', finishedGoodsWarehouseId: plant?.finishedGoodsWarehouseId ?? '',
      quarantineWarehouseId: plant?.quarantineWarehouseId ?? '', scrapWarehouseId: plant?.scrapWarehouseId ?? '', defaultCalendarId: plant?.defaultCalendarId ?? '',
    },
  })

  const save = useMutation({
    mutationFn: (v: PlantForm) => {
      const body: PlantBody = {
        name: v.name, timeZoneId: v.timeZoneId || null, rawMaterialWarehouseId: v.rawMaterialWarehouseId, finishedGoodsWarehouseId: v.finishedGoodsWarehouseId,
        quarantineWarehouseId: v.quarantineWarehouseId, scrapWarehouseId: v.scrapWarehouseId || null, defaultCalendarId: v.defaultCalendarId || null,
      }
      return isEdit ? mfgApi.updatePlant(plant!.id, body) : mfgApi.createPlant({ ...body, code: v.code })
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'plants'] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  const warehouseOptions = (
    <>
      <option value="">{t('common.select')}</option>
      {warehouses.map((w) => <option key={w.id} value={w.id}>{w.code} - {w.name}</option>)}
    </>
  )

  return (
    <Modal
      open
      size="md"
      title={isEdit ? t('mfg.plants.editTitle') : t('mfg.plants.addTitle')}
      onClose={onClose}
      closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="plant-form" loading={save.isPending}>{t('common.save')}</Button></>}
    >
      <form id="plant-form" onSubmit={handleSubmit((v) => save.mutate(v))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <div className="grid grid-cols-2 gap-4">
          {!isEdit && (
            <FormField label={t('common.code')} hint={t('mfg.plants.codeHint')} required error={errors.code ? t('common.required') : undefined}>
              <Input {...register('code', { required: true })} />
            </FormField>
          )}
          <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined} className={isEdit ? 'col-span-2' : undefined}>
            <Input {...register('name', { required: true })} />
          </FormField>
        </div>
        <FormField label={t('mfg.plants.timeZone')}><Input {...register('timeZoneId')} /></FormField>
        <div className="grid grid-cols-2 gap-4">
          <FormField label={t('mfg.plants.rawWarehouse')} required error={errors.rawMaterialWarehouseId ? t('common.required') : undefined}>
            <Select {...register('rawMaterialWarehouseId', { required: true })}>{warehouseOptions}</Select>
          </FormField>
          <FormField label={t('mfg.plants.fgWarehouse')} required error={errors.finishedGoodsWarehouseId ? t('common.required') : undefined}>
            <Select {...register('finishedGoodsWarehouseId', { required: true })}>{warehouseOptions}</Select>
          </FormField>
          <FormField label={t('mfg.plants.qcWarehouse')} required error={errors.quarantineWarehouseId ? t('common.required') : undefined}>
            <Select {...register('quarantineWarehouseId', { required: true })}>{warehouseOptions}</Select>
          </FormField>
          <FormField label={t('mfg.plants.scrapWarehouse')}>
            <Select {...register('scrapWarehouseId')}>{warehouseOptions}</Select>
          </FormField>
        </div>
        {isEdit && (
          <FormField label={t('mfg.plants.defaultCalendar')} hint={t('mfg.plants.defaultCalendarHint')}>
            <Select {...register('defaultCalendarId')}>
              <option value="">{t('common.none')}</option>
              {calendars.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.code} - {c.name}</option>)}
            </Select>
          </FormField>
        )}
      </form>
    </Modal>
  )
}

/** Who may work in a plant besides the holders of settings:manage (BR-GEN-09). */
function AccessPanel({ plants }: { plants: Plant[] }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:settings:manage')
  const [plantId, setPlantId] = useState(plants[0]?.id ?? '')
  const [userId, setUserId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const key = ['mfg', 'access', plantId]
  const { data: grants = [] } = useQuery({ queryKey: key, queryFn: () => mfgApi.listAccess(plantId), enabled: !!plantId })
  const { data: users } = useQuery({ queryKey: ['users', 'for-plant-access'], queryFn: () => usersApi.list({ page: 1, pageSize: 100 }), retry: false })
  const people = users?.items ?? []
  const nameOf = (id: string) => people.find((u) => u.id === id)?.fullName || people.find((u) => u.id === id)?.email || id

  const grant = useMutation({
    mutationFn: () => mfgApi.grantAccess(plantId, userId),
    onSuccess: () => { setUserId(''); setError(null); queryClient.invalidateQueries({ queryKey: key }) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const revoke = useMutation({
    mutationFn: (id: string) => mfgApi.revokeAccess(plantId, id),
    onSuccess: () => { toast.success(t('common.saved')); queryClient.invalidateQueries({ queryKey: key }) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  if (plants.length === 0) return <EmptyState title={t('mfg.noPlantTitle')} />
  return (
    <div className="space-y-4">
      <p className="text-sm text-ink-secondary">{t('mfg.plants.accessHint')}</p>
      {error && <Alert variant="danger">{error}</Alert>}
      <div className="flex flex-wrap items-end gap-3">
        <FormField label={t('mfg.plant')}>
          <Select className="w-64" value={plantId} onChange={(e) => setPlantId(e.target.value)}>
            {plants.map((p) => <option key={p.id} value={p.id}>{p.code} - {p.name}</option>)}
          </Select>
        </FormField>
        {canManage && (
          <>
            <FormField label={t('mfg.plants.user')}>
              <Select className="w-72" value={userId} onChange={(e) => setUserId(e.target.value)}>
                <option value="">{t('common.select')}</option>
                {people.filter((u) => !grants.some((g) => g.userId === u.id)).map((u) => <option key={u.id} value={u.id}>{u.fullName || u.email}</option>)}
              </Select>
            </FormField>
            <Button disabled={!userId} loading={grant.isPending} onClick={() => grant.mutate()}>{t('mfg.plants.grant')}</Button>
          </>
        )}
      </div>
      <DataTable
        data={grants.map((g) => ({ id: g.userId, ...g }))}
        emptyMessage={t('mfg.plants.noAccess')}
        columns={[
          { key: 'user', label: t('mfg.plants.user'), render: (g) => nameOf(g.userId) },
          { key: 'actions', label: t('common.actions'), render: (g) => canManage && <Button variant="link-danger" size="sm" onClick={() => revoke.mutate(g.userId)}>{t('mfg.plants.revoke')}</Button> },
        ]}
      />
    </div>
  )
}

export function PlantsPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:masterdata:manage')
  const [tab, setTab] = useState<'plants' | 'access'>('plants')
  const [editing, setEditing] = useState<Plant | 'new' | null>(null)
  const [toggling, setToggling] = useState<Plant | null>(null)
  const { data: plants = [], isLoading } = useQuery({ queryKey: ['mfg', 'plants', 'all'], queryFn: () => mfgApi.listPlants(false) })

  const toggle = useMutation({
    mutationFn: (p: Plant) => mfgApi.setPlantActive(p.id, !p.isActive),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'plants'] }); toast.success(t('common.saved')); setToggling(null) },
    onError: (e) => { toast.error(serverMessage(e, t('common.failedToSave'))); setToggling(null) },
  })

  return (
    <div>
      <PageHeader title={t('mfg.nav.plants')} hint={t('mfg.plants.hint')} actions={canManage && tab === 'plants' ? <Button onClick={() => setEditing('new')}>{t('mfg.plants.add')}</Button> : undefined} />
      <TabList id="mfg-plants" label={t('mfg.nav.plants')} current={tab} onSelect={setTab} tabs={[{ key: 'plants', label: t('mfg.nav.plants') }, { key: 'access', label: t('mfg.plants.accessTab') }]} />
      <div className="pt-4" role="tabpanel">
        {tab === 'plants' ? (
          <DataTable
            data={plants}
            isLoading={isLoading}
            emptyState={<EmptyState title={t('mfg.noPlantTitle')} description={t('mfg.plants.emptyHint')} />}
            columns={[
              { key: 'code', label: t('common.code'), render: (p: Plant) => <span className="font-mono text-xs">{p.code}</span> },
              { key: 'name', label: t('common.name') },
              { key: 'timeZoneId', label: t('mfg.plants.timeZone') },
              { key: 'isActive', label: t('common.status'), render: (p: Plant) => <ActiveBadge active={p.isActive} /> },
              {
                key: 'actions',
                label: t('common.actions'),
                render: (p: Plant) => canManage && (
                  <div className="flex gap-2">
                    <Button variant="link" size="sm" onClick={() => setEditing(p)}>{t('common.edit')}</Button>
                    <Button variant={p.isActive ? 'link-danger' : 'link'} size="sm" onClick={() => setToggling(p)}>{p.isActive ? t('common.deactivate') : t('common.activate')}</Button>
                  </div>
                ),
              },
            ]}
          />
        ) : (
          <AccessPanel plants={plants.filter((p) => p.isActive)} />
        )}
      </div>
      {editing && <PlantModal plant={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={!!toggling}
        title={toggling?.isActive ? t('common.deactivate') : t('common.activate')}
        message={toggling ? t(toggling.isActive ? 'mfg.plants.deactivateAsk' : 'mfg.plants.activateAsk', { code: toggling.code }) : ''}
        danger={!!toggling?.isActive}
        onConfirm={() => toggling && toggle.mutate(toggling)}
        onCancel={() => setToggling(null)}
      />
    </div>
  )
}
