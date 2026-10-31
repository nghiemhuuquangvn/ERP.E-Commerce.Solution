import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { posApi } from '@/api/pos/pos.api'
import type { Terminal } from '@/api/pos/pos.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Alert, Button, EmptyState, FormField, Input, Modal, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'

export const TERMINALS_KEY = ['pos', 'terminals']

function TerminalModal({ terminal, onClose }: { terminal: Terminal | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const isEdit = !!terminal
  const { register, handleSubmit, formState: { errors } } = useForm<{ name: string }>({ defaultValues: { name: terminal?.name ?? '' } })

  const mutation = useMutation({
    mutationFn: ({ name }: { name: string }) => (isEdit ? posApi.renameTerminal(terminal!.id, name.trim()) : posApi.createTerminal({ name: name.trim() })),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TERMINALS_KEY })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('pos.terminals.editTitle') : t('pos.terminals.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="terminal-form" loading={mutation.isPending}>{mutation.isPending ? t('common.saving') : t('common.save')}</Button>
        </>
      }
    >
      <form id="terminal-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('common.name')} required error={errors.name ? t('pos.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{t('pos.terminals.failedToSave')}</Alert>}
      </form>
    </Modal>
  )
}

export function PosTerminalsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [modal, setModal] = useState<{ open: boolean; terminal: Terminal | null }>({ open: false, terminal: null })
  const [deactivating, setDeactivating] = useState<Terminal | null>(null)

  const canManage = hasPermission('pos:terminals:manage')
  const { data, isLoading } = useQuery({ queryKey: TERMINALS_KEY, queryFn: ({ signal }) => posApi.terminals(undefined, signal), staleTime: 30_000, enabled: canManage })

  const setActive = useMutation({
    mutationFn: ({ terminal, active }: { terminal: Terminal; active: boolean }) => posApi.setTerminalActive(terminal.id, active),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TERMINALS_KEY })
      toast.success(t('common.saved'))
      setDeactivating(null)
    },
    onError: () => {
      toast.error(t('common.failedToSave'))
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
              <Button variant="link" className="text-xs" onClick={() => setModal({ open: true, terminal: x })}>{t('common.edit')}</Button>
              {x.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setDeactivating(x)}>{t('common.deactivate')}</Button>
              ) : (
                <Button variant="link" className="text-xs" onClick={() => setActive.mutate({ terminal: x, active: true })}>{t('common.activate')}</Button>
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
        {canManage && <Button onClick={() => setModal({ open: true, terminal: null })}>{t('pos.terminals.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={data ?? []}
        isLoading={isLoading}
        emptyState={
          <EmptyState
            title={t('pos.terminals.empty')}
            description={canManage ? t('pos.terminals.emptyHint') : undefined}
            action={canManage ? <Button onClick={() => setModal({ open: true, terminal: null })}>{t('pos.terminals.add')}</Button> : undefined}
          />
        }
      />

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
