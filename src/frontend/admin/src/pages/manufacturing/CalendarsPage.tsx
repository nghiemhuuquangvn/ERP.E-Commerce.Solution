import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { DAYS, mfgApi, type CalendarExceptionType, type Shift, type WorkCalendar } from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { TabList } from '@/components/shared/PageTabs'
import { ActiveBadge, NoPlant, PageHeader, PlantSelector, todayIso, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const hhmm = (s: string) => s.slice(0, 5)
const withSeconds = (s: string) => (s.length === 5 ? `${s}:00` : s)

// ── Shifts ────────────────────────────────────────────────────────────────────────────────────────────────
interface ShiftForm { code: string; name: string; startTime: string; endTime: string; breakMinutes: number }

function ShiftModal({ plantId, shift, onClose }: { plantId: string; shift: Shift | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const { register, handleSubmit, formState: { errors } } = useForm<ShiftForm>({
    defaultValues: { code: shift?.code ?? '', name: shift?.name ?? '', startTime: hhmm(shift?.startTime ?? '06:00'), endTime: hhmm(shift?.endTime ?? '14:00'), breakMinutes: shift?.breakMinutes ?? 30 },
  })
  const save = useMutation({
    mutationFn: (v: ShiftForm) => {
      const body = { name: v.name, startTime: withSeconds(v.startTime), endTime: withSeconds(v.endTime), breakMinutes: Number(v.breakMinutes) }
      return shift ? mfgApi.updateShift(shift.id, body) : mfgApi.createShift(plantId, v.code, body)
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['mfg', 'shifts'] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'calendars'] }); toast.success(t('common.saved')); onClose() },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  return (
    <Modal open size="sm" title={shift ? t('mfg.calendars.editShift') : t('mfg.calendars.addShift')} onClose={onClose} closeOnEsc={!save.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" form="shift-form" loading={save.isPending}>{t('common.save')}</Button></>}>
      <form id="shift-form" onSubmit={handleSubmit((v) => save.mutate(v))} className="space-y-4">
        {error && <Alert variant="danger">{error}</Alert>}
        {!shift && <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}><Input {...register('code', { required: true })} /></FormField>}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}><Input {...register('name', { required: true })} /></FormField>
        <div className="grid grid-cols-2 gap-4">
          <FormField label={t('mfg.calendars.start')} required><Input type="time" {...register('startTime', { required: true })} /></FormField>
          <FormField label={t('mfg.calendars.end')} hint={t('mfg.calendars.overnightHint')} required><Input type="time" {...register('endTime', { required: true })} /></FormField>
        </div>
        <FormField label={t('mfg.calendars.breakMinutes')} hint={t('mfg.calendars.breakHint')}><Input type="number" min={0} {...register('breakMinutes', { required: true, min: 0 })} /></FormField>
      </form>
    </Modal>
  )
}

function ShiftsPanel({ plantId }: { plantId: string }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:masterdata:manage')
  const [editing, setEditing] = useState<Shift | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Shift | null>(null)
  const { data: shifts = [], isLoading } = useQuery({ queryKey: ['mfg', 'shifts', plantId], queryFn: () => mfgApi.listShifts(plantId) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['mfg', 'shifts'] })
  const toggle = useMutation({ mutationFn: (s: Shift) => mfgApi.setShiftActive(s.id, !s.isActive), onSuccess: refresh, onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))) })
  const remove = useMutation({
    mutationFn: (s: Shift) => mfgApi.deleteShift(s.id),
    onSuccess: () => { refresh(); setDeleting(null) },
    onError: (e) => { toast.error(serverMessage(e, t('common.failedToSave'))); setDeleting(null) },
  })
  return (
    <div className="space-y-3">
      {canManage && <div className="flex justify-end"><Button onClick={() => setEditing('new')}>{t('mfg.calendars.addShift')}</Button></div>}
      <DataTable
        data={shifts}
        isLoading={isLoading}
        emptyMessage={t('mfg.calendars.noShifts')}
        columns={[
          { key: 'code', label: t('common.code'), render: (s: Shift) => <span className="font-mono text-xs">{s.code}</span> },
          { key: 'name', label: t('common.name') },
          { key: 'time', label: t('mfg.calendars.hours'), render: (s: Shift) => <span>{hhmm(s.startTime)} - {hhmm(s.endTime)}{s.endTime <= s.startTime ? ` (${t('mfg.calendars.overnight')})` : ''}</span> },
          { key: 'breakMinutes', label: t('mfg.calendars.breakMinutes'), align: 'right' as const },
          { key: 'durationMinutes', label: t('mfg.calendars.workMinutes'), align: 'right' as const },
          { key: 'isActive', label: t('common.status'), render: (s: Shift) => <ActiveBadge active={s.isActive} /> },
          {
            key: 'actions', label: t('common.actions'),
            render: (s: Shift) => canManage && (
              <div className="flex gap-2">
                <Button variant="link" size="sm" onClick={() => setEditing(s)}>{t('common.edit')}</Button>
                <Button variant="link" size="sm" onClick={() => toggle.mutate(s)}>{s.isActive ? t('common.deactivate') : t('common.activate')}</Button>
                <Button variant="link-danger" size="sm" onClick={() => setDeleting(s)}>{t('common.delete')}</Button>
              </div>
            ),
          },
        ]}
      />
      {editing && <ShiftModal plantId={plantId} shift={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog open={!!deleting} danger title={t('common.delete')} message={t('mfg.deleteAsk', { code: deleting?.code ?? '' })} onConfirm={() => deleting && remove.mutate(deleting)} onCancel={() => setDeleting(null)} />
    </div>
  )
}

// ── Calendars ─────────────────────────────────────────────────────────────────────────────────────────────
function MonthGrid({ calendarId }: { calendarId: string }) {
  const { t } = useTranslation()
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const { data: days = [] } = useQuery({ queryKey: ['mfg', 'month', calendarId, year, month], queryFn: () => mfgApi.month(calendarId, year, month) })
  const first = days.length ? (new Date(days[0].date + 'T00:00:00').getDay() + 6) % 7 : 0 // Monday first
  const shift = (d: number) => { const m = month - 1 + d; setYear(year + Math.floor(m / 12)); setMonth(((m % 12) + 12) % 12 + 1) }
  return (
    <div>
      <div className="mb-2 flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={() => shift(-1)} aria-label={t('mfg.calendars.prevMonth')}>‹</Button>
        <span className="min-w-28 text-center text-sm font-medium" aria-live="polite">{year}-{String(month).padStart(2, '0')}</span>
        <Button variant="outline" size="sm" onClick={() => shift(1)} aria-label={t('mfg.calendars.nextMonth')}>›</Button>
      </div>
      <table className="w-full table-fixed border-collapse text-center text-xs">
        <caption className="sr-only">{t('mfg.calendars.monthView')}</caption>
        <thead><tr>{DAYS.map((d) => <th key={d} scope="col" className="border border-line bg-surface-muted py-1 font-medium text-ink-secondary">{t(`mfg.day.${d}`)}</th>)}</tr></thead>
        <tbody>
          {Array.from({ length: Math.ceil((first + days.length) / 7) }, (_, week) => (
            <tr key={week}>
              {Array.from({ length: 7 }, (_, col) => {
                const day = days[week * 7 + col - first]
                if (!day) return <td key={col} className="border border-line" />
                return (
                  <td key={col} className="border border-line p-1 align-top" title={day.isException ? t(`mfg.exception.${day.exceptionType}`) : undefined}>
                    <div className="text-ink-secondary">{Number(day.date.slice(8))}</div>
                    <div className={day.workingMinutes === 0 ? 'text-ink-muted' : 'font-semibold text-ink'}>{day.workingMinutes}</div>
                    {day.isException && <div className="text-warning-soft-text">{t(`mfg.exception.${day.exceptionType}`)}</div>}
                  </td>
                )
              })}
            </tr>
          ))}
        </tbody>
      </table>
      <p className="mt-1 text-xs text-ink-secondary">{t('mfg.calendars.monthHint')}</p>
    </div>
  )
}

function WorkingTimeTool({ calendarId }: { calendarId: string }) {
  const { t } = useTranslation()
  const [date, setDate] = useState(todayIso())
  const [minute, setMinute] = useState(0)
  const [amount, setAmount] = useState(60)
  const [op, setOp] = useState('SubtractMinutes')
  const [answer, setAnswer] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const ask = useMutation({
    mutationFn: () => mfgApi.workingTime(calendarId, op, date, minute, amount),
    onSuccess: (r) => { setError(null); setAnswer(r.workingMinute != null && op.endsWith('Minutes') ? `${r.date} · ${t('mfg.calendars.workingMinute')} ${r.workingMinute}${r.clockTime ? ` · ${r.clockTime.slice(11, 16)}` : ''}` : r.date) },
    onError: (e) => { setAnswer(null); setError(serverMessage(e, t('common.failedToSave'))) },
  })
  return (
    <form className="space-y-2" onSubmit={(e) => { e.preventDefault(); ask.mutate() }} aria-label={t('mfg.calendars.tool')}>
      <h3 className="text-sm font-semibold">{t('mfg.calendars.tool')}</h3>
      <div className="flex flex-wrap items-end gap-2">
        <FormField label={t('mfg.calendars.operation')}>
          <Select className="w-48" value={op} onChange={(e) => setOp(e.target.value)}>
            {['AddMinutes', 'SubtractMinutes', 'AddDays', 'SubtractDays'].map((o) => <option key={o} value={o}>{t(`mfg.calendars.op.${o}`)}</option>)}
          </Select>
        </FormField>
        <FormField label={t('common.date')}><Input type="date" className="w-40" value={date} onChange={(e) => setDate(e.target.value)} /></FormField>
        {op.endsWith('Minutes') && <FormField label={t('mfg.calendars.fromMinute')}><Input type="number" min={0} className="w-24" value={minute} onChange={(e) => setMinute(Number(e.target.value))} /></FormField>}
        <FormField label={t('mfg.calendars.amount')}><Input type="number" min={0} className="w-24" value={amount} onChange={(e) => setAmount(Number(e.target.value))} /></FormField>
        <Button type="submit" variant="secondary" loading={ask.isPending}>{t('mfg.calendars.calculate')}</Button>
      </div>
      {answer && <p className="text-sm font-medium" role="status">{answer}</p>}
      {error && <Alert variant="danger">{error}</Alert>}
    </form>
  )
}

function CalendarEditor({ calendar, shifts, canManage }: { calendar: WorkCalendar; shifts: Shift[]; canManage: boolean }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [error, setError] = useState<string | null>(null)
  const [exc, setExc] = useState<{ date: string; type: CalendarExceptionType; description: string; shiftIds: string[] }>({ date: todayIso(), type: 'Holiday', description: '', shiftIds: [] })
  const active = shifts.filter((s) => s.isActive)
  const refresh = () => { setError(null); queryClient.invalidateQueries({ queryKey: ['mfg', 'calendars'] }); queryClient.invalidateQueries({ queryKey: ['mfg', 'month', calendar.id] }) }
  const fail = (e: unknown) => setError(serverMessage(e, t('common.failedToSave')))
  const setDay = useMutation({ mutationFn: (v: { day: string; ids: string[] }) => mfgApi.setCalendarDay(calendar.id, v.day, v.ids), onSuccess: refresh, onError: fail })
  const addExc = useMutation({
    mutationFn: () => mfgApi.addException(calendar.id, { date: exc.date, type: exc.type, description: exc.description || null, shiftIds: exc.type === 'Holiday' ? [] : exc.shiftIds }),
    onSuccess: () => { refresh(); toast.success(t('common.saved')) },
    onError: fail,
  })
  const delExc = useMutation({ mutationFn: (date: string) => mfgApi.removeException(calendar.id, date), onSuccess: refresh, onError: fail })

  const dayOf = (name: string) => calendar.week.find((d) => String(d.dayOfWeek) === name)

  return (
    <div className="space-y-6">
      {error && <Alert variant="danger">{error}</Alert>}
      <section aria-labelledby="mfg-week">
        <h3 id="mfg-week" className="mb-2 text-sm font-semibold">{t('mfg.calendars.week')}</h3>
        <table className="w-full text-sm">
          <caption className="sr-only">{t('mfg.calendars.week')}</caption>
          <thead><tr className="border-b border-line text-left text-ink-secondary"><th className="py-1 pr-3" scope="col">{t('common.date')}</th><th className="py-1" scope="col">{t('mfg.calendars.shifts')}</th><th className="py-1 text-right" scope="col">{t('mfg.calendars.workMinutes')}</th></tr></thead>
          <tbody>
            {DAYS.map((d) => {
              const row = dayOf(d)
              return (
                <tr key={d} className="border-b border-line">
                  <th scope="row" className="py-1 pr-3 text-left font-medium">{t(`mfg.day.${d}`)}</th>
                  <td className="flex flex-wrap gap-x-4 py-1">
                    {active.map((s) => (
                      <Checkbox
                        key={s.id}
                        label={s.code}
                        disabled={!canManage || setDay.isPending}
                        checked={!!row?.shiftIds.includes(s.id)}
                        onChange={(e) => setDay.mutate({ day: d, ids: e.target.checked ? [...(row?.shiftIds ?? []), s.id] : (row?.shiftIds ?? []).filter((x) => x !== s.id) })}
                      />
                    ))}
                  </td>
                  <td className="py-1 text-right font-mono">{row?.workingMinutes ?? 0}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </section>

      <section aria-labelledby="mfg-exc">
        <h3 id="mfg-exc" className="mb-2 text-sm font-semibold">{t('mfg.calendars.exceptions')}</h3>
        {canManage && (
          <div className="mb-3 flex flex-wrap items-end gap-2">
            <FormField label={t('common.date')}><Input type="date" className="w-40" value={exc.date} onChange={(e) => setExc({ ...exc, date: e.target.value })} /></FormField>
            <FormField label={t('mfg.calendars.exceptionType')}>
              <Select className="w-48" value={exc.type} onChange={(e) => setExc({ ...exc, type: e.target.value as CalendarExceptionType })}>
                {['Holiday', 'ExtraWorkingDay', 'ModifiedShifts'].map((x) => <option key={x} value={x}>{t(`mfg.exception.${x}`)}</option>)}
              </Select>
            </FormField>
            {exc.type !== 'Holiday' && (
              <fieldset className="flex flex-wrap gap-3 pb-1"><legend className="sr-only">{t('mfg.calendars.shifts')}</legend>
                {active.map((s) => <Checkbox key={s.id} label={s.code} checked={exc.shiftIds.includes(s.id)} onChange={(e) => setExc({ ...exc, shiftIds: e.target.checked ? [...exc.shiftIds, s.id] : exc.shiftIds.filter((x) => x !== s.id) })} />)}
              </fieldset>
            )}
            <FormField label={t('common.notes')}><Input className="w-56" value={exc.description} onChange={(e) => setExc({ ...exc, description: e.target.value })} /></FormField>
            <Button loading={addExc.isPending} onClick={() => addExc.mutate()}>{t('common.add')}</Button>
          </div>
        )}
        <DataTable
          data={calendar.exceptions.map((x) => ({ id: x.date, ...x }))}
          emptyMessage={t('mfg.calendars.noExceptions')}
          columns={[
            { key: 'date', label: t('common.date') },
            { key: 'type', label: t('mfg.calendars.exceptionType'), render: (x) => t(`mfg.exception.${x.type}`) },
            { key: 'description', label: t('common.notes') },
            { key: 'workingMinutes', label: t('mfg.calendars.workMinutes'), align: 'right' as const },
            { key: 'actions', label: t('common.actions'), render: (x) => canManage && <Button variant="link-danger" size="sm" onClick={() => delExc.mutate(x.date)}>{t('common.delete')}</Button> },
          ]}
        />
      </section>

      <section aria-labelledby="mfg-month"><h3 id="mfg-month" className="mb-2 text-sm font-semibold">{t('mfg.calendars.monthView')}</h3><MonthGrid calendarId={calendar.id} /></section>
      <WorkingTimeTool calendarId={calendar.id} />
    </div>
  )
}

function CalendarsPanel({ plantId }: { plantId: string }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('manufacturing:masterdata:manage')
  const [selected, setSelected] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const { data: calendars = [] } = useQuery({ queryKey: ['mfg', 'calendars', plantId], queryFn: () => mfgApi.listCalendars(plantId) })
  const { data: shifts = [] } = useQuery({ queryKey: ['mfg', 'shifts', plantId], queryFn: () => mfgApi.listShifts(plantId) })
  const current = calendars.find((c) => c.id === selected) ?? calendars[0]

  const create = useMutation({
    mutationFn: () => mfgApi.createCalendar(plantId, code, name),
    onSuccess: (c) => { queryClient.invalidateQueries({ queryKey: ['mfg', 'calendars'] }); setAdding(false); setCode(''); setName(''); setSelected(c.id); toast.success(t('common.saved')) },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })
  const toggle = useMutation({ mutationFn: (c: WorkCalendar) => mfgApi.setCalendarActive(c.id, !c.isActive), onSuccess: () => queryClient.invalidateQueries({ queryKey: ['mfg', 'calendars'] }), onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))) })
  const remove = useMutation({
    mutationFn: (c: WorkCalendar) => mfgApi.deleteCalendar(c.id),
    onSuccess: () => { setSelected(null); queryClient.invalidateQueries({ queryKey: ['mfg', 'calendars'] }) },
    onError: (e) => toast.error(serverMessage(e, t('common.failedToSave'))),
  })

  return (
    <div className="grid gap-6 lg:grid-cols-[16rem_minmax(0,1fr)]">
      <aside aria-label={t('mfg.calendars.list')} className="space-y-2">
        {canManage && <Button size="sm" onClick={() => { setAdding(true); setError(null) }}>{t('mfg.calendars.add')}</Button>}
        {calendars.length === 0 && <EmptyState title={t('mfg.calendars.none')} />}
        <ul className="space-y-1">
          {calendars.map((c) => (
            <li key={c.id}>
              <button type="button" onClick={() => setSelected(c.id)} aria-current={current?.id === c.id || undefined}
                className={`flex w-full items-center justify-between rounded-md border px-3 py-2 text-left text-sm ${current?.id === c.id ? 'border-brand-action bg-brand-soft text-brand-soft-text' : 'border-line bg-surface text-ink'}`}>
                <span><span className="font-mono text-xs">{c.code}</span> {c.name}</span>
                {!c.isActive && <ActiveBadge active={false} />}
              </button>
            </li>
          ))}
        </ul>
      </aside>
      <div>
        {current ? (
          <>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <h2 className="text-lg font-semibold">{current.code} - {current.name}</h2>
              {canManage && (
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" onClick={() => toggle.mutate(current)}>{current.isActive ? t('common.deactivate') : t('common.activate')}</Button>
                  <Button variant="outline" size="sm" onClick={() => remove.mutate(current)}>{t('common.delete')}</Button>
                </div>
              )}
            </div>
            <CalendarEditor calendar={current} shifts={shifts} canManage={canManage} />
          </>
        ) : <EmptyState title={t('mfg.calendars.none')} description={t('mfg.calendars.noneHint')} />}
      </div>
      <Modal open={adding} size="sm" title={t('mfg.calendars.add')} onClose={() => setAdding(false)}
        footer={<><Button variant="outline" onClick={() => setAdding(false)}>{t('common.cancel')}</Button><Button disabled={!code || !name} loading={create.isPending} onClick={() => create.mutate()}>{t('common.save')}</Button></>}>
        <div className="space-y-4">
          {error && <Alert variant="danger">{error}</Alert>}
          <FormField label={t('common.code')} required><Input value={code} onChange={(e) => setCode(e.target.value)} /></FormField>
          <FormField label={t('common.name')} required><Input value={name} onChange={(e) => setName(e.target.value)} /></FormField>
        </div>
      </Modal>
    </div>
  )
}

/** S04: shifts and work calendars (BA §5.4). */
export function CalendarsPage() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<'shifts' | 'calendars'>('calendars')
  const { plantId, isLoading } = usePlantContext()
  if (!isLoading && !plantId) return <div><PageHeader title={t('mfg.nav.calendars')} /><NoPlant /></div>
  return (
    <div>
      <PageHeader title={t('mfg.nav.calendars')} hint={t('mfg.calendars.hint')} actions={<PlantSelector />} />
      <TabList id="mfg-cal" label={t('mfg.nav.calendars')} current={tab} onSelect={setTab} tabs={[{ key: 'calendars', label: t('mfg.calendars.calendarsTab') }, { key: 'shifts', label: t('mfg.calendars.shiftsTab') }]} />
      <div className="pt-4" role="tabpanel">{plantId && (tab === 'shifts' ? <ShiftsPanel plantId={plantId} /> : <CalendarsPanel plantId={plantId} />)}</div>
    </div>
  )
}
