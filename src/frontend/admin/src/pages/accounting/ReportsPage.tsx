import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { reportsApi } from '@/api/accounting/reports.api'
import type { FinancialAccountLineDto } from '@/api/accounting/reports.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'

function todayISODate(): string {
  return new Date().toISOString().slice(0, 10)
}

function TrialBalancePanel() {
  const { t } = useTranslation()
  const [fiscalPeriodId, setFiscalPeriodId] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [hideZeroBalance, setHideZeroBalance] = useState(true)

  const { data, isLoading, isFetched } = useQuery({
    queryKey: ['accounting', 'reports', 'trial-balance', { fiscalPeriodId, fromDate, toDate, hideZeroBalance }],
    queryFn: () =>
      reportsApi.trialBalance({
        fiscalPeriodId: fiscalPeriodId.trim() || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        hideZeroBalance,
      }),
    staleTime: 15_000,
  })

  const rows = (data ?? []).map((line) => ({ id: line.accountId, ...line }))

  const columns = useMemo(
    () => [
      { key: 'accountNumber', label: t('accounting.reports.accountNumber'), className: 'font-mono text-xs' },
      { key: 'accountName', label: t('accounting.reports.accountName') },
      { key: 'accountType', label: t('accounting.reports.accountType') },
      { key: 'normalBalance', label: t('accounting.reports.normalBalance') },
      {
        key: 'totalDebit',
        label: t('accounting.reports.totalDebit'),
        render: (r: { totalDebit: number }) => r.totalDebit.toLocaleString(),
      },
      {
        key: 'totalCredit',
        label: t('accounting.reports.totalCredit'),
        render: (r: { totalCredit: number }) => r.totalCredit.toLocaleString(),
      },
      {
        key: 'balance',
        label: t('accounting.reports.balance'),
        render: (r: { balance: number }) => r.balance.toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">
            {t('accounting.reports.fiscalPeriodId')}
          </label>
          <input className="input" value={fiscalPeriodId} onChange={(e) => setFiscalPeriodId(e.target.value)} />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.fromDate')}</label>
          <input type="date" className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.toDate')}</label>
          <input type="date" className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <label className="flex items-center gap-2 pb-2 text-sm text-gray-700">
          <input
            type="checkbox"
            checked={hideZeroBalance}
            onChange={(e) => setHideZeroBalance(e.target.checked)}
          />
          {t('accounting.reports.hideZeroBalance')}
        </label>
      </div>

      {(isLoading || isFetched) && <DataTable columns={columns} data={rows} isLoading={isLoading} />}
    </div>
  )
}

function ProfitAndLossPanel() {
  const { t } = useTranslation()
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'reports', 'profit-and-loss', { fromDate, toDate }],
    queryFn: () => reportsApi.profitAndLoss({ fromDate, toDate }),
    enabled: !!fromDate && !!toDate,
    staleTime: 15_000,
  })

  const lineColumns = useMemo(
    () => [
      { key: 'accountNumber', label: t('accounting.reports.accountNumber'), className: 'font-mono text-xs' },
      { key: 'accountName', label: t('accounting.reports.accountName') },
      {
        key: 'amount',
        label: t('accounting.reports.amount'),
        render: (r: { amount: number }) => r.amount.toLocaleString(),
      },
    ],
    [t],
  )

  const toRows = (lines: FinancialAccountLineDto[]) => lines.map((l) => ({ id: l.accountId, ...l }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.fromDate')}</label>
          <input type="date" className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.toDate')}</label>
          <input type="date" className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
      </div>

      {!fromDate || !toDate ? (
        <p className="text-sm text-gray-400">{t('accounting.reports.selectDateRange')}</p>
      ) : isLoading ? (
        <p className="text-sm text-gray-400">{t('common.loading')}</p>
      ) : data ? (
        <div className="space-y-6">
          <div className="grid grid-cols-3 gap-4">
            <div className="card p-4">
              <p className="text-xs uppercase text-gray-500">{t('accounting.reports.totalRevenue')}</p>
              <p className="text-lg font-semibold">{data.totalRevenue.toLocaleString()}</p>
            </div>
            <div className="card p-4">
              <p className="text-xs uppercase text-gray-500">{t('accounting.reports.totalExpenses')}</p>
              <p className="text-lg font-semibold">{data.totalExpenses.toLocaleString()}</p>
            </div>
            <div className="card p-4">
              <p className="text-xs uppercase text-gray-500">{t('accounting.reports.netIncome')}</p>
              <p className="text-lg font-semibold">{data.netIncome.toLocaleString()}</p>
            </div>
          </div>

          <div>
            <h3 className="mb-2 text-sm font-semibold text-gray-800">{t('accounting.reports.revenueLines')}</h3>
            <DataTable columns={lineColumns} data={toRows(data.revenueLines)} />
          </div>
          <div>
            <h3 className="mb-2 text-sm font-semibold text-gray-800">{t('accounting.reports.expenseLines')}</h3>
            <DataTable columns={lineColumns} data={toRows(data.expenseLines)} />
          </div>
        </div>
      ) : null}
    </div>
  )
}

function BalanceSheetPanel() {
  const { t } = useTranslation()
  const [asOfDate, setAsOfDate] = useState(todayISODate())

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'reports', 'balance-sheet', asOfDate],
    queryFn: () => reportsApi.balanceSheet({ asOfDate: asOfDate || undefined }),
    staleTime: 15_000,
  })

  const lineColumns = useMemo(
    () => [
      { key: 'accountNumber', label: t('accounting.reports.accountNumber'), className: 'font-mono text-xs' },
      { key: 'accountName', label: t('accounting.reports.accountName') },
      {
        key: 'amount',
        label: t('accounting.reports.amount'),
        render: (r: { amount: number }) => r.amount.toLocaleString(),
      },
    ],
    [t],
  )

  const toRows = (lines: FinancialAccountLineDto[]) => lines.map((l) => ({ id: l.accountId, ...l }))

  return (
    <div className="space-y-4">
      <div>
        <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.asOfDate')}</label>
        <input
          type="date"
          className="input max-w-xs"
          value={asOfDate}
          onChange={(e) => setAsOfDate(e.target.value)}
        />
      </div>

      {isLoading ? (
        <p className="text-sm text-gray-400">{t('common.loading')}</p>
      ) : data ? (
        <div className="space-y-6">
          <div className="grid grid-cols-3 gap-4">
            <div className="card p-4">
              <p className="text-xs uppercase text-gray-500">{t('accounting.reports.totalAssets')}</p>
              <p className="text-lg font-semibold">{data.totalAssets.toLocaleString()}</p>
            </div>
            <div className="card p-4">
              <p className="text-xs uppercase text-gray-500">{t('accounting.reports.totalLiabilities')}</p>
              <p className="text-lg font-semibold">{data.totalLiabilities.toLocaleString()}</p>
            </div>
            <div className="card p-4">
              <p className="text-xs uppercase text-gray-500">{t('accounting.reports.totalEquity')}</p>
              <p className="text-lg font-semibold">{data.totalEquity.toLocaleString()}</p>
            </div>
          </div>

          <div>
            <h3 className="mb-2 text-sm font-semibold text-gray-800">{t('accounting.reports.assetLines')}</h3>
            <DataTable columns={lineColumns} data={toRows(data.assetLines)} />
          </div>
          <div>
            <h3 className="mb-2 text-sm font-semibold text-gray-800">{t('accounting.reports.liabilityLines')}</h3>
            <DataTable columns={lineColumns} data={toRows(data.liabilityLines)} />
          </div>
          <div>
            <h3 className="mb-2 text-sm font-semibold text-gray-800">{t('accounting.reports.equityLines')}</h3>
            <DataTable columns={lineColumns} data={toRows(data.equityLines)} />
          </div>
        </div>
      ) : null}
    </div>
  )
}

export function ReportsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const canView = hasPermission('accounting:reports:view')
  const [tab, setTab] = useState<'trial-balance' | 'profit-and-loss' | 'balance-sheet'>('trial-balance')

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  const TABS: { key: typeof tab; label: string }[] = [
    { key: 'trial-balance', label: t('accounting.reports.tabTrialBalance') },
    { key: 'profit-and-loss', label: t('accounting.reports.tabProfitAndLoss') },
    { key: 'balance-sheet', label: t('accounting.reports.tabBalanceSheet') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('accounting.reports.title')}</h1>

      <div className="flex gap-2 border-b border-gray-200">
        {TABS.map((x) => (
          <button
            key={x.key}
            type="button"
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'trial-balance' && <TrialBalancePanel />}
      {tab === 'profit-and-loss' && <ProfitAndLossPanel />}
      {tab === 'balance-sheet' && <BalanceSheetPanel />}
    </div>
  )
}
