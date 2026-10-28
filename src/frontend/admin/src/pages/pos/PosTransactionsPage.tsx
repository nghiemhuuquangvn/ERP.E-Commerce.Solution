import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { posApi } from '@/api/pos/pos.api'
import type { PosTransaction } from '@/api/pos/pos.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { ReasonDialog } from '@/components/shared/ReasonDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { TERMINALS_KEY } from './PosTerminalsPage'

const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`
const errorText = (e: unknown, fallback: string) =>
  (e as { response?: { data?: { detail?: string; message?: string } } })?.response?.data?.detail ??
  (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? fallback

function Receipt({ id }: { id: string }) {
  const { t } = useTranslation()
  const { data, isLoading, isError } = useQuery({ queryKey: ['pos', 'receipt', id], queryFn: () => posApi.receipt(id) })
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>
  if (isError) return <p className="text-sm text-red-600">{t('pos.transactions.receiptFailed')}</p>
  return <pre className="max-h-64 overflow-auto rounded bg-gray-50 p-3 text-xs">{data}</pre>
}

function TransactionDetail({ tx, canVoid, onVoid, onClose }: { tx: PosTransaction; canVoid: boolean; onVoid: () => void; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/40 p-4">
      <div className="card w-full max-w-2xl space-y-4 p-6">
        <h3 className="text-base font-semibold text-gray-900">{t('pos.transactions.detailTitle', { id: tx.id.slice(0, 8) })}</h3>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs uppercase text-gray-500">
              <th className="py-1">{t('pos.transactions.product')}</th><th>{t('pos.transactions.qty')}</th><th>{t('pos.transactions.unitPrice')}</th><th className="text-right">{t('pos.transactions.lineTotal')}</th>
            </tr>
          </thead>
          <tbody>
            {tx.items.map((i, n) => (
              <tr key={n} className="border-t"><td className="py-1">{i.productName}</td><td>{i.quantity}</td><td>{money(i.unitPrice)}</td><td className="text-right">{money(i.lineTotal)}</td></tr>
            ))}
          </tbody>
        </table>
        <dl className="ml-auto grid w-64 grid-cols-2 gap-1 text-sm">
          <dt className="text-gray-500">{t('pos.transactions.subTotal')}</dt><dd className="text-right">{money(tx.subTotal)}</dd>
          <dt className="text-gray-500">{t('pos.transactions.discount')}</dt><dd className="text-right">−{money(tx.discountAmount)}</dd>
          <dt className="text-gray-500">{t('pos.transactions.tax')}</dt><dd className="text-right">{money(tx.taxAmount)}</dd>
          <dt className="font-semibold">{t('pos.transactions.total')}</dt><dd className="text-right font-semibold">{money(tx.totalAmount)}</dd>
        </dl>
        {tx.tenders.length > 0 && (
          <p className="text-sm text-gray-600">{t('pos.transactions.tenders')}: {tx.tenders.map((x) => `${x.paymentMethod} ${money(x.amount)}`).join(' · ')}</p>
        )}
        {tx.voidReason && <p className="text-sm text-red-600">{t('pos.transactions.voidReason')}: {tx.voidReason}</p>}
        {tx.status === 'Completed' && <Receipt id={tx.id} />}
        <div className="flex justify-end gap-3">
          {canVoid && tx.status !== 'Voided' && (
            <button className="btn-outline text-red-600" onClick={onVoid}>{t('pos.transactions.void')}</button>
          )}
          <button className="btn-outline" onClick={onClose}>{t('common.close')}</button>
        </div>
      </div>
    </div>
  )
}

export function PosTransactionsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const { page, pageSize, setPage } = usePagination()
  const [terminalId, setTerminalId] = useState('')
  const [status, setStatus] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [selected, setSelected] = useState<PosTransaction | null>(null)
  const [voiding, setVoiding] = useState<PosTransaction | null>(null)

  const canVoid = hasPermission('pos:transactions')
  const terminals = useQuery({ queryKey: TERMINALS_KEY, queryFn: ({ signal }) => posApi.terminals(undefined, signal), staleTime: 60_000, retry: false })
  const nameOf = (id: string) => terminals.data?.find((x) => x.id === id)?.name ?? id.slice(0, 8)

  const filter = { terminalId: terminalId || undefined, status: status || undefined, from: from || undefined, to: to || undefined, page, pageSize }
  const { data, isLoading } = useQuery({ queryKey: ['pos', 'transactions', filter], queryFn: ({ signal }) => posApi.transactions(filter, signal) })

  const voidMutation = useMutation({
    mutationFn: ({ tx, reason }: { tx: PosTransaction; reason: string }) => posApi.voidTransaction(tx.id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pos', 'transactions'] })
      queryClient.invalidateQueries({ queryKey: ['pos', 'receipt'] })
      setVoiding(null)
      setSelected(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'createdAt', label: t('pos.transactions.when'), render: (x: PosTransaction) => new Date(x.createdAt).toLocaleString() },
      { key: 'terminalId', label: t('pos.shifts.terminal'), render: (x: PosTransaction) => nameOf(x.terminalId) },
      { key: 'items', label: t('pos.transactions.items'), render: (x: PosTransaction) => x.items.reduce((n, i) => n + i.quantity, 0) },
      { key: 'paymentMethod', label: t('pos.transactions.payment'), render: (x: PosTransaction) => x.paymentMethod ?? '—' },
      { key: 'totalAmount', label: t('pos.transactions.total'), render: (x: PosTransaction) => money(x.totalAmount) },
      { key: 'status', label: t('common.status'), render: (x: PosTransaction) => <StatusBadge status={x.status} label={t(`pos.transactionStatus.${x.status}`)} /> },
      { key: 'actions', label: '', render: (x: PosTransaction) => <button className="text-xs text-primary-600 hover:underline" onClick={() => setSelected(x)}>{t('pos.shifts.view')}</button> },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, terminals.data],
  )

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('pos.transactions.title')}</h1>

      <div className="flex flex-wrap items-end gap-3">
        {terminals.data && (
          <select className="input w-52" value={terminalId} onChange={(e) => { setTerminalId(e.target.value); setPage(1) }}>
            <option value="">{t('pos.allTerminals')}</option>
            {terminals.data.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        )}
        <select className="input w-44" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          <option value="">{t('pos.allStatuses')}</option>
          {['Open', 'Completed', 'Voided', 'Suspended'].map((s) => <option key={s} value={s}>{t(`pos.transactionStatus.${s}`)}</option>)}
        </select>
        <input type="date" className="input w-40" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} aria-label={t('pos.from')} />
        <input type="date" className="input w-40" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} aria-label={t('pos.to')} />
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />

      {selected && <TransactionDetail tx={selected} canVoid={canVoid} onVoid={() => setVoiding(selected)} onClose={() => setSelected(null)} />}

      <ReasonDialog
        open={!!voiding}
        title={t('pos.transactions.voidTitle')}
        message={t('pos.transactions.voidMessage')}
        confirmLabel={t('pos.transactions.void')}
        danger
        busy={voidMutation.isPending}
        error={voidMutation.isError ? errorText(voidMutation.error, t('pos.transactions.voidFailed')) : null}
        onConfirm={(reason) => voiding && voidMutation.mutate({ tx: voiding, reason })}
        onCancel={() => setVoiding(null)}
      />
    </div>
  )
}
