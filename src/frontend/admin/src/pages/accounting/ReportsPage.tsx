import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { reportsApi } from '@/api/accounting/reports.api'
import type { FinancialAccountLineDto, CashFlowSection } from '@/api/accounting/reports.api'
import { accountsApi } from '@/api/accounting/accounts.api'
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
  const [rollUp, setRollUp] = useState(true)

  const { data, isLoading, isFetched } = useQuery({
    queryKey: ['accounting', 'reports', 'trial-balance-full', { fiscalPeriodId, fromDate, toDate, hideZeroBalance, rollUp }],
    queryFn: () =>
      reportsApi.trialBalanceFull({
        fiscalPeriodId: fiscalPeriodId.trim() || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        hideZeroBalance,
        rollUp,
      }),
    staleTime: 15_000,
  })

  const rows = (data?.lines ?? []).map((line) => ({ id: line.accountId, ...line }))
  const totals = data?.totals
  const n = (v: number) => (v === 0 ? '' : v.toLocaleString())

  const columns = useMemo(
    () => [
      {
        key: 'accountNumber',
        label: t('accounting.reports.accountNumber'),
        className: 'font-mono text-xs',
        render: (r: { accountNumber: string; level: number; isParent: boolean }) => (
          <span style={{ paddingLeft: `${(r.level - 1) * 16}px` }} className={r.isParent ? 'font-semibold' : ''}>
            {r.accountNumber}
          </span>
        ),
      },
      { key: 'accountName', label: t('accounting.reports.accountName') },
      { key: 'openingDebit', label: t('accounting.reports.openingDebit'), render: (r: { openingDebit: number }) => n(r.openingDebit) },
      { key: 'openingCredit', label: t('accounting.reports.openingCredit'), render: (r: { openingCredit: number }) => n(r.openingCredit) },
      { key: 'periodDebit', label: t('accounting.reports.periodDebit'), render: (r: { periodDebit: number }) => n(r.periodDebit) },
      { key: 'periodCredit', label: t('accounting.reports.periodCredit'), render: (r: { periodCredit: number }) => n(r.periodCredit) },
      { key: 'closingDebit', label: t('accounting.reports.closingDebit'), render: (r: { closingDebit: number }) => n(r.closingDebit) },
      { key: 'closingCredit', label: t('accounting.reports.closingCredit'), render: (r: { closingCredit: number }) => n(r.closingCredit) },
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
        <label className="flex items-center gap-2 pb-2 text-sm text-gray-700">
          <input type="checkbox" checked={rollUp} onChange={(e) => setRollUp(e.target.checked)} />
          {t('accounting.reports.rollUp')}
        </label>
      </div>

      {(isLoading || isFetched) && <DataTable columns={columns} data={rows} isLoading={isLoading} />}

      {totals && (
        <div className="card flex flex-wrap items-center gap-x-6 gap-y-1 p-3 text-sm">
          <span className="font-semibold">{t('accounting.reports.totals')}</span>
          <span>{t('accounting.reports.openingDebit')}: {totals.openingDebit.toLocaleString()}</span>
          <span>{t('accounting.reports.openingCredit')}: {totals.openingCredit.toLocaleString()}</span>
          <span>{t('accounting.reports.periodDebit')}: {totals.periodDebit.toLocaleString()}</span>
          <span>{t('accounting.reports.periodCredit')}: {totals.periodCredit.toLocaleString()}</span>
          <span>{t('accounting.reports.closingDebit')}: {totals.closingDebit.toLocaleString()}</span>
          <span>{t('accounting.reports.closingCredit')}: {totals.closingCredit.toLocaleString()}</span>
          <span className={totals.isBalanced ? 'text-green-700' : 'font-semibold text-red-700'}>
            {totals.isBalanced ? t('accounting.reports.balanced') : t('accounting.reports.notBalanced')}
          </span>
        </div>
      )}
    </div>
  )
}

function LedgerPanel() {
  const { t } = useTranslation()
  const [accountId, setAccountId] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [includeSubAccounts, setIncludeSubAccounts] = useState(true)
  const [pageNumber, setPageNumber] = useState(1)
  const PAGE_SIZE = 100

  const { data: accounts } = useQuery({
    queryKey: ['accounting', 'accounts'],
    queryFn: () => accountsApi.list(),
    staleTime: 60_000,
  })

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'reports', 'ledger', { accountId, fromDate, toDate, includeSubAccounts, pageNumber }],
    queryFn: () =>
      reportsApi.accountLedger({
        accountId,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        includeSubAccounts,
        pageNumber,
        pageSize: PAGE_SIZE,
      }),
    enabled: !!accountId,
    staleTime: 15_000,
  })

  const columns = useMemo(
    () => [
      { key: 'entryDate', label: t('accounting.reports.date'), render: (r: { entryDate: string }) => new Date(r.entryDate).toLocaleDateString() },
      { key: 'entryNumber', label: t('accounting.journalEntries.entryNumber'), className: 'font-mono text-xs' },
      { key: 'description', label: t('accounting.journalEntries.description') },
      { key: 'accountNumber', label: t('accounting.reports.accountNumber'), className: 'font-mono text-xs' },
      { key: 'counterAccounts', label: t('accounting.reports.counterAccounts'), className: 'font-mono text-xs' },
      { key: 'debit', label: t('accounting.reports.debit'), render: (r: { debit: number }) => (r.debit ? r.debit.toLocaleString() : '') },
      { key: 'credit', label: t('accounting.reports.credit'), render: (r: { credit: number }) => (r.credit ? r.credit.toLocaleString() : '') },
      { key: 'balance', label: t('accounting.reports.balance'), render: (r: { balance: number }) => r.balance.toLocaleString() },
    ],
    [t],
  )

  const rows = (data?.lines ?? []).map((l, i) => ({ id: `${l.entryId}-${i}`, ...l }))

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.account')}</label>
          <select className="input" value={accountId} onChange={(e) => { setAccountId(e.target.value); setPageNumber(1) }}>
            <option value="">{t('accounting.reports.chooseAccount')}</option>
            {(accounts ?? []).map((a) => (
              <option key={a.id} value={a.id}>
                {a.accountNumber} — {a.name}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.fromDate')}</label>
          <input type="date" className="input" value={fromDate} onChange={(e) => { setFromDate(e.target.value); setPageNumber(1) }} />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.toDate')}</label>
          <input type="date" className="input" value={toDate} onChange={(e) => { setToDate(e.target.value); setPageNumber(1) }} />
        </div>
        <label className="flex items-center gap-2 pb-2 text-sm text-gray-700">
          <input type="checkbox" checked={includeSubAccounts} onChange={(e) => setIncludeSubAccounts(e.target.checked)} />
          {t('accounting.reports.includeSubAccounts')}
        </label>
      </div>

      {data && (
        <div className="card flex flex-wrap items-center gap-x-6 gap-y-1 p-3 text-sm">
          <span className="font-semibold">{data.accountNumber} — {data.accountName}</span>
          <span>{t('accounting.reports.openingBalance')}: {data.openingBalance.toLocaleString()}</span>
          <span>{t('accounting.reports.debit')}: {data.totalDebit.toLocaleString()}</span>
          <span>{t('accounting.reports.credit')}: {data.totalCredit.toLocaleString()}</span>
          <span>{t('accounting.reports.closingBalance')}: {data.closingBalance.toLocaleString()}</span>
        </div>
      )}

      {accountId && <DataTable columns={columns} data={rows} isLoading={isLoading} />}

      {data && data.totalRows > PAGE_SIZE && (
        <div className="flex justify-end gap-2">
          <button className="btn-outline" disabled={pageNumber === 1} onClick={() => setPageNumber((p) => Math.max(1, p - 1))}>
            {t('common.prev')}
          </button>
          <button className="btn-outline" disabled={pageNumber * PAGE_SIZE >= data.totalRows} onClick={() => setPageNumber((p) => p + 1)}>
            {t('common.next')}
          </button>
        </div>
      )}
    </div>
  )
}

function CashFlowPanel() {
  const { t } = useTranslation()
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['accounting', 'reports', 'cash-flow', { fromDate, toDate }],
    queryFn: () => reportsApi.cashFlow({ fromDate, toDate }),
    enabled: !!fromDate && !!toDate,
    staleTime: 15_000,
  })

  const columns = useMemo(
    () => [
      { key: 'accountNumber', label: t('accounting.reports.accountNumber'), className: 'font-mono text-xs' },
      { key: 'accountName', label: t('accounting.reports.accountName') },
      { key: 'inflow', label: t('accounting.reports.inflow'), render: (r: { inflow: number }) => (r.inflow ? r.inflow.toLocaleString() : '') },
      { key: 'outflow', label: t('accounting.reports.outflow'), render: (r: { outflow: number }) => (r.outflow ? r.outflow.toLocaleString() : '') },
      { key: 'net', label: t('accounting.reports.net'), render: (r: { net: number }) => r.net.toLocaleString() },
    ],
    [t],
  )

  const section = (key: 'operating' | 'investing' | 'financing', s: CashFlowSection) => (
    <div key={key}>
      <h3 className="mb-2 text-sm font-semibold text-gray-800">
        {t(`accounting.reports.cashFlow_${key}`)} — {s.net.toLocaleString()}
      </h3>
      <DataTable columns={columns} data={s.lines.map((l) => ({ id: l.accountId, ...l }))} />
    </div>
  )

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

      {isLoading && <p className="text-sm text-gray-400">{t('common.loading')}</p>}
      {data && (
        <div className="space-y-4">
          {data.cashAccountCount === 0 && <p className="text-sm text-amber-700">{t('accounting.reports.noCashAccounts')}</p>}
          <div className="card flex flex-wrap items-center gap-x-6 gap-y-1 p-3 text-sm">
            <span>{t('accounting.reports.openingCash')}: {data.openingCash.toLocaleString()}</span>
            <span>{t('accounting.reports.netChange')}: {data.netChange.toLocaleString()}</span>
            <span>{t('accounting.reports.closingCash')}: {data.closingCash.toLocaleString()}</span>
            <span className={data.isReconciled ? 'text-green-700' : 'font-semibold text-red-700'}>
              {data.isReconciled
                ? t('accounting.reports.reconciled')
                : t('accounting.reports.notReconciled', { difference: data.difference.toLocaleString() })}
            </span>
          </div>
          {section('operating', data.operating)}
          {section('investing', data.investing)}
          {section('financing', data.financing)}
          <p className="text-xs text-gray-500">{t('accounting.reports.cashFlowNote')}</p>
        </div>
      )}
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
  const [tab, setTab] = useState<'trial-balance' | 'ledger' | 'profit-and-loss' | 'balance-sheet' | 'cash-flow'>('trial-balance')

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  const TABS: { key: typeof tab; label: string }[] = [
    { key: 'trial-balance', label: t('accounting.reports.tabTrialBalance') },
    { key: 'ledger', label: t('accounting.reports.tabLedger') },
    { key: 'profit-and-loss', label: t('accounting.reports.tabProfitAndLoss') },
    { key: 'balance-sheet', label: t('accounting.reports.tabBalanceSheet') },
    { key: 'cash-flow', label: t('accounting.reports.tabCashFlow') },
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
      {tab === 'ledger' && <LedgerPanel />}
      {tab === 'profit-and-loss' && <ProfitAndLossPanel />}
      {tab === 'balance-sheet' && <BalanceSheetPanel />}
      {tab === 'cash-flow' && <CashFlowPanel />}
    </div>
  )
}
