import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { posApi } from '@/api/pos/pos.api'
import type { CashShift } from '@/api/pos/pos.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button, EmptyState, Input, Modal, Select, Skeleton } from '@/components/ui'
import { usePagination } from '@/hooks/usePagination'
import { TERMINALS_KEY } from './PosTerminalsPage'

const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`
const short = (id: string) => id.slice(0, 8)

function ShiftDetail({ shift, terminalName, onClose }: { shift: CashShift; terminalName: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { data: summary, isLoading } = useQuery({ queryKey: ['pos', 'shiftSummary', shift.id], queryFn: () => posApi.shiftSummary(shift.id) })
  const expected = shift.openingBalance + (summary?.salesByPaymentMethod?.Cash ?? 0)
  const diff = shift.closingBalance != null ? shift.closingBalance - expected : null

  const denominations = (title: string, rows: CashShift['openingDenominations']) =>
    rows.length > 0 && (
      <div>
        <h4 className="mb-1 text-xs font-semibold uppercase text-ink-secondary">{title}</h4>
        <table className="text-sm">
          <tbody>
            {rows.map((d) => (
              <tr key={d.denomination}><td className="pr-4">{money(d.denomination)}</td><td className="pr-4">× {d.count}</td><td>{money(d.lineTotal)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    )

  return (
    <Modal
      open
      size="lg"
      title={t('pos.shifts.detailTitle', { terminal: terminalName })}
      onClose={onClose}
      dismissOnBackdrop
      footer={<Button variant="outline" onClick={onClose}>{t('common.close')}</Button>}
    >
      <div className="space-y-4">
        <dl className="grid grid-cols-2 gap-3 text-sm md:grid-cols-3">
          <div><dt className="text-ink-secondary">{t('pos.shifts.opened')}</dt><dd>{new Date(shift.openedAt).toLocaleString()}</dd></div>
          <div><dt className="text-ink-secondary">{t('pos.shifts.closed')}</dt><dd>{shift.closedAt ? new Date(shift.closedAt).toLocaleString() : '—'}</dd></div>
          <div><dt className="text-ink-secondary">{t('pos.shifts.cashier')}</dt><dd title={shift.cashierId}>{short(shift.cashierId)}</dd></div>
          <div><dt className="text-ink-secondary">{t('pos.shifts.openingBalance')}</dt><dd>{money(shift.openingBalance)}</dd></div>
          <div><dt className="text-ink-secondary">{t('pos.shifts.closingBalance')}</dt><dd>{shift.closingBalance != null ? money(shift.closingBalance) : '—'}</dd></div>
          <div>
            <dt className="text-ink-secondary">{t('pos.shifts.difference')}</dt>
            <dd className={diff ? 'font-semibold text-danger-soft-text' : ''}>{diff == null ? '—' : money(diff)}</dd>
          </div>
        </dl>

        <div>
          <h4 className="mb-1 text-xs font-semibold uppercase text-ink-secondary">{t('pos.shifts.salesByMethod')}</h4>
          {isLoading ? <Skeleton className="h-20 w-64" /> : (
            <table className="text-sm">
              <tbody>
                {Object.entries(summary?.salesByPaymentMethod ?? {}).map(([method, amount]) => (
                  <tr key={method}><td className="pr-6">{method}</td><td>{money(amount)}</td></tr>
                ))}
                <tr className="border-t border-line font-semibold"><td className="pr-6">{t('pos.shifts.totalSales')}</td><td>{money(summary?.totalSales ?? shift.totalSales)}</td></tr>
                <tr><td className="pr-6 text-ink-secondary">{t('pos.shifts.completed')}</td><td>{summary?.completedTransactionCount ?? shift.transactionCount}</td></tr>
                <tr><td className="pr-6 text-ink-secondary">{t('pos.shifts.voided')}</td><td>{summary?.voidedTransactionCount ?? 0}</td></tr>
              </tbody>
            </table>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          {denominations(t('pos.shifts.openingCount'), shift.openingDenominations)}
          {denominations(t('pos.shifts.closingCount'), shift.closingDenominations)}
        </div>
      </div>
    </Modal>
  )
}

export function PosShiftsPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [terminalId, setTerminalId] = useState('')
  const [status, setStatus] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [selected, setSelected] = useState<CashShift | null>(null)

  const terminals = useQuery({ queryKey: TERMINALS_KEY, queryFn: ({ signal }) => posApi.terminals(undefined, signal), staleTime: 60_000, retry: false })
  const nameOf = (id: string) => terminals.data?.find((x) => x.id === id)?.name ?? short(id)

  const filter = { terminalId: terminalId || undefined, status: status || undefined, from: from || undefined, to: to || undefined, page, pageSize }
  const filtered = !!(terminalId || status || from || to)
  const { data, isLoading } = useQuery({ queryKey: ['pos', 'shifts', filter], queryFn: ({ signal }) => posApi.shifts(filter, signal) })
  const clearFilters = () => { setTerminalId(''); setStatus(''); setFrom(''); setTo(''); setPage(1) }

  const columns = useMemo(
    () => [
      { key: 'terminalId', label: t('pos.shifts.terminal'), render: (s: CashShift) => nameOf(s.terminalId) },
      { key: 'openedAt', label: t('pos.shifts.opened'), render: (s: CashShift) => new Date(s.openedAt).toLocaleString() },
      { key: 'closedAt', label: t('pos.shifts.closed'), render: (s: CashShift) => (s.closedAt ? new Date(s.closedAt).toLocaleString() : '—') },
      { key: 'status', label: t('common.status'), render: (s: CashShift) => <StatusBadge status={s.status === 'Open' ? 'Active' : 'Completed'} label={t(`pos.shiftStatus.${s.status}`)} /> },
      { key: 'transactionCount', label: t('pos.shifts.transactions'), align: 'right' as const },
      { key: 'totalSales', label: t('pos.shifts.totalSales'), align: 'right' as const, render: (s: CashShift) => money(s.totalSales) },
      { key: 'actions', label: '', render: (s: CashShift) => <Button variant="link" className="text-xs" onClick={() => setSelected(s)}>{t('pos.shifts.view')}</Button> },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, terminals.data],
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('pos.shifts.title')}</h1>

      <div className="flex flex-wrap items-end gap-3">
        {terminals.data && (
          <Select className="w-52" aria-label={t('pos.shifts.terminal')} value={terminalId} onChange={(e) => { setTerminalId(e.target.value); setPage(1) }}>
            <option value="">{t('pos.allTerminals')}</option>
            {terminals.data.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </Select>
        )}
        <Select className="w-40" aria-label={t('common.status')} value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          <option value="">{t('pos.allStatuses')}</option>
          <option value="Open">{t('pos.shiftStatus.Open')}</option>
          <option value="Closed">{t('pos.shiftStatus.Closed')}</option>
        </Select>
        <Input type="date" className="w-40" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} aria-label={t('pos.from')} />
        <Input type="date" className="w-40" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} aria-label={t('pos.to')} />
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={filtered
          ? <EmptyState variant="no-results" title={t('common.noResults')} description={t('common.noResultsHint')} action={<Button variant="outline" onClick={clearFilters}>{t('common.clear')}</Button>} />
          : <EmptyState title={t('pos.shifts.empty')} />}
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />

      {selected && <ShiftDetail shift={selected} terminalName={nameOf(selected.terminalId)} onClose={() => setSelected(null)} />}
    </div>
  )
}
