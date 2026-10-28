import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { posApi } from '@/api/pos/pos.api'
import type { Terminal } from '@/api/pos/pos.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

export const TERMINALS_KEY = ['pos', 'terminals']

function TerminalModal({ terminal, onClose }: { terminal: Terminal | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!terminal
  const { register, handleSubmit, formState: { errors } } = useForm<{ name: string }>({ defaultValues: { name: terminal?.name ?? '' } })

  const mutation = useMutation({
    mutationFn: ({ name }: { name: string }) => (isEdit ? posApi.renameTerminal(terminal!.id, name.trim()) : posApi.createTerminal({ name: name.trim() })),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TERMINALS_KEY })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{isEdit ? t('pos.terminals.editTitle') : t('pos.terminals.addTitle')}</h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('pos.required')}</p>}
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('pos.terminals.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">{t('common.cancel')}</button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">{mutation.isPending ? t('common.saving') : t('common.save')}</button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function PosTerminalsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [modal, setModal] = useState<{ open: boolean; terminal: Terminal | null }>({ open: false, terminal: null })
  const [deactivating, setDeactivating] = useState<Terminal | null>(null)

  const canManage = hasPermission('pos:terminals:manage')
  const { data, isLoading } = useQuery({ queryKey: TERMINALS_KEY, queryFn: ({ signal }) => posApi.terminals(undefined, signal), staleTime: 30_000, enabled: canManage })

  const setActive = useMutation({
    mutationFn: ({ terminal, active }: { terminal: Terminal; active: boolean }) => posApi.setTerminalActive(terminal.id, active),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TERMINALS_KEY })
      setDeactivating(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'name', label: t('common.name') },
      { key: 'isActive', label: t('common.status'), render: (x: Terminal) => <StatusBadge status={x.isActive ? 'Active' : 'Cancelled'} label={x.isActive ? t('pos.terminals.active') : t('pos.terminals.inactive')} /> },
      { key: 'lastSyncAt', label: t('pos.terminals.lastSync'), render: (x: Terminal) => (x.lastSyncAt ? new Date(x.lastSyncAt).toLocaleString() : '—') },
      { key: 'createdAt', label: t('pos.terminals.created'), render: (x: Terminal) => new Date(x.createdAt).toLocaleDateString() },
      {
        key: 'actions',
        label: '',
        render: (x: Terminal) =>
          canManage ? (
            <div className="flex gap-3">
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setModal({ open: true, terminal: x })}>{t('common.edit')}</button>
              {x.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setDeactivating(x)}>{t('common.deactivate')}</button>
              ) : (
                <button className="text-xs text-green-700 hover:underline" onClick={() => setActive.mutate({ terminal: x, active: true })}>{t('common.activate')}</button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, t, setActive],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('pos.terminals.title')}</h1>
        {canManage && <button className="btn-primary" onClick={() => setModal({ open: true, terminal: null })}>{t('pos.terminals.add')}</button>}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modal.open && <TerminalModal terminal={modal.terminal} onClose={() => setModal({ open: false, terminal: null })} />}

      <ConfirmDialog
        open={!!deactivating}
        title={t('pos.terminals.deactivateTitle')}
        message={t('pos.terminals.deactivateConfirm', { name: deactivating?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => deactivating && setActive.mutate({ terminal: deactivating, active: false })}
        onCancel={() => setDeactivating(null)}
      />
    </div>
  )
}
