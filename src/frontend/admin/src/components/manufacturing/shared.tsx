import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { mfgApi, downloadTemplate, type ImportResult, type MachineStatus, type VersionStatus } from '@/api/manufacturing/mfg.api'
import { Alert, Badge, Button, Modal, Select, Spinner, useToast } from '@/components/ui'
import type { BadgeVariant } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

// ── The plant everything on the Manufacturing screens is about ───────────────────────────────────────────

interface PlantState {
  plantId: string | null
  setPlantId: (id: string | null) => void
}

/** The plant chosen in the selector; kept in the browser so the next screen opens on the same plant. */
export const usePlantStore = create<PlantState>()(persist((set) => ({ plantId: null, setPlantId: (plantId) => set({ plantId }) }), { name: 'erp-mfg-plant' }))

/** The active plants the user may see and the one in use (the stored one when it still exists, else the first). */
export function usePlantContext() {
  const { plantId, setPlantId } = usePlantStore()
  const { data: plants = [], isLoading } = useQuery({ queryKey: ['mfg', 'plants', 'active'], queryFn: () => mfgApi.listPlants(true), staleTime: 30_000 })
  const current = plants.find((p) => p.id === plantId) ?? plants[0] ?? null
  useEffect(() => {
    if (current && current.id !== plantId) setPlantId(current.id)
  }, [current, plantId, setPlantId])
  return { plants, plant: current, plantId: current?.id ?? null, setPlantId, isLoading }
}

export function PlantSelector() {
  const { t } = useTranslation()
  const { plants, plantId, setPlantId } = usePlantContext()
  if (plants.length === 0) return null
  return (
    <label className="flex items-center gap-2 text-sm text-ink-secondary">
      {t('mfg.plant')}
      <Select className="w-56" value={plantId ?? ''} onChange={(e) => setPlantId(e.target.value)} aria-label={t('mfg.plant')}>
        {plants.map((p) => <option key={p.id} value={p.id}>{p.code} - {p.name}</option>)}
      </Select>
    </label>
  )
}

/** What a screen shows when no plant exists yet (or none is visible to the user). */
export function NoPlant() {
  const { t } = useTranslation()
  return (
    <Alert variant="info" title={t('mfg.noPlantTitle')}>
      {t('mfg.noPlant')} <Link className="font-medium text-brand-action underline" to="/manufacturing/plants">{t('mfg.nav.plants')}</Link>
    </Alert>
  )
}

export function PageHeader({ title, hint, actions }: { title: string; hint?: string; actions?: React.ReactNode }) {
  return (
    <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 className="text-xl font-semibold text-ink">{title}</h1>
        {hint && <p className="mt-1 max-w-3xl text-sm text-ink-secondary">{hint}</p>}
      </div>
      <div className="flex flex-wrap items-center gap-3">{actions}</div>
    </div>
  )
}

const VERSION_VARIANT: Record<VersionStatus, BadgeVariant> = { Draft: 'muted', Approved: 'success', Obsolete: 'warning' }
export function VersionBadge({ status }: { status: VersionStatus }) {
  const { t } = useTranslation()
  return <Badge variant={VERSION_VARIANT[status]}>{t(`mfg.status.${status}`)}</Badge>
}

const MACHINE_VARIANT: Record<MachineStatus, BadgeVariant> = { Active: 'success', UnderMaintenance: 'warning', Retired: 'muted' }
export function MachineBadge({ status }: { status: MachineStatus }) {
  const { t } = useTranslation()
  return <Badge variant={MACHINE_VARIANT[status]}>{t(`mfg.machineStatus.${status}`)}</Badge>
}

export function ActiveBadge({ active }: { active: boolean }) {
  const { t } = useTranslation()
  return <Badge variant={active ? 'success' : 'muted'}>{active ? t('common.active') : t('common.inactive')}</Badge>
}

export const fmtQty = (n: number | null | undefined, max = 6) => (n == null ? '' : new Intl.NumberFormat('en-US', { maximumFractionDigits: max }).format(n))
export const todayIso = () => new Date().toISOString().slice(0, 10)

// ── CSV import dialog (BOM and routing, T2.9): dry run first, nothing is written while the file has errors ──

interface CsvImportDialogProps {
  open: boolean
  kind: 'bom' | 'routing'
  plantId: string
  itemId: string
  effectiveFrom?: string
  onClose: () => void
  /** Called with the id of the Draft that was created. */
  onImported: (id: string) => void
}

export function CsvImportDialog({ open, kind, plantId, itemId, effectiveFrom, onClose, onImported }: CsvImportDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (open) { setFile(null); setResult(null); setError(null) }
  }, [open])

  const run = useMutation({
    mutationFn: (dryRun: boolean) => {
      const params = { plantId, itemId, effectiveFrom, dryRun }
      return kind === 'bom' ? mfgApi.importBom(file!, params) : mfgApi.importRouting(file!, params)
    },
    onSuccess: (r) => {
      setResult(r)
      setError(null)
      if (r.createdId) { toast.success(t('mfg.import.created', { version: r.version })); onImported(r.createdId) }
    },
    onError: (e) => { setResult(null); setError(serverMessage(e, t('mfg.import.failed'))) },
  })

  const template = () => downloadTemplate(kind === 'bom' ? mfgApi.bomTemplateUrl : mfgApi.routingTemplateUrl, kind === 'bom' ? 'bom-import-template.csv' : 'routing-import-template.csv').catch(() => toast.error(t('mfg.import.templateFailed')))
  const clean = !!result && result.errors.length === 0

  return (
    <Modal
      open={open}
      size="lg"
      title={t(kind === 'bom' ? 'mfg.import.bomTitle' : 'mfg.import.routingTitle')}
      onClose={onClose}
      closeOnEsc={!run.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.close')}</Button>
          <Button variant="secondary" disabled={!file || run.isPending} loading={run.isPending && run.variables === true} onClick={() => run.mutate(true)}>{t('mfg.import.dryRun')}</Button>
          <Button disabled={!file || !clean || result?.dryRun === false || run.isPending} loading={run.isPending && run.variables === false} onClick={() => run.mutate(false)}>{t('mfg.import.import')}</Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-secondary">{t('mfg.import.hint')}</p>
        <div className="flex flex-wrap items-center gap-3">
          <Button variant="outline" size="sm" onClick={template}>{t('mfg.import.template')}</Button>
          <input
            ref={input}
            type="file"
            accept=".csv,text/csv"
            aria-label={t('mfg.import.file')}
            className="text-sm text-ink"
            onChange={(e) => { setFile(e.target.files?.[0] ?? null); setResult(null); setError(null) }}
          />
        </div>
        {run.isPending && <div className="flex items-center gap-2 text-sm text-ink-secondary"><Spinner size={16} />{t('mfg.import.working')}</div>}
        {error && <Alert variant="danger">{error}</Alert>}
        {result && result.errors.length === 0 && (
          <Alert variant="success">{result.dryRun ? t('mfg.import.cleanDry', { count: result.rowCount }) : t('mfg.import.created', { version: result.version })}</Alert>
        )}
        {result && result.errors.length > 0 && (
          <div>
            <Alert variant="danger" title={t('mfg.import.errorsTitle', { count: result.errors.length })}>{t('mfg.import.errorsHint')}</Alert>
            <table className="mt-3 w-full text-left text-sm">
              <caption className="sr-only">{t('mfg.import.errorsTitle', { count: result.errors.length })}</caption>
              <thead><tr className="border-b border-line text-ink-secondary"><th className="py-1 pr-3">{t('mfg.import.line')}</th><th className="py-1 pr-3">{t('mfg.import.key')}</th><th className="py-1">{t('mfg.import.message')}</th></tr></thead>
              <tbody>
                {result.errors.map((e, i) => (
                  <tr key={i} className="border-b border-line align-top"><td className="py-1 pr-3 font-mono">{e.line}</td><td className="py-1 pr-3 font-mono text-xs">{e.key ?? ''}</td><td className="py-1">{e.message}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </Modal>
  )
}
