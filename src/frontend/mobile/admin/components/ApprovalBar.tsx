import { useState } from 'react'
import { View } from 'react-native'
import { useTranslation } from 'react-i18next'
import { Alert, BottomSheet, Button, TextField, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  run: (reason?: string) => Promise<unknown>
  successMessage: string
}

/**
 * The action row under a record, and the one sheet every action goes through: nothing runs on the first tap. An action that
 * needs a reason (reject, cancel) cannot be confirmed without one; a refusal from the server is shown in the sheet in the
 * server's own words and the sheet stays open. The caller passes only the actions the status and the permissions allow.
 */
export function ApprovalBar({ actions, onDone }: { actions: ApprovalAction[]; onDone: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const [active, setActive] = useState<ApprovalAction | null>(null)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tried, setTried] = useState(false)

  if (actions.length === 0) return null

  const close = () => {
    if (busy) return
    setActive(null)
    setReason('')
    setError(null)
    setTried(false)
  }

  async function confirm() {
    if (!active) return
    setTried(true)
    if (active.reasonLabel && !reason.trim()) return
    setBusy(true)
    setError(null)
    try {
      await active.run(active.reasonLabel ? reason.trim() : undefined)
      toast.success(active.successMessage)
      setBusy(false)
      setActive(null)
      setReason('')
      setTried(false)
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
            <Button variant={a.variant} onPress={() => setActive(a)}>{a.label}</Button>
          </View>
        ))}
      </View>
      <BottomSheet visible={!!active} title={active?.confirmTitle ?? ''} onClose={close} closeLabel={t('common.close')}>
        {error ? <View className="mb-3"><Alert tone="danger">{error}</Alert></View> : null}
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
