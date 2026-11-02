import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, FormField, Input, Modal } from '@/components/ui'

interface PayDialogProps {
  open: boolean
  title: string
  message: string
  /** The document's foreign currency; asks for the rate the payment was made at (required, above 0). */
  currency?: string | null
  defaultRate?: number | null
  busy?: boolean
  onConfirm: (paymentRate?: number) => void
  onCancel: () => void
}

/** Confirms marking an invoice / bill paid (it books the receipt / payment); a foreign-currency document also needs the payment rate. */
export function PayDialog({ open, title, message, currency, defaultRate, busy = false, onConfirm, onCancel }: PayDialogProps) {
  const { t } = useTranslation()
  const [rate, setRate] = useState('')
  const [invalid, setInvalid] = useState(false)

  useEffect(() => {
    if (open) {
      setRate(defaultRate ? String(defaultRate) : '')
      setInvalid(false)
    }
  }, [open, defaultRate])

  const submit = () => {
    if (!currency) return onConfirm()
    if (!(Number(rate) > 0)) return setInvalid(true)
    onConfirm(Number(rate))
  }

  return (
    <Modal
      open={open}
      title={title}
      onClose={onCancel}
      size="sm"
      closeOnEsc={!busy}
      footer={
        <>
          <Button variant="outline" onClick={onCancel}>
            {t('common.cancel')}
          </Button>
          <Button loading={busy} onClick={submit}>
            {t('accounting.markPaidAction')}
          </Button>
        </>
      }
    >
      <div className="space-y-3">
        <p className="text-sm text-ink-secondary">{message}</p>
        {currency && (
          <FormField label={t('accounting.fx.paymentRate', { currency })} required error={invalid ? t('accounting.fx.paymentRateRequired') : undefined}>
            <Input
              type="number"
              min="0"
              step="0.0001"
              value={rate}
              onChange={(e) => {
                setRate(e.target.value)
                setInvalid(false)
              }}
            />
          </FormField>
        )}
      </div>
    </Modal>
  )
}
