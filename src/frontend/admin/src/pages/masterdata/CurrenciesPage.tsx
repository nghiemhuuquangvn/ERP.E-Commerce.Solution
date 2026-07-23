import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  currenciesApi,
  type Currency,
  type ExchangeRate,
} from '@/api/masterdata/currencies.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'

type Tab = 'currencies' | 'exchangeRates'

interface CurrencyFormValues {
  code: string
  name: string
  symbol: string
  decimalPlaces: number
}

function CurrencyModal({
  currency,
  onClose,
}: {
  currency: Currency | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!currency

  const { register, handleSubmit, formState: { errors } } = useForm<CurrencyFormValues>({
    defaultValues: {
      code: currency?.code ?? '',
      name: currency?.name ?? '',
      symbol: currency?.symbol ?? '',
      decimalPlaces: currency?.decimalPlaces ?? 2,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: CurrencyFormValues) => {
      if (isEdit) {
        return currenciesApi.update(currency!.id, {
          name: values.name,
          symbol: values.symbol,
          decimalPlaces: Number(values.decimalPlaces),
        })
      }
      return currenciesApi.create({
        code: values.code,
        name: values.name,
        symbol: values.symbol,
        decimalPlaces: Number(values.decimalPlaces),
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'currencies'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('masterdata.currencies.editTitle') : t('masterdata.currencies.addTitle')}
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
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.currencies.symbol')}
            </label>
            <input className="input" {...register('symbol', { required: true })} />
            {errors.symbol && (
              <p className="mt-1 text-xs text-red-600">{t('masterdata.currencies.symbol')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.currencies.decimalPlaces')}
            </label>
            <input
              type="number"
              className="input"
              {...register('decimalPlaces', { required: true, valueAsNumber: true })}
            />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.currencies.failedToSave')}</p>
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

interface ExchangeRateFormValues {
  toCurrencyId: string
  rate: number
  effectiveDate: string
}

function ExchangeRateModal({
  fromCurrencyId,
  exchangeRate,
  currencies,
  onClose,
}: {
  fromCurrencyId: string
  exchangeRate: ExchangeRate | null
  currencies: Currency[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!exchangeRate

  const { register, handleSubmit, formState: { errors } } = useForm<ExchangeRateFormValues>({
    defaultValues: {
      toCurrencyId: exchangeRate?.toCurrencyId ?? '',
      rate: exchangeRate?.rate ?? 0,
      effectiveDate: exchangeRate?.effectiveDate ? exchangeRate.effectiveDate.slice(0, 10) : '',
    },
  })

  const mutation = useMutation({
    mutationFn: (values: ExchangeRateFormValues) => {
      if (isEdit) {
        return currenciesApi.updateExchangeRate(fromCurrencyId, exchangeRate!.id, {
          rate: Number(values.rate),
          effectiveDate: values.effectiveDate,
        })
      }
      return currenciesApi.createExchangeRate(fromCurrencyId, {
        toCurrencyId: values.toCurrencyId,
        rate: Number(values.rate),
        effectiveDate: values.effectiveDate,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'exchangeRates', fromCurrencyId] })
      onClose()
    },
  })

  const toCurrencies = currencies.filter((c) => c.id !== fromCurrencyId)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit
            ? t('masterdata.currencies.editExchangeRateTitle')
            : t('masterdata.currencies.addExchangeRateTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">
                {t('masterdata.currencies.toCurrency')}
              </label>
              <select className="input" {...register('toCurrencyId', { required: true })}>
                <option value="">{t('common.select')}</option>
                {toCurrencies.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.code} — {c.name}
                  </option>
                ))}
              </select>
              {errors.toCurrencyId && (
                <p className="mt-1 text-xs text-red-600">
                  {t('masterdata.currencies.toCurrency')} is required.
                </p>
              )}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.currencies.rate')}
            </label>
            <input
              type="number"
              step="0.000001"
              className="input"
              {...register('rate', { required: true, valueAsNumber: true })}
            />
            {errors.rate && (
              <p className="mt-1 text-xs text-red-600">{t('masterdata.currencies.rate')} is required.</p>
            )}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('masterdata.currencies.effectiveDate')}
            </label>
            <input type="date" className="input" {...register('effectiveDate', { required: true })} />
            {errors.effectiveDate && (
              <p className="mt-1 text-xs text-red-600">
                {t('masterdata.currencies.effectiveDate')} is required.
              </p>
            )}
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('masterdata.currencies.failedToSave')}</p>
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

function EffectiveRateLookupPanel({ currencies }: { currencies: Currency[] }) {
  const { t } = useTranslation()
  const [fromCurrencyId, setFromCurrencyId] = useState('')
  const [toCurrencyId, setToCurrencyId] = useState('')
  const [asOfDate, setAsOfDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [result, setResult] = useState<ExchangeRate | null | undefined>(undefined)

  const lookupMutation = useMutation({
    mutationFn: () => currenciesApi.getEffectiveRate(fromCurrencyId, toCurrencyId, asOfDate),
    onSuccess: (data) => setResult(data),
  })

  const canLookup = !!fromCurrencyId && !!toCurrencyId && !!asOfDate

  return (
    <div className="card space-y-3 p-4">
      <h2 className="text-sm font-semibold text-gray-900">
        {t('masterdata.currencies.effectiveRateLookup')}
      </h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-700">
            {t('masterdata.currencies.fromCurrency')}
          </label>
          <select
            className="input"
            value={fromCurrencyId}
            onChange={(e) => {
              setFromCurrencyId(e.target.value)
              setResult(undefined)
            }}
          >
            <option value="">{t('common.select')}</option>
            {currencies.map((c) => (
              <option key={c.id} value={c.id}>
                {c.code} — {c.name}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-700">
            {t('masterdata.currencies.toCurrency')}
          </label>
          <select
            className="input"
            value={toCurrencyId}
            onChange={(e) => {
              setToCurrencyId(e.target.value)
              setResult(undefined)
            }}
          >
            <option value="">{t('common.select')}</option>
            {currencies.map((c) => (
              <option key={c.id} value={c.id}>
                {c.code} — {c.name}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-700">
            {t('masterdata.currencies.asOfDate')}
          </label>
          <input
            type="date"
            className="input"
            value={asOfDate}
            onChange={(e) => {
              setAsOfDate(e.target.value)
              setResult(undefined)
            }}
          />
        </div>
        <div className="flex items-end">
          <button
            type="button"
            className="btn-primary w-full"
            disabled={!canLookup || lookupMutation.isPending}
            onClick={() => lookupMutation.mutate()}
          >
            {lookupMutation.isPending ? t('common.loading') : t('masterdata.currencies.lookUp')}
          </button>
        </div>
      </div>
      {result !== undefined && (
        <p className="text-sm">
          {result ? (
            <span className="text-gray-900">
              {t('masterdata.currencies.rate')}: <span className="font-semibold">{result.rate}</span>{' '}
              ({t('masterdata.currencies.effectiveDate')}: {result.effectiveDate.slice(0, 10)})
            </span>
          ) : (
            <span className="text-gray-500">{t('masterdata.currencies.noRateFound')}</span>
          )}
        </p>
      )}
    </div>
  )
}

export function CurrenciesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canManage = hasPermission('masterdata:currencies:manage')
  const [tab, setTab] = useState<Tab>('currencies')
  const { page, pageSize, setPage, reset } = usePagination()

  const [fromCurrencyId, setFromCurrencyId] = useState('')

  const [currencyModalState, setCurrencyModalState] = useState<{ open: boolean; currency: Currency | null }>({
    open: false,
    currency: null,
  })
  const [exchangeRateModalState, setExchangeRateModalState] = useState<{
    open: boolean
    exchangeRate: ExchangeRate | null
  }>({ open: false, exchangeRate: null })
  const [confirmTarget, setConfirmTarget] = useState<Currency | null>(null)

  const queryClient = useQueryClient()

  const currenciesQuery = useQuery({
    queryKey: ['masterdata', 'currencies', { page, pageSize }],
    queryFn: () => currenciesApi.list({ page, pageSize }),
    staleTime: 30_000,
  })

  const allCurrenciesQuery = useQuery({
    queryKey: ['masterdata', 'currencies', 'picker'],
    queryFn: () => currenciesApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })

  const exchangeRatesQuery = useQuery({
    queryKey: ['masterdata', 'exchangeRates', fromCurrencyId, { page, pageSize }],
    queryFn: () => currenciesApi.listExchangeRates(fromCurrencyId, { page, pageSize }),
    enabled: !!fromCurrencyId,
    staleTime: 30_000,
  })

  const currencies = allCurrenciesQuery.data?.items ?? []

  const toggleMutation = useMutation({
    mutationFn: (c: Currency) => (c.isActive ? currenciesApi.deactivate(c.id) : currenciesApi.activate(c.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['masterdata', 'currencies'] })
      setConfirmTarget(null)
    },
  })

  const currencyColumns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'symbol', label: t('masterdata.currencies.symbol') },
      { key: 'decimalPlaces', label: t('masterdata.currencies.decimalPlaces') },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (c: Currency) => (
          <StatusBadge
            status={c.isActive ? 'Active' : 'Inactive'}
            label={c.isActive ? t('masterdata.currencies.active') : t('masterdata.currencies.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (c: Currency) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setCurrencyModalState({ open: true, currency: c })}
              >
                {t('common.edit')}
              </button>
              {c.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(c)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(c)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, t],
  )

  const exchangeRateColumns = useMemo(
    () => [
      {
        key: 'toCurrencyId',
        label: t('masterdata.currencies.toCurrency'),
        render: (er: ExchangeRate) => currencies.find((c) => c.id === er.toCurrencyId)?.code ?? er.toCurrencyId,
      },
      { key: 'rate', label: t('masterdata.currencies.rate') },
      {
        key: 'effectiveDate',
        label: t('masterdata.currencies.effectiveDate'),
        render: (er: ExchangeRate) => er.effectiveDate.slice(0, 10),
      },
      {
        key: 'actions',
        label: '',
        render: (er: ExchangeRate) =>
          canManage ? (
            <button
              className="text-xs text-primary-600 hover:underline"
              onClick={() => setExchangeRateModalState({ open: true, exchangeRate: er })}
            >
              {t('common.edit')}
            </button>
          ) : null,
      },
    ],
    [canManage, currencies, t],
  )

  function switchTab(next: Tab) {
    setTab(next)
    reset()
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('masterdata.currencies.title')}</h1>
        {canManage && tab === 'currencies' && (
          <button className="btn-primary" onClick={() => setCurrencyModalState({ open: true, currency: null })}>
            {t('masterdata.currencies.add')}
          </button>
        )}
        {canManage && tab === 'exchangeRates' && fromCurrencyId && (
          <button
            className="btn-primary"
            onClick={() => setExchangeRateModalState({ open: true, exchangeRate: null })}
          >
            {t('masterdata.currencies.addExchangeRate')}
          </button>
        )}
      </div>

      <div className="flex gap-2 border-b border-gray-200">
        {(
          [
            { key: 'currencies' as const, label: t('masterdata.currencies.currenciesTab') },
            { key: 'exchangeRates' as const, label: t('masterdata.currencies.exchangeRatesTab') },
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

      {tab === 'currencies' && (
        <>
          <DataTable
            columns={currencyColumns}
            data={currenciesQuery.data?.items ?? []}
            isLoading={currenciesQuery.isLoading}
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={currenciesQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {tab === 'exchangeRates' && (
        <>
          <EffectiveRateLookupPanel currencies={currencies} />

          <select
            className="input max-w-xs"
            value={fromCurrencyId}
            onChange={(e) => {
              setFromCurrencyId(e.target.value)
              setPage(1)
            }}
          >
            <option value="">{t('masterdata.currencies.selectCurrency')}</option>
            {currencies.map((c) => (
              <option key={c.id} value={c.id}>
                {c.code} — {c.name}
              </option>
            ))}
          </select>
          <DataTable
            columns={exchangeRateColumns}
            data={exchangeRatesQuery.data?.items ?? []}
            isLoading={exchangeRatesQuery.isLoading}
            emptyMessage={
              fromCurrencyId
                ? t('masterdata.currencies.noExchangeRates')
                : t('masterdata.currencies.pickCurrencyFirst')
            }
          />
          <Pagination
            page={page}
            pageSize={pageSize}
            totalCount={exchangeRatesQuery.data?.totalCount ?? 0}
            onPageChange={setPage}
          />
        </>
      )}

      {currencyModalState.open && (
        <CurrencyModal
          currency={currencyModalState.currency}
          onClose={() => setCurrencyModalState({ open: false, currency: null })}
        />
      )}

      {exchangeRateModalState.open && fromCurrencyId && (
        <ExchangeRateModal
          fromCurrencyId={fromCurrencyId}
          exchangeRate={exchangeRateModalState.exchangeRate}
          currencies={currencies}
          onClose={() => setExchangeRateModalState({ open: false, exchangeRate: null })}
        />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('masterdata.currencies.deactivateTitle')}
        message={t('masterdata.currencies.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
