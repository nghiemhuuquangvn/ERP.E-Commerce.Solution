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
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

type Tab = 'countries' | 'provinces' | 'districts' | 'wards'

function CreateCountryModal({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ code: string; name: string; phoneCode: string }>()

  const mutation = useMutation({
    mutationFn: (v: { code: string; name: string; phoneCode: string }) =>
      geographyApi.createCountry({ code: v.code, name: v.name, phoneCode: v.phoneCode || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'countries'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.geography.addCountry')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-country-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="create-country-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.geography.phoneCode')}>
          <Input {...register('phoneCode')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.geography.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ countryId: string; code: string; name: string }>()

  const mutation = useMutation({
    mutationFn: (v: { countryId: string; code: string; name: string }) =>
      geographyApi.createProvince(v.countryId, { code: v.code, name: v.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'provinces'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.geography.addProvince')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-province-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="create-province-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.geography.country')} required error={errors.countryId ? t('common.required') : undefined}>
          <Select {...register('countryId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {countries.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.geography.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ provinceId: string; code: string; name: string }>()

  const mutation = useMutation({
    mutationFn: (v: { provinceId: string; code: string; name: string }) =>
      geographyApi.createDistrict(v.provinceId, { code: v.code, name: v.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'districts'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.geography.addDistrict')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-district-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="create-district-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.geography.province')} required error={errors.provinceId ? t('common.required') : undefined}>
          <Select {...register('provinceId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {provinces.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.geography.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

function CreateWardModal({ districts, onClose }: { districts: District[]; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<{ districtId: string; code: string; name: string }>()

  const mutation = useMutation({
    mutationFn: (v: { districtId: string; code: string; name: string }) =>
      geographyApi.createWard(v.districtId, { code: v.code, name: v.name }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'geography', 'wards'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('masterdata.geography.addWard')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="create-ward-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="create-ward-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('masterdata.geography.district')} required error={errors.districtId ? t('common.required') : undefined}>
          <Select {...register('districtId', { required: true })}>
            <option value="">{t('common.select')}</option>
            {districts.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
          <Input {...register('code', { required: true })} />
        </FormField>
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.geography.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
          <Button onClick={() => setModalOpen(true)}>
            {TABS.find((x) => x.key === tab)?.addLabel}
          </Button>
        )}
      </div>

      <div className="flex gap-2 border-b border-line">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => switchTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-brand-action text-brand-action'
                : 'text-ink-secondary hover:text-ink'
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
          <Select
            className="max-w-xs"
        aria-label={t('masterdata.geography.selectCountry')}
            value={countryId}
            onChange={(e) => setCountryId(e.target.value)}
          >
            <option value="">{t('masterdata.geography.selectCountry')}</option>
            {(allCountriesQuery.data?.items ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
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
          <Select
            className="max-w-xs"
        aria-label={t('masterdata.geography.selectProvince')}
            value={provinceId}
            onChange={(e) => setProvinceId(e.target.value)}
          >
            <option value="">{t('masterdata.geography.selectProvince')}</option>
            {(allProvincesQuery.data?.items ?? []).map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </Select>
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
          <Select
            className="max-w-xs"
        aria-label={t('masterdata.geography.selectDistrict')}
            value={districtId}
            onChange={(e) => setDistrictId(e.target.value)}
          >
            <option value="">{t('masterdata.geography.selectDistrict')}</option>
            {(allDistrictsQuery.data?.items ?? []).map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
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
