import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  organizationCalendarsApi,
  type OrganizationCalendar,
  type Holiday,
} from '@/api/masterdata/organizationCalendars.api'
import { companiesApi } from '@/api/masterdata/companies.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, Checkbox, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

type Tab = 'calendars' | 'holidays'

interface CalendarFormValues {
  companyId: string
  year: number
  name: string
  isDefault: boolean
}

function CalendarModal({
  calendar,
  companies,
  defaultCompanyId,
  onClose,
}: {
  calendar: OrganizationCalendar | null
  companies: { id: string; name: string }[]
  defaultCompanyId: string
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!calendar

  const { register, handleSubmit, formState: { errors } } = useForm<CalendarFormValues>({
    defaultValues: {
      companyId: calendar?.companyId ?? defaultCompanyId,
      year: calendar?.year ?? new Date().getFullYear(),
      name: calendar?.name ?? '',
      isDefault: calendar?.isDefault ?? false,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CalendarFormValues) => {
      if (isEdit) {
        return organizationCalendarsApi.update(calendar!.id, {
          name: values.name,
          isDefault: values.isDefault,
        })
      }
      return organizationCalendarsApi.create({
        companyId: values.companyId,
        year: Number(values.year),
        name: values.name,
        isDefault: values.isDefault,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'organizationCalendars'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit
            ? t('masterdata.organizationCalendars.editTitle')
            : t('masterdata.organizationCalendars.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="calendar-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="calendar-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <>
            <FormField label={t('masterdata.organizationCalendars.company')} required error={errors.companyId ? t('common.required') : undefined}>
              <Select {...register('companyId', { required: true })}>
                <option value="">{t('common.select')}</option>
                {companies.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Select>
            </FormField>
            <FormField label={t('masterdata.organizationCalendars.year')} required error={errors.year ? t('common.required') : undefined}>
              <Input
                type="number"
                {...register('year', { required: true, valueAsNumber: true })}
              />
            </FormField>
          </>
        )}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <Checkbox label={t('masterdata.organizationCalendars.isDefault')} {...register('isDefault')} />
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.organizationCalendars.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

interface HolidayFormValues {
  date: string
  name: string
  isRecurringYearly: boolean
}

function HolidayModal({
  calendarId,
  holiday,
  onClose,
}: {
  calendarId: string
  holiday: Holiday | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!holiday

  const { register, handleSubmit, formState: { errors } } = useForm<HolidayFormValues>({
    defaultValues: {
      date: holiday?.date ? holiday.date.slice(0, 10) : '',
      name: holiday?.name ?? '',
      isRecurringYearly: holiday?.isRecurringYearly ?? false,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: HolidayFormValues) => {
      if (isEdit) {
        return organizationCalendarsApi.updateHoliday(calendarId, holiday!.id, {
          date: values.date,
          name: values.name,
          isRecurringYearly: values.isRecurringYearly,
        })
      }
      return organizationCalendarsApi.createHoliday(calendarId, {
        date: values.date,
        name: values.name,
        isRecurringYearly: values.isRecurringYearly,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'holidays', calendarId] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.organizationCalendars.editHolidayTitle') : t('masterdata.organizationCalendars.addHolidayTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="holiday-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="holiday-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.date')} required error={errors.date ? t('common.required') : undefined}>
          <Input type="date" {...register('date', { required: true })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <Checkbox label={t('masterdata.organizationCalendars.isRecurringYearly')} {...register('isRecurringYearly')} />
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.organizationCalendars.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function OrganizationCalendarsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('masterdata:organization-calendars:manage')
  const [tab, setTab] = useState<Tab>('calendars')
  const { page, pageSize, setPage, reset } = usePagination()

  const [companyId, setCompanyId] = useState('')
  const [calendarId, setCalendarId] = useState('')

  const [calendarModalState, setCalendarModalState] = useState<{ open: boolean; calendar: OrganizationCalendar | null }>({
    open: false,
    calendar: null,
  })
  const [holidayModalState, setHolidayModalState] = useState<{ open: boolean; holiday: Holiday | null }>({
    open: false,
    holiday: null,
  })

  const { data: companiesPage } = useQuery({
    queryKey: ['masterdata', 'companies', 'picker'],
    queryFn: () => companiesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })
  const companies = companiesPage?.items ?? []

  const calendarsQuery = useQuery({
    queryKey: ['masterdata', 'organizationCalendars', { companyId, page, pageSize }],
    queryFn: () => organizationCalendarsApi.list({ companyId: companyId || undefined, page, pageSize }),
    staleTime: 30_000,
  })

  const allCalendarsQuery = useQuery({
    queryKey: ['masterdata', 'organizationCalendars', 'picker'],
    queryFn: () => organizationCalendarsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const holidaysQuery = useQuery({
    queryKey: ['masterdata', 'holidays', calendarId, { page, pageSize }],
    queryFn: () => organizationCalendarsApi.listHolidays(calendarId, { page, pageSize }),
    enabled: !!calendarId,
    staleTime: 30_000,
  })

  const calendars = allCalendarsQuery.data?.items ?? []

  const calendarColumns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      { key: 'year', label: t('masterdata.organizationCalendars.year') },
      {
        key: 'companyId',
        label: t('masterdata.organizationCalendars.company'),
        render: (c: OrganizationCalendar) => companies.find((co) => co.id === c.companyId)?.name ?? c.companyId,
      },
      {
        key: 'isDefault',
        label: t('masterdata.organizationCalendars.isDefault'),
        render: (c: OrganizationCalendar) => (c.isDefault ? t('common.yes') : t('common.no')),
      },
      {
        key: 'actions',
        label: '',
        render: (c: OrganizationCalendar) =>
          canManage ? (
            <Button variant="link" className="text-xs"
              onClick={() => setCalendarModalState({ open: true, calendar: c })}>
              {t('common.edit')}
            </Button>
          ) : null,
      },
    ],
    [canManage, companies, t],
  )

  const holidayColumns = useMemo(
    () => [
      { key: 'date', label: t('common.date'), render: (h: Holiday) => h.date.slice(0, 10) },
      { key: 'name', label: t('common.name') },
      {
        key: 'isRecurringYearly',
        label: t('masterdata.organizationCalendars.isRecurringYearly'),
        render: (h: Holiday) => (h.isRecurringYearly ? t('common.yes') : t('common.no')),
      },
      {
        key: 'actions',
        label: '',
        render: (h: Holiday) =>
          canManage ? (
            <Button variant="link" className="text-xs"
              onClick={() => setHolidayModalState({ open: true, holiday: h })}>
              {t('common.edit')}
            </Button>
          ) : null,
      },
    ],
    [canManage, t],
  )

  function switchTab(next: Tab) {
    setTab(next)
    reset()
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.organizationCalendars.title')}</h1>
        {canManage && tab === 'calendars' && (
          <Button onClick={() => setCalendarModalState({ open: true, calendar: null })}>
            {t('masterdata.organizationCalendars.add')}
          </Button>
        )}
        {canManage && tab === 'holidays' && calendarId && (
          <Button onClick={() => setHolidayModalState({ open: true, holiday: null })}>
            {t('masterdata.organizationCalendars.addHoliday')}
          </Button>
        )}
      </div>

      <div className="flex gap-2 border-b border-line">
        {(
          [
            { key: 'calendars' as const, label: t('masterdata.organizationCalendars.calendarsTab') },
            { key: 'holidays' as const, label: t('masterdata.organizationCalendars.holidaysTab') },
          ]
        ).map((x) => (
          <button
            key={x.key}
            onClick={() => switchTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key ? 'border-b-2 border-brand-action text-brand-action' : 'text-ink-secondary hover:text-ink'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'calendars' && (
        <>
          <Select
            className="max-w-xs"
        aria-label={t('masterdata.organizationCalendars.allCompanies')}
            value={companyId}
            onChange={(e) => {
              setCompanyId(e.target.value)
              setPage(1)
            }}
          >
            <option value="">{t('masterdata.organizationCalendars.allCompanies')}</option>
            {companies.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
          <DataTable
            columns={calendarColumns}
            data={calendarsQuery.data?.items ?? []}
            isLoading={calendarsQuery.isLoading}
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={calendarsQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {tab === 'holidays' && (
        <>
          <Select
            className="max-w-xs"
        aria-label={t('masterdata.organizationCalendars.selectCalendar')}
            value={calendarId}
            onChange={(e) => {
              setCalendarId(e.target.value)
              setPage(1)
            }}
          >
            <option value="">{t('masterdata.organizationCalendars.selectCalendar')}</option>
            {calendars.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.year})
              </option>
            ))}
          </Select>
          <DataTable
            columns={holidayColumns}
            data={holidaysQuery.data?.items ?? []}
            isLoading={holidaysQuery.isLoading}
            emptyMessage={calendarId ? t('masterdata.organizationCalendars.noHolidays') : t('masterdata.organizationCalendars.pickCalendarFirst')}
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={holidaysQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {calendarModalState.open && (
        <CalendarModal
          calendar={calendarModalState.calendar}
          companies={companies}
          defaultCompanyId={companyId}
          onClose={() => setCalendarModalState({ open: false, calendar: null })}
        />
      )}

      {holidayModalState.open && calendarId && (
        <HolidayModal
          calendarId={calendarId}
          holiday={holidayModalState.holiday}
          onClose={() => setHolidayModalState({ open: false, holiday: null })}
        />
      )}
    </div>
  )
}
