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
import { Alert, Button, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('masterdata.currencies.editTitle') : t('masterdata.currencies.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="currency-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="currency-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
            <Input {...register('code', { required: true })} />
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.currencies.symbol')} required error={errors.symbol ? t('common.required') : undefined}>
          <Input {...register('symbol', { required: true })} />
        </FormField>
        <FormField label={t('masterdata.currencies.decimalPlaces')} required error={errors.decimalPlaces ? t('common.required') : undefined}>
          <Input
            type="number"
            {...register('decimalPlaces', { required: true, valueAsNumber: true })}
          />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.currencies.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const toCurrencies = currencies.filter((c) => c.id !== fromCurrencyId)

  return (
    <Modal
      open
      size="sm"
      title={isEdit
            ? t('masterdata.currencies.editExchangeRateTitle')
            : t('masterdata.currencies.addExchangeRateTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="exchange-rate-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="exchange-rate-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('masterdata.currencies.toCurrency')} required error={errors.toCurrencyId ? t('common.required') : undefined}>
            <Select {...register('toCurrencyId', { required: true })}>
              <option value="">{t('common.select')}</option>
              {toCurrencies.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.code} — {c.name}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        <FormField label={t('masterdata.currencies.rate')} required error={errors.rate ? t('common.required') : undefined}>
          <Input
            type="number"
            step="0.000001"
            {...register('rate', { required: true, valueAsNumber: true })}
          />
        </FormField>
        <FormField label={t('masterdata.currencies.effectiveDate')} required error={errors.effectiveDate ? t('common.required') : undefined}>
          <Input type="date" {...register('effectiveDate', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('masterdata.currencies.failedToSave'))}</Alert>}
      </form>
    </Modal>
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
      <h2 className="text-sm font-semibold text-ink">
        {t('masterdata.currencies.effectiveRateLookup')}
      </h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-4">
        <FormField label={t('masterdata.currencies.fromCurrency')}>
          <Select
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
          </Select>
        </FormField>
        <FormField label={t('masterdata.currencies.toCurrency')}>
          <Select
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
          </Select>
        </FormField>
        <FormField label={t('masterdata.currencies.asOfDate')}>
          <Input
            type="date"
            value={asOfDate}
            onChange={(e) => {
              setAsOfDate(e.target.value)
              setResult(undefined)
            }}
          />
        </FormField>
        <div className="flex items-end">
          <Button fullWidth
            type="button"
            disabled={!canLookup || lookupMutation.isPending}
            onClick={() => lookupMutation.mutate()}>
            {lookupMutation.isPending ? t('common.loading') : t('masterdata.currencies.lookUp')}
          </Button>
        </div>
      </div>
      {result !== undefined && (
        <p className="text-sm">
          {result ? (
            <span className="text-ink">
              {t('masterdata.currencies.rate')}: <span className="font-semibold">{result.rate}</span>{' '}
              ({t('masterdata.currencies.effectiveDate')}: {result.effectiveDate.slice(0, 10)})
            </span>
          ) : (
            <span className="text-ink-secondary">{t('masterdata.currencies.noRateFound')}</span>
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
              <Button variant="link" className="text-xs"
                onClick={() => setCurrencyModalState({ open: true, currency: c })}>
                {t('common.edit')}
              </Button>
              {c.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(c)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs"
                  onClick={() => toggleMutation.mutate(c)}>
                  {t('common.activate')}
                </Button>
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
            <Button variant="link" className="text-xs"
              onClick={() => setExchangeRateModalState({ open: true, exchangeRate: er })}>
              {t('common.edit')}
            </Button>
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
          <Button onClick={() => setCurrencyModalState({ open: true, currency: null })}>
            {t('masterdata.currencies.add')}
          </Button>
        )}
        {canManage && tab === 'exchangeRates' && fromCurrencyId && (
          <Button
            onClick={() => setExchangeRateModalState({ open: true, exchangeRate: null })}>
            {t('masterdata.currencies.addExchangeRate')}
          </Button>
        )}
      </div>

      <div className="flex gap-2 border-b border-line">
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
              tab === x.key ? 'border-b-2 border-brand-action text-brand-action' : 'text-ink-secondary hover:text-ink'
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

          <Select
            className="max-w-xs"
        aria-label={t('masterdata.currencies.selectCurrency')}
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
          </Select>
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
