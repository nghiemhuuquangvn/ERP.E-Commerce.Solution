import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import { mfgApi, type WorkCenter, type WorkCenterValues } from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ActiveBadge, NoPlant, PageHeader, PlantSelector, fmtQty, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'
import { colors } from '@/styles/tokens'

interface CenterForm extends Omit<WorkCenterValues, 'departmentId' | 'vendorId'> { code: string; departmentId: string; vendorId: string }

function WorkCenterModal({ plantId, center, onClose }: { plantId: string; center: WorkCenter | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const { data: calendars = [] } = useQuery({ queryKey: ['mfg', 'calendars', plantId], queryFn: () => mfgApi.listCalendars(plantId) })
  const v = center?.values
  const { register, handleSubmit, watch, formState: { errors } } = useForm<CenterForm>({
    defaultValues: {
      code: center?.code ?? '', name: v?.name ?? '', calendarId: v?.calendarId ?? '', type: v?.type ?? 'Machine', numberOfResources: v?.numberOfResources ?? 1,
      efficiencyPct: v?.efficiencyPct ?? 100, utilizationPct: v?.utilizationPct ?? 100, laborRatePerHour: v?.laborRatePerHour ?? 0, machineRatePerHour: v?.machineRatePerHour ?? 0,
      queueTimeMinutes: v?.queueTimeMinutes ?? 0, moveTimeMinutes: v?.moveTimeMinutes ?? 0, departmentId: v?.departmentId ?? '', vendorId: v?.vendorId ?? '',
    },
  })
  const type = watch('type')
  const save = useMutation({
    mutationFn: (f: CenterForm) => {
      const values: WorkCenterValues = {
        name: f.name, calendarId: f.calendarId, type: f.type, numberOfResources: Number(f.numberOfResources), efficiencyPct: Number(f.efficiencyPct), utilizationPct: Number(f.utilizationPct),
        laborRatePerHour: Number(f.laborRatePerHour), machineRatePerHour: Number(f.machineRatePerHour), queueTimeMinutes: Number(f.queueTimeMinutes), moveTimeMinutes: Number(f.moveTimeMinutes),
        departmentId: f.departmentId || null, vendorId: f.type === 'Subcontract' ? f.vendorId || null : null,
      }
      return center ? mfgApi.updateWorkCenter(center.id, values) : mfgApi.createWorkCenter(plantId, f.code, values)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'work-centers'] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const bad = (k: keyof CenterForm) => (errors[k] ? t('common.required') : undefined)

  return (
    <Modal open size="lg" title={center ? t('mfg.wc.edit') : t('mfg.wc.add')} onClose={onClose} closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="wc-form" loading={save.isPending}>{t('common.save')}</Button></>}>
      <form id="wc-form" onSubmit={handleSubmit((f) => save.mutate(f))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        <div className="grid gap-4 sm:grid-cols-3">
          {!center && <FormField label={t('common.code')} hint={t('mfg.plants.codeHint')} required error={bad('code')}><Input {...register('code', { required: true })} /></FormField>}
          <FormField label={t('common.name')} required error={bad('name')} className={center ? 'sm:col-span-2' : undefined}><Input {...register('name', { required: true })} /></FormField>
          <FormField label={t('mfg.wc.type')}>
            <Select {...register('type')}>{['Machine', 'Labor', 'Subcontract'].map((x) => <option key={x} value={x}>{t(`mfg.wc.${x}`)}</option>)}</Select>
          </FormField>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField label={t('mfg.wc.calendar')} required error={bad('calendarId')}>
            <Select {...register('calendarId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {calendars.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.code} - {c.name}</option>)}
            </Select>
          </FormField>
          {type === 'Subcontract' && <FormField label={t('mfg.wc.vendorId')} hint={t('mfg.wc.vendorHint')} required error={bad('vendorId')}><Input {...register('vendorId', { required: true })} /></FormField>}
        </div>
        <div className="grid gap-4 sm:grid-cols-4">
          <FormField label={t('mfg.wc.resources')} hint="1-999"><Input type="number" min={1} max={999} {...register('numberOfResources', { required: true, min: 1, max: 999 })} /></FormField>
          <FormField label={t('mfg.wc.efficiency')} hint="1-200"><Input type="number" step="0.01" min={1} max={200} {...register('efficiencyPct', { required: true, min: 1, max: 200 })} /></FormField>
          <FormField label={t('mfg.wc.utilization')} hint="1-100"><Input type="number" step="0.01" min={1} max={100} {...register('utilizationPct', { required: true, min: 1, max: 100 })} /></FormField>
          <div />
          <FormField label={t('mfg.wc.laborRate')}><Input type="number" step="any" min={0} {...register('laborRatePerHour', { required: true, min: 0 })} /></FormField>
          <FormField label={t('mfg.wc.machineRate')}><Input type="number" step="any" min={0} {...register('machineRatePerHour', { required: true, min: 0 })} /></FormField>
          <FormField label={t('mfg.wc.queue')}><Input type="number" min={0} {...register('queueTimeMinutes', { required: true, min: 0 })} /></FormField>
          <FormField label={t('mfg.wc.move')}><Input type="number" min={0} {...register('moveTimeMinutes', { required: true, min: 0 })} /></FormField>
        </div>
      </form>
    </Modal>
  )
}

const iso = (d: Date) => d.toISOString().slice(0, 10)

/** Working minutes against capacity (working minutes x resources x efficiency x utilization) per day, BR-WC-02. */
function CapacityPanel({ center }: { center: WorkCenter }) {
  const { t } = useTranslation()
  const start = new Date()
  const [from, setFrom] = useState(iso(start))
  const [to, setTo] = useState(iso(new Date(start.getTime() + 13 * 86_400_000)))
  const { data = [], isLoading, error } = useQuery({ queryKey: ['mfg', 'capacity', center.id, from, to], queryFn: () => mfgApi.capacity(center.id, from, to), enabled: from <= to })
  const total = data.reduce((s, d) => s + d.capacityMinutes, 0)
  return (
    <section aria-labelledby="mfg-capacity" className="mt-6 rounded-lg border border-line bg-surface p-4">
      <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
        <h2 id="mfg-capacity" className="text-sm font-semibold">{t('mfg.wc.capacityOf', { code: center.code })}</h2>
        <div className="flex items-end gap-2">
          <FormField label={t('mfg.wc.from')}><Input type="date" className="w-40" value={from} onChange={(e) => setFrom(e.target.value)} /></FormField>
          <FormField label={t('mfg.wc.to')}><Input type="date" className="w-40" value={to} onChange={(e) => setTo(e.target.value)} /></FormField>
        </div>
      </div>
      {error && <Alert variant="danger">{serverMessage(error, t('mfg.wc.capacityFailed'))}</Alert>}
      {!isLoading && data.length > 0 && (
        <>
          <p className="mb-2 text-sm text-ink-secondary">{t('mfg.wc.capacityTotal', { minutes: fmtQty(total, 1) })}</p>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={data.map((d) => ({ ...d, day: d.date.slice(5) }))}>
              <CartesianGrid strokeDasharray="3 3" stroke={colors.line} />
              <XAxis dataKey="day" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} />
              <Tooltip />
              <Legend />
              <Bar dataKey="workingMinutes" name={t('mfg.wc.workingMinutes')} fill={colors.lineStrong} />
              <Bar dataKey="capacityMinutes" name={t('mfg.wc.capacityMinutes')} fill={colors.brandAction} />
            </BarChart>
          </ResponsiveContainer>
          <table className="sr-only">
            <caption>{t('mfg.wc.capacityOf', { code: center.code })}</caption>
            <thead><tr><th scope="col">{t('common.date')}</th><th scope="col">{t('mfg.wc.workingMinutes')}</th><th scope="col">{t('mfg.wc.capacityMinutes')}</th></tr></thead>
            <tbody>{data.map((d) => <tr key={d.date}><td>{d.date}</td><td>{d.workingMinutes}</td><td>{d.capacityMinutes}</td></tr>)}</tbody>
          </table>
        </>
      )}
    </section>
  )
}

/** S05: work centers with capacity and rates (BA §5.5). */
export function WorkCentersPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:masterdata:manage')
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [editing, setEditing] = useState<WorkCenter | 'new' | null>(null)
  const [deleting, setDeleting] = useState<WorkCenter | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const { data: centers = [], isLoading } = useQuery({ queryKey: ['mfg', 'work-centers', plantId], queryFn: () => mfgApi.listWorkCenters(plantId!), enabled: !!plantId })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['mfg', 'work-centers'] })
  const toggle = useMutation({ mutationFn: (c: WorkCenter) => mfgApi.setWorkCenterActive(c.id, !c.isActive), onSuccess: refresh, onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))) })
  const remove = useMutation({
    mutationFn: (c: WorkCenter) => mfgApi.deleteWorkCenter(c.id),
    onSuccess: () => { refresh(); setDeleting(null) },
    onError: (e) => { toast.error(`${serverMessage(e, t('common.failedToSave'))} ${t('mfg.deactivateInstead')}`); setDeleting(null) },
  })

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.workCenters')} /><NoPlant /></div>
  const current = centers.find((c) => c.id === selected)
  return (
    <div>
      <PageHeader title={t('mfg.nav.workCenters')} hint={t('mfg.wc.hint')} actions={<><PlantSelector />{canManage && <Button onClick={() => setEditing('new')}>{t('mfg.wc.add')}</Button>}</>} />
      <DataTable
        data={centers}
        isLoading={isLoading}
        emptyMessage={t('mfg.wc.empty')}
        columns={[
          { key: 'code', label: t('common.code'), render: (c: WorkCenter) => <span className="font-mono text-xs">{c.code}</span> },
          { key: 'name', label: t('common.name'), render: (c: WorkCenter) => c.values.name },
          { key: 'type', label: t('mfg.wc.type'), render: (c: WorkCenter) => t(`mfg.wc.${c.values.type}`) },
          { key: 'resources', label: t('mfg.wc.resources'), align: 'right' as const, render: (c: WorkCenter) => c.values.numberOfResources },
          { key: 'eff', label: t('mfg.wc.efficiency'), align: 'right' as const, render: (c: WorkCenter) => `${fmtQty(c.values.efficiencyPct, 2)}%` },
          { key: 'util', label: t('mfg.wc.utilization'), align: 'right' as const, render: (c: WorkCenter) => `${fmtQty(c.values.utilizationPct, 2)}%` },
          { key: 'isActive', label: t('common.status'), render: (c: WorkCenter) => <ActiveBadge active={c.isActive} /> },
          {
            key: 'actions', label: t('common.actions'),
            render: (c: WorkCenter) => (
              <div className="flex flex-wrap gap-2">
                <Button variant="link" size="sm" onClick={() => setSelected(c.id)}>{t('mfg.wc.capacity')}</Button>
                {canManage && (
                  <>
                    <Button variant="link" size="sm" onClick={() => setEditing(c)}>{t('common.edit')}</Button>
                    <Button variant="link" size="sm" onClick={() => toggle.mutate(c)}>{c.isActive ? t('common.deactivate') : t('common.activate')}</Button>
                    <Button variant="link-danger" size="sm" onClick={() => setDeleting(c)}>{t('common.delete')}</Button>
                  </>
                )}
              </div>
            ),
          },
        ]}
      />
      {current && <CapacityPanel center={current} />}
      {editing && plantId && <WorkCenterModal plantId={plantId} center={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={!!deleting} danger title={t('common.delete')} message={t('mfg.deleteAsk', { code: deleting?.code ?? '' })} onConfirm={() => deleting && remove.mutate(deleting)} onCancel={() => setDeleting(null)} />
    </div>
  )
}
