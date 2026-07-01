import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  geographyApi,
  type Country,
  type Province,
  type District,
} from '@/api/masterdata/geography.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

type Tab = 'countries' | 'provinces' | 'districts' | 'wards'

function CreateCountryModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<{ code: string; name: string; phoneCode: string }>()

  const mutation = useMutation({
    mutationFn: (v: { code: string; name: string; phoneCode: string }) =>
      geographyApi.createCountry({ code: v.code, name: v.name, phoneCode: v.phoneCode || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'countries'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('masterdata.geography.addCountry')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.geography.phoneCode')}</label>
            <input className="input" {...register('phoneCode')} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.geography.failedToSave')}</p>
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

function CreateProvinceModal({
  countries,
  onClose,
}: {
  countries: Country[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<{ countryId: string; code: string; name: string }>()

  const mutation = useMutation({
    mutationFn: (v: { countryId: string; code: string; name: string }) =>
      geographyApi.createProvince(v.countryId, { code: v.code, name: v.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'provinces'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('masterdata.geography.addProvince')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.geography.country')}</label>
            <select className="input" {...register('countryId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {countries.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.geography.failedToSave')}</p>
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

function CreateDistrictModal({
  provinces,
  onClose,
}: {
  provinces: Province[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<{ provinceId: string; code: string; name: string }>()

  const mutation = useMutation({
    mutationFn: (v: { provinceId: string; code: string; name: string }) =>
      geographyApi.createDistrict(v.provinceId, { code: v.code, name: v.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'districts'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('masterdata.geography.addDistrict')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.geography.province')}</label>
            <select className="input" {...register('provinceId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {provinces.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.geography.failedToSave')}</p>
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

function CreateWardModal({ districts, onClose }: { districts: District[]; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { register, handleSubmit } = useForm<{ districtId: string; code: string; name: string }>()

  const mutation = useMutation({
    mutationFn: (v: { districtId: string; code: string; name: string }) =>
      geographyApi.createWard(v.districtId, { code: v.code, name: v.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'wards'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('masterdata.geography.addWard')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('masterdata.geography.district')}</label>
            <select className="input" {...register('districtId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {districts.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" {...register('code', { required: true })} />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.geography.failedToSave')}</p>
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

export function GeographyPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('masterdata:geography:manage')
  const [tab, setTab] = useState<Tab>('countries')
  const [modalOpen, setModalOpen] = useState(false)
  const { page, pageSize, setPage, reset } = usePagination()

  const TABS: { key: Tab; label: string; addLabel: string }[] = [
    { key: 'countries', label: t('masterdata.geography.countries'), addLabel: t('masterdata.geography.addCountry') },
    { key: 'provinces', label: t('masterdata.geography.provinces'), addLabel: t('masterdata.geography.addProvince') },
    { key: 'districts', label: t('masterdata.geography.districts'), addLabel: t('masterdata.geography.addDistrict') },
    { key: 'wards', label: t('masterdata.geography.wards'), addLabel: t('masterdata.geography.addWard') },
  ]

  // Selected parents for the cascading Provinces/Districts/Wards tabs.
  const [countryId, setCountryId] = useState('')
  const [provinceId, setProvinceId] = useState('')
  const [districtId, setDistrictId] = useState('')

  const countriesQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'countries', { page, pageSize }],
    queryFn: () => geographyApi.listCountries({ page, pageSize }),
    staleTime: 60_000,
  })

  // Full (unpaginated-ish) country list, used to populate parent pickers on other tabs.
  const allCountriesQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'countries', 'picker'],
    queryFn: () => geographyApi.listCountries({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const provincesQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'provinces', countryId, { page, pageSize }],
    queryFn: () => geographyApi.listProvinces(countryId, { page, pageSize }),
    enabled: !!countryId,
    staleTime: 60_000,
  })

  const allProvincesQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'provinces', 'picker', countryId],
    queryFn: () => geographyApi.listProvinces(countryId, { page: 1, pageSize: 200 }),
    enabled: !!countryId,
    staleTime: 60_000,
  })

  const districtsQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'districts', provinceId, { page, pageSize }],
    queryFn: () => geographyApi.listDistricts(provinceId, { page, pageSize }),
    enabled: !!provinceId,
    staleTime: 60_000,
  })

  const allDistrictsQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'districts', 'picker', provinceId],
    queryFn: () => geographyApi.listDistricts(provinceId, { page: 1, pageSize: 200 }),
    enabled: !!provinceId,
    staleTime: 60_000,
  })

  const wardsQuery = useQuery({
    queryKey: ['masterdata', 'geography', 'wards', districtId, { page, pageSize }],
    queryFn: () => geographyApi.listWards(districtId, { page, pageSize }),
    enabled: !!districtId,
    staleTime: 60_000,
  })

  const countryColumns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'phoneCode', label: t('masterdata.geography.phoneCode'), render: (c: Country) => c.phoneCode ?? '—' },
    ],
    [t],
  )

  const provinceColumns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
    ],
    [t],
  )

  const districtColumns = provinceColumns
  const wardColumns = provinceColumns

  function switchTab(next: Tab) {
    setTab(next)
    reset()
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.geography.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalOpen(true)}>
            {TABS.find((x) => x.key === tab)?.addLabel}
          </button>
        )}
      </div>

      <div className="flex gap-2 border-b border-gray-200">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => switchTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-primary-600 text-primary-700'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'countries' && (
        <>
          <DataTable
            columns={countryColumns}
            data={countriesQuery.data?.items ?? []}
            isLoading={countriesQuery.isLoading}
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={countriesQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {tab === 'provinces' && (
        <>
          <select
            className="input max-w-xs"
            value={countryId}
            onChange={(e) => setCountryId(e.target.value)}
          >
            <option value="">{t('masterdata.geography.selectCountry')}</option>
            {(allCountriesQuery.data?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
          <DataTable
            columns={provinceColumns}
            data={provincesQuery.data?.items ?? []}
            isLoading={provincesQuery.isLoading}
            emptyMessage={countryId ? t('masterdata.geography.noProvinces') : t('masterdata.geography.pickCountryFirst')}
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={provincesQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {tab === 'districts' && (
        <>
          <select
            className="input max-w-xs"
            value={provinceId}
            onChange={(e) => setProvinceId(e.target.value)}
          >
            <option value="">{t('masterdata.geography.selectProvince')}</option>
            {(allProvincesQuery.data?.items ?? []).map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          <DataTable
            columns={districtColumns}
            data={districtsQuery.data?.items ?? []}
            isLoading={districtsQuery.isLoading}
            emptyMessage={
              provinceId ? t('masterdata.geography.noDistricts') : t('masterdata.geography.pickProvinceFirst')
            }
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={districtsQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {tab === 'wards' && (
        <>
          <select
            className="input max-w-xs"
            value={districtId}
            onChange={(e) => setDistrictId(e.target.value)}
          >
            <option value="">{t('masterdata.geography.selectDistrict')}</option>
            {(allDistrictsQuery.data?.items ?? []).map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
          <DataTable
            columns={wardColumns}
            data={wardsQuery.data?.items ?? []}
            isLoading={wardsQuery.isLoading}
            emptyMessage={districtId ? t('masterdata.geography.noWards') : t('masterdata.geography.pickDistrictFirst')}
          />
        </>
      )}

      {modalOpen && tab === 'countries' && (
        <CreateCountryModal onClose={() => setModalOpen(false)} />
      )}
      {modalOpen && tab === 'provinces' && (
        <CreateProvinceModal
          countries={allCountriesQuery.data?.items ?? []}
          onClose={() => setModalOpen(false)}
        />
      )}
      {modalOpen && tab === 'districts' && (
        <CreateDistrictModal
          provinces={allProvincesQuery.data?.items ?? []}
          onClose={() => setModalOpen(false)}
        />
      )}
      {modalOpen && tab === 'wards' && (
        <CreateWardModal
          districts={allDistrictsQuery.data?.items ?? []}
          onClose={() => setModalOpen(false)}
        />
      )}
    </div>
  )
}
