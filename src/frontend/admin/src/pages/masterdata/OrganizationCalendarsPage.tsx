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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit
            ? t('masterdata.organizationCalendars.editTitle')
            : t('masterdata.organizationCalendars.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('masterdata.organizationCalendars.company')}
                </label>
                <select className="input" {...register('companyId', { required: true })}>
                  <option value="">{t('common.select')}</option>
                  {companies.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
                {errors.companyId && (
                  <p className="mt-1 text-xs text-red-600">
                    {t('masterdata.organizationCalendars.company')} is required.
                  </p>
                )}
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  {t('masterdata.organizationCalendars.year')}
                </label>
                <input
                  type="number"
                  className="input"
                  {...register('year', { required: true, valueAsNumber: true })}
                />
              </div>
            </>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="isDefault" {...register('isDefault')} />
            <label htmlFor="isDefault" className="text-sm font-medium text-gray-700">
              {t('masterdata.organizationCalendars.isDefault')}
            </label>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.organizationCalendars.failedToSave')}</p>
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
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.organizationCalendars.editHolidayTitle') : t('masterdata.organizationCalendars.addHolidayTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.date')}</label>
            <input type="date" className="input" {...register('date', { required: true })} />
            {errors.date && <p className="mt-1 text-xs text-red-600">{t('common.date')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div className="flex items-center gap-2">
            <input type="checkbox" id="isRecurringYearly" {...register('isRecurringYearly')} />
            <label htmlFor="isRecurringYearly" className="text-sm font-medium text-gray-700">
              {t('masterdata.organizationCalendars.isRecurringYearly')}
            </label>
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.organizationCalendars.failedToSave')}</p>
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
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setCalendarModalState({ open: true, calendar: c })}
            >
              {t('common.edit')}
            </button>
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
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setHolidayModalState({ open: true, holiday: h })}
            >
              {t('common.edit')}
            </button>
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
          <button className="btn-primary" onClick={() => setCalendarModalState({ open: true, calendar: null })}>
            {t('masterdata.organizationCalendars.add')}
          </button>
        )}
        {canManage && tab === 'holidays' && calendarId && (
          <button className="btn-primary" onClick={() => setHolidayModalState({ open: true, holiday: null })}>
            {t('masterdata.organizationCalendars.addHoliday')}
          </button>
        )}
      </div>

      <div className="flex gap-2 border-b border-gray-200">
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
              tab === x.key ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'calendars' && (
        <>
          <select
            className="input max-w-xs"
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
          </select>
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
          <select
            className="input max-w-xs"
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
          </select>
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
