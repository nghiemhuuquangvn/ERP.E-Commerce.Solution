import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { format } from 'date-fns'
import { useTranslation } from 'react-i18next'
import { mfgApi, type ProductionSettingValues } from '@/api/manufacturing/mfg.api'
import { DataTable } from '@/components/shared/DataTable'
import { NoPlant, PageHeader, PlantSelector, usePlantContext } from '@/components/manufacturing/shared'
import { Alert, Button, Checkbox, FormField, Input, Select, Skeleton, useToast } from '@/components/ui'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/lib/serverMessage'

const PCT_FIELDS = ['overProductionTolerancePct', 'underCompletionTolerancePct', 'overIssueTolerancePct'] as const
const FLAGS = ['blockReleaseOnShortage', 'allowNegativeStockOnBackflush', 'requireSequentialOperations', 'autoCompleteOnFullReceipt'] as const

/** S02: the production parameters of a plant (BR-SET-01..03) and the trail of who changed what. */
export function SettingsPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { hasPermission } = usePermission()
  const canSave = hasPermission('manufacturing:settings:manage')
  const { plantId, isLoading: plantsLoading } = usePlantContext()
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({ queryKey: ['mfg', 'settings', plantId], queryFn: () => mfgApi.getSettings(plantId!), enabled: !!plantId })
  const { data: audit = [] } = useQuery({ queryKey: ['mfg', 'settings-audit', plantId], queryFn: () => mfgApi.settingsAudit(plantId!), enabled: !!plantId })
  const { register, handleSubmit, reset, formState: { errors, isDirty } } = useForm<ProductionSettingValues>()
  useEffect(() => { if (data) reset(data.values) }, [data, reset])

  const save = useMutation({
    mutationFn: (v: ProductionSettingValues) => mfgApi.saveSettings(plantId!, {
      ...v,
      overProductionTolerancePct: Number(v.overProductionTolerancePct), underCompletionTolerancePct: Number(v.underCompletionTolerancePct), overIssueTolerancePct: Number(v.overIssueTolerancePct),
      mrpHorizonDays: Number(v.mrpHorizonDays), maxBomLevels: Number(v.maxBomLevels),
    }),
    onSuccess: () => {
      setError(null)
      toast.success(t('common.saved'))
      queryClient.invalidateQueries({ queryKey: ['mfg', 'settings', plantId] })
      queryClient.invalidateQueries({ queryKey: ['mfg', 'settings-audit', plantId] })
    },
    onError: (e) => setError(serverMessage(e, t('common.failedToSave'))),
  })

  if (!plantsLoading && !plantId) return <div><PageHeader title={t('mfg.nav.settings')} /><NoPlant /></div>
  const pct = { min: 0, max: 100, step: 0.01 }

  return (
    <div>
      <PageHeader title={t('mfg.nav.settings')} hint={t('mfg.settings.hint')} actions={<PlantSelector />} />
      {isLoading || !data ? <Skeleton className="h-64 w-full" /> : (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
          <form onSubmit={handleSubmit((v) => save.mutate(v))} className="space-y-6 rounded-lg border border-line bg-surface p-5" aria-label={t('mfg.nav.settings')}>
            {error && <Alert variant="danger">{error}</Alert>}
            {!canSave && <Alert variant="info">{t('mfg.settings.readOnly')}</Alert>}
            <fieldset disabled={!canSave} className="space-y-6">
              <div>
                <h2 className="mb-2 text-sm font-semibold text-ink">{t('mfg.settings.tolerances')}</h2>
                <div className="grid gap-4 sm:grid-cols-3">
                  {PCT_FIELDS.map((f) => (
                    <FormField key={f} label={t(`mfg.settings.${f}`)} hint={t(`mfg.settings.${f}Hint`)} error={errors[f] ? t('mfg.settings.range', { min: 0, max: 100 }) : undefined}>
                      <Input type="number" {...pct} {...register(f, { required: true, min: 0, max: 100 })} />
                    </FormField>
                  ))}
                </div>
              </div>
              <div>
                <h2 className="mb-2 text-sm font-semibold text-ink">{t('mfg.settings.behaviour')}</h2>
                <div className="grid gap-2 sm:grid-cols-2">
                  {FLAGS.map((f) => <Checkbox key={f} label={t(`mfg.settings.${f}`)} {...register(f)} />)}
                </div>
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <FormField label={t('mfg.settings.defaultIssueMethod')}>
                  <Select {...register('defaultIssueMethod')}><option value="Manual">{t('mfg.issue.Manual')}</option><option value="Backflush">{t('mfg.issue.Backflush')}</option></Select>
                </FormField>
                <FormField label={t('mfg.settings.costingMethod')} hint={t('mfg.settings.costingHint')}>
                  <Select {...register('costingMethod')}><option value="Actual">{t('mfg.settings.Actual')}</option><option value="Standard">{t('mfg.settings.Standard')}</option></Select>
                </FormField>
                <FormField label={t('mfg.settings.mrpHorizonDays')} error={errors.mrpHorizonDays ? t('mfg.settings.range', { min: 7, max: 365 }) : undefined}>
                  <Input type="number" {...register('mrpHorizonDays', { required: true, min: 7, max: 365 })} />
                </FormField>
                <FormField label={t('mfg.settings.maxBomLevels')} error={errors.maxBomLevels ? t('mfg.settings.range', { min: 1, max: 50 }) : undefined}>
                  <Input type="number" {...register('maxBomLevels', { required: true, min: 1, max: 50 })} />
                </FormField>
                <FormField label={t('mfg.settings.fgLotNumberFormat')} hint={t('mfg.settings.fgLotHint')} className="sm:col-span-2" error={errors.fgLotNumberFormat ? t('common.required') : undefined}>
                  <Input {...register('fgLotNumberFormat', { required: true })} />
                </FormField>
              </div>
            </fieldset>
            {canSave && <div className="flex justify-end"><Button type="submit" disabled={!isDirty} loading={save.isPending}>{t('common.save')}</Button></div>}
          </form>

          <section aria-labelledby="mfg-audit">
            <h2 id="mfg-audit" className="mb-2 text-sm font-semibold text-ink">{t('mfg.settings.audit')}</h2>
            <DataTable
              data={audit.map((a, i) => ({ id: String(i), ...a }))}
              emptyMessage={t('mfg.settings.noAudit')}
              columns={[
                { key: 'field', label: t('mfg.settings.field'), render: (a) => <span className="text-xs">{a.field}</span> },
                { key: 'change', label: t('mfg.settings.change'), render: (a) => <span className="font-mono text-xs">{a.oldValue} → {a.newValue}</span> },
                { key: 'by', label: t('mfg.settings.by'), render: (a) => <div className="text-xs"><div>{a.changedBy}</div><div className="text-ink-secondary">{format(new Date(a.changedAt), 'yyyy-MM-dd HH:mm')}</div></div> },
              ]}
            />
          </section>
        </div>
      )}
    </div>
  )
}
