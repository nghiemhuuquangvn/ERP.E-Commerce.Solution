import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { reportsApi, downloadReport } from '@/api/accounting/reports.api'
import type { AmountUnit, FinancialStatement, ReportProfile } from '@/api/accounting/reports.api'
import type { FinancialAccountLineDto, CashFlowSection } from '@/api/accounting/reports.api'
import { accountsApi } from '@/api/accounting/accounts.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'

function todayISODate(): string {
  return new Date().toISOString().slice(0, 10)
}

/** "Export CSV" and "Export Excel" buttons: ask the server for the same report as a file. */
function ExportButton({ path, params, name, disabled, pdf }: { path: string; params: Record<string, unknown>; name: string; disabled?: boolean; pdf?: boolean }) {
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const run = async (format: 'csv' | 'xlsx' | 'pdf') => {
    setBusy(true)
    setFailed(false)
    try {
      await downloadReport(path, params, `${name.replace(/\.csv$/, '')}.${format}`, format)
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }
  return (
    <span className="flex items-center gap-2">
      <button type="button" className="btn-outline" disabled={disabled || busy} onClick={() => run('csv')}>
        {t('accounting.reports.exportCsv')}
      </button>
      <button type="button" className="btn-outline" disabled={disabled || busy} onClick={() => run('xlsx')}>
        {t('accounting.reports.exportExcel')}
      </button>
      {pdf && (
        <button type="button" className="btn-outline" disabled={disabled || busy} onClick={() => run('pdf')}>
          {t('accounting.reports.exportPdf')}
        </button>
      )}
      {failed && <span className="text-xs text-red-600">{t('accounting.reports.exportFailed')}</span>}
    </span>
  )
}

const EMPTY_PROFILE: ReportProfile = {
  companyName: '',
  taxCode: '',
  address: '',
  preparedBy: '',
  chiefAccountant: '',
  director: '',
  amountUnit: 'Vnd',
}

/** The company header, signatories and default amount unit printed on B01 / B02 / B03 and their exports. */
function ReportProfilePanel({ canEdit }: { canEdit: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data } = useQuery({ queryKey: ['accounting', 'reportProfile'], queryFn: () => reportsApi.reportProfile(), staleTime: 30_000 })
  const [draft, setDraft] = useState<ReportProfile | null>(null)
  const value = draft ?? data ?? EMPTY_PROFILE
  const set = (patch: Partial<ReportProfile>) => setDraft({ ...value, ...patch })

  const save = useMutation({
    mutationFn: () => reportsApi.updateReportProfile(value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['accounting', 'reportProfile'] })
      queryClient.invalidateQueries({ queryKey: ['accounting', 'reports', 'statement'] })
      setDraft(null)
    },
  })
  const error = (save.error as { response?: { data?: { detail?: string } } } | null)?.response?.data?.detail

  const field = (key: 'companyName' | 'taxCode' | 'address' | 'preparedBy' | 'chiefAccountant' | 'director', label: string, max: number) => (
    <div>
      <label className="mb-1 block text-sm font-medium text-gray-700">{label}</label>
      <input className="input" maxLength={max} disabled={!canEdit} value={value[key]} onChange={(e) => set({ [key]: e.target.value })} />
    </div>
  )

  return (
    <details className="card p-3">
      <summary className="cursor-pointer text-sm font-semibold text-gray-900">{t('accounting.reports.profileTitle')}</summary>
      <div className="mt-3 grid gap-3 md:grid-cols-2">
        {field('companyName', t('accounting.reports.companyName'), 200)}
        {field('taxCode', t('accounting.reports.taxCode'), 20)}
        {field('address', t('accounting.reports.address'), 300)}
        {field('preparedBy', t('accounting.reports.preparedBy'), 300)}
        {field('chiefAccountant', t('accounting.reports.chiefAccountant'), 300)}
        {field('director', t('accounting.reports.director'), 300)}
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.defaultUnit')}</label>
          <select className="input" disabled={!canEdit} value={value.amountUnit} onChange={(e) => set({ amountUnit: e.target.value as AmountUnit })}>
            <option value="Vnd">{t('accounting.reports.unitVnd')}</option>
            <option value="Thousand">{t('accounting.reports.unitThousand')}</option>
            <option value="Million">{t('accounting.reports.unitMillion')}</option>
          </select>
        </div>
      </div>
      {canEdit && (
        <div className="mt-3 flex items-center gap-3">
          <button className="btn-primary" disabled={!draft || save.isPending} onClick={() => save.mutate()}>
            {t('common.save')}
          </button>
          {save.isError && <span className="text-sm text-red-600">{error ?? t('accounting.reports.profileFailed')}</span>}
        </div>
      )}
    </details>
  )
}

function StatementPanel({ kind }: { kind: 'balance-sheet' | 'income-statement' | 'cash-flow' }) {
  const { t } = useTranslation()
  const [unit, setUnit] = useState<AmountUnit | ''>('') // '' = the report profile's unit
  const [method, setMethod] = useState<'direct' | 'indirect'>('direct') // B03 only
  const isBalanceSheet = kind === 'balance-sheet'
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState(todayISODate())
  const [compare, setCompare] = useState(false)
  const [compareFrom, setCompareFrom] = useState('')
  const [compareTo, setCompareTo] = useState('')

  const ready = isBalanceSheet ? !!toDate : !!fromDate && !!toDate
  const compareReady = compare && (isBalanceSheet ? !!compareTo : !!compareFrom && !!compareTo)
  const params: Record<string, unknown> = isBalanceSheet
    ? { asOfDate: toDate, compareAsOfDate: compareReady ? compareTo : undefined, unit: unit || undefined }
    : { fromDate, toDate, compareFromDate: compareReady ? compareFrom : undefined, compareToDate: compareReady ? compareTo : undefined, unit: unit || undefined, method: kind === 'cash-flow' ? method : undefined }

  const { data, isLoading } = useQuery<FinancialStatement>({
    queryKey: ['accounting', 'reports', 'statement', kind, params],
    queryFn: () =>
      isBalanceSheet
        ? reportsApi.balanceSheetStatement(params)
        : kind === 'cash-flow'
          ? reportsApi.cashFlowStatement(params as { fromDate: string; toDate: string })
          : reportsApi.incomeStatement(params as { fromDate: string; toDate: string }),
    enabled: ready,
    staleTime: 15_000,
  })

  const showPrior = !!data && data.rows.some((r) => r.priorAmount !== null)
  const columns = useMemo(
    () => [
      { key: 'code', label: t('accounting.reports.indicatorCode'), className: 'font-mono text-xs' },
      {
        key: 'name',
        label: t('accounting.reports.indicator'),
        render: (r: { name: string; indent: number; isTotal: boolean }) => (
          <span style={{ paddingLeft: `${(r.indent - 1) * 14}px` }} className={r.isTotal ? 'font-semibold' : ''}>
            {r.name}
          </span>
        ),
      },
      {
        key: 'amount',
        label: t('accounting.reports.amount'),
        render: (r: { amount: number; isTotal: boolean }) => (
          <span className={r.isTotal ? 'font-semibold' : ''}>{r.amount.toLocaleString()}</span>
        ),
      },
      ...(showPrior
        ? [{ key: 'priorAmount', label: t('accounting.reports.prior'), render: (r: { priorAmount: number | null }) => (r.priorAmount ?? 0).toLocaleString() }]
        : []),
    ],
    [t, showPrior],
  )

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        {!isBalanceSheet && (
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.fromDate')}</label>
            <input type="date" className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          </div>
        )}
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">
            {isBalanceSheet ? t('accounting.reports.asOfDate') : t('accounting.reports.toDate')}
          </label>
          <input type="date" className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
        <label className="flex items-center gap-2 pb-2 text-sm text-gray-700">
          <input type="checkbox" checked={compare} onChange={(e) => setCompare(e.target.checked)} />
          {t('accounting.reports.compare')}
        </label>
        {compare && !isBalanceSheet && (
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.compareFrom')}</label>
            <input type="date" className="input" value={compareFrom} onChange={(e) => setCompareFrom(e.target.value)} />
          </div>
        )}
        {compare && (
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {isBalanceSheet ? t('accounting.reports.compareAsOf') : t('accounting.reports.compareTo')}
            </label>
            <input type="date" className="input" value={compareTo} onChange={(e) => setCompareTo(e.target.value)} />
          </div>
        )}
        {kind === 'cash-flow' && (
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.method')}</label>
            <select className="input" value={method} onChange={(e) => setMethod(e.target.value as 'direct' | 'indirect')}>
              <option value="direct">{t('accounting.reports.methodDirect')}</option>
              <option value="indirect">{t('accounting.reports.methodIndirect')}</option>
            </select>
          </div>
        )}
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">{t('accounting.reports.unit')}</label>
          <select className="input" value={unit} onChange={(e) => setUnit(e.target.value as AmountUnit | '')}>
            <option value="">{t('accounting.reports.unitProfile')}</option>
            <option value="Vnd">{t('accounting.reports.unitVnd')}</option>
            <option value="Thousand">{t('accounting.reports.unitThousand')}</option>
            <option value="Million">{t('accounting.reports.unitMillion')}</option>
          </select>
        </div>
        <ExportButton
          path={`statements/${kind}`}
          params={params}
          name={`${kind}.csv`}
          disabled={!ready}
          pdf
        />
      </div>

      {data?.header && (
        <div className="text-sm text-gray-700">
          <p className="font-semibold">{data.header.companyName}</p>
          {data.header.taxCode && <p>{t('accounting.reports.taxCode')}: {data.header.taxCode}</p>}
          {data.header.address && <p>{data.header.address}</p>}
        </div>
      )}

      {data && (
        <p className="text-xs text-gray-500">
          {t('accounting.reports.unit')}: {data.unit}
        </p>
      )}

      {data && (
        <p className="text-xs text-amber-700">
          {data.title}
          {data.isDraft ? ` — ${t('accounting.reports.draftWarning')}` : ''}
        </p>
      )}

      {ready && (
        <DataTable
          columns={columns}
          data={(data?.rows ?? []).map((r) => ({ id: r.code, ...r }))}
          isLoading={isLoading}
        />
      )}

      {data?.isBalanced !== null && data?.isBalanced !== undefined && (
        <p className={data.isBalanced ? 'text-sm text-green-700' : 'text-sm font-semibold text-red-700'}>
          {data.isBalanced ? t('accounting.reports.balanced') : t('accounting.reports.notBalanced')}
        </p>
      )}

      {data && data.unmapped.length > 0 && (
        <div className="card space-y-1 border border-amber-300 p-3 text-sm">
          <p className="font-semibold text-amber-800">{t('accounting.reports.unmappedTitle')}</p>
          {data.unmapped.map((u) => (
            <p key={u.accountNumber} className="font-mono text-xs">
              {u.accountNumber} {u.accountName}: {u.balance.toLocaleString()}
            </p>
          ))}
        </div>
      )}

      {data?.header && (data.header.preparedBy || data.header.chiefAccountant || data.header.director) && (
        <div className="grid grid-cols-3 gap-4 pt-4 text-center text-sm">
          <div><p className="text-xs text-gray-500">{t('accounting.reports.preparedBy')}</p><p>{data.header.preparedBy}</p></div>
          <div><p className="text-xs text-gray-500">{t('accounting.reports.chiefAccountant')}</p><p>{data.header.chiefAccountant}</p></div>
          <div><p className="text-xs text-gray-500">{t('accounting.reports.director')}</p><p>{data.header.director}</p></div>
        </div>
      )}

      {data && data.notes.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-xs text-gray-500">
          {data.notes.map((n) => (
            <li key={n}>{n}</li>
          ))}
        </ul>
      )}
    </div>
  )
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
        <ExportButton
          path="trial-balance/full"
          params={{
            fiscalPeriodId: fiscalPeriodId.trim() || undefined,
            fromDate: fromDate || undefined,
            toDate: toDate || undefined,
            hideZeroBalance,
            rollUp,
          }}
          name="trial-balance.csv"
        />
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
        <ExportButton
          path="account-ledger"
          params={{ accountId, fromDate: fromDate || undefined, toDate: toDate || undefined, includeSubAccounts }}
          name="account-ledger.csv"
          disabled={!accountId}
        />
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
        <ExportButton path="cash-flow" params={{ fromDate, toDate }} name="cash-flow.csv" disabled={!fromDate || !toDate} />
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
  const [tab, setTab] = useState<'trial-balance' | 'ledger' | 'profit-and-loss' | 'balance-sheet' | 'cash-flow' | 'b01' | 'b02' | 'b03'>('trial-balance')

  if (!canView) {
    return <p className="text-sm text-gray-400">{t('common.noPermission')}</p>
  }

  const TABS: { key: typeof tab; label: string }[] = [
    { key: 'trial-balance', label: t('accounting.reports.tabTrialBalance') },
    { key: 'ledger', label: t('accounting.reports.tabLedger') },
    { key: 'profit-and-loss', label: t('accounting.reports.tabProfitAndLoss') },
    { key: 'balance-sheet', label: t('accounting.reports.tabBalanceSheet') },
    { key: 'cash-flow', label: t('accounting.reports.tabCashFlow') },
    { key: 'b01', label: t('accounting.reports.tabB01') },
    { key: 'b02', label: t('accounting.reports.tabB02') },
    { key: 'b03', label: t('accounting.reports.tabB03') },
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
      {(tab === 'b01' || tab === 'b02' || tab === 'b03') && <ReportProfilePanel canEdit={hasPermission('accounting:accounts:manage')} />}
      {tab === 'b01' && <StatementPanel kind="balance-sheet" />}
      {tab === 'b02' && <StatementPanel kind="income-statement" />}
      {tab === 'b03' && <StatementPanel kind="cash-flow" />}
    </div>
  )
}
