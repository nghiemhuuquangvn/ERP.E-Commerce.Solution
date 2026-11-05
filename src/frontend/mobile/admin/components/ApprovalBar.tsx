import { useState } from 'react'
import { ScrollView, View } from 'react-native'
import { useTranslation } from 'react-i18next'
import { Alert, BottomSheet, Button, TextField, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

/** One input of an action's sheet (a credit note number, an amount, an address line). */
export interface ActionField {
  key: string
  label: string
  /** text (default) or a non-negative number; a number is validated before anything is sent. */
  kind?: 'text' | 'number'
  required?: boolean
  multiline?: boolean
  /** Text the box opens with (e.g. the current credit limit). */
  initial?: string
}

export interface ApprovalAction {
  key: string
  label: string
  variant: 'primary' | 'outline' | 'danger'
  /** Title of the confirm / reason sheet ("Reject PO-00123"). */
  confirmTitle: string
  /** Label of the sheet's confirm button (defaults to the action label). */
  confirmLabel?: string
  /** When set, the sheet asks for a reason (required) and passes it to `run`. */
  reasonLabel?: string
  /** Extra inputs; their values (trimmed text by key) are passed to `run` as the second argument. */
  fields?: ActionField[]
  run: (reason?: string, values?: Record<string, string>) => Promise<unknown>
  successMessage: string
}

/** A number as people type it: "1.250,5" is not accepted, "1250.5" and "1250,5" are. Null when it is not a non-negative number. */
export function parseAmount(text: string): number | null {
  const n = Number(text.trim().replace(',', '.'))
  return text.trim() !== '' && Number.isFinite(n) && n >= 0 ? n : null
}

/**
 * The action row under a record, and the one sheet every action goes through: nothing runs on the first tap. An action that
 * needs a reason (reject, cancel) cannot be confirmed without one, nor without its required fields; a refusal from the server is shown
 * in the sheet in the server's own words and the sheet stays open. The caller passes only the actions the status and the permissions allow.
 */
export function ApprovalBar({ actions, onDone }: { actions: ApprovalAction[]; onDone: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const [active, setActive] = useState<ApprovalAction | null>(null)
  const [reason, setReason] = useState('')
  const [values, setValues] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tried, setTried] = useState(false)

  if (actions.length === 0) return null

  const reset = () => {
    setActive(null)
    setReason('')
    setValues({})
    setError(null)
    setTried(false)
  }
  const close = () => {
    if (busy) return
    reset()
  }
  const open = (a: ApprovalAction) => {
    setActive(a)
    setReason('')
    setValues(Object.fromEntries((a.fields ?? []).map((f) => [f.key, f.initial ?? ''])))
    setError(null)
    setTried(false)
  }

  const fieldError = (f: ActionField): string | undefined => {
    const text = (values[f.key] ?? '').trim()
    if (f.required && !text) return t('common.fieldRequired')
    if (f.kind === 'number' && text && parseAmount(text) === null) return t('common.numberInvalid')
    return undefined
  }

  async function confirm() {
    if (!active) return
    setTried(true)
    if (active.reasonLabel && !reason.trim()) return
    if ((active.fields ?? []).some((f) => fieldError(f))) return
    setBusy(true)
    setError(null)
    try {
      const sent = Object.fromEntries((active.fields ?? []).map((f) => [f.key, (values[f.key] ?? '').trim()]))
      await active.run(active.reasonLabel ? reason.trim() : undefined, active.fields ? sent : undefined)
      toast.success(active.successMessage)
      setBusy(false)
      reset()
      onDone()
    } catch (e) {
      setBusy(false)
      setError(serverMessage(e, t('common.actionError'), t('common.forbidden')))
    }
  }

  return (
    <>
      <View className="flex-row gap-2 border-t border-line bg-surface px-4 py-3">
        {actions.map((a) => (
          <View key={a.key} className="flex-1">
            <Button variant={a.variant} onPress={() => open(a)}>{a.label}</Button>
          </View>
        ))}
      </View>
      <BottomSheet visible={!!active} title={active?.confirmTitle ?? ''} onClose={close} closeLabel={t('common.close')}>
        <ScrollView keyboardShouldPersistTaps="handled" style={{ maxHeight: 420 }}>
          {error ? <View className="mb-3"><Alert tone="danger">{error}</Alert></View> : null}
          {(active?.fields ?? []).map((f) => (
            <TextField
              key={f.key}
              label={f.label}
              value={values[f.key] ?? ''}
              onChangeText={(v) => setValues((cur) => ({ ...cur, [f.key]: v }))}
              keyboardType={f.kind === 'number' ? 'decimal-pad' : 'default'}
              multiline={f.multiline}
              required={f.required}
              error={tried ? fieldError(f) : undefined}
            />
          ))}
          {active?.reasonLabel ? (
            <TextField
              label={active.reasonLabel}
              value={reason}
              onChangeText={setReason}
              multiline
              required
              error={tried && !reason.trim() ? t('common.reasonRequired') : undefined}
            />
          ) : null}
        </ScrollView>
        <View className="flex-row gap-2">
          <View className="flex-1"><Button variant="outline" onPress={close} disabled={busy}>{t('common.back')}</Button></View>
          <View className="flex-1">
            <Button variant={active?.variant === 'danger' ? 'danger' : 'primary'} onPress={confirm} loading={busy}>
              {active?.confirmLabel ?? active?.label ?? ''}
            </Button>
          </View>
        </View>
      </BottomSheet>
    </>
  )
}
