import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useVerifyTwoFactor, useResendTwoFactor } from '@/hooks/useAuth'

const RESEND_COOLDOWN_SECONDS = 60

export function TwoFactorPage() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()
  const [challengeId, setChallengeId] = useState(searchParams.get('challengeId') ?? '')
  const [method, setMethod] = useState(searchParams.get('method') ?? '')
  const [code, setCode] = useState('')
  const [cooldownUntil, setCooldownUntil] = useState<number | null>(null)
  const [now, setNow] = useState(Date.now())

  const verify = useVerifyTwoFactor()
  const resend = useResendTwoFactor()

  const cooldownRemaining = cooldownUntil ? Math.max(0, Math.ceil((cooldownUntil - now) / 1000)) : 0

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    verify.mutate({ challengeId, code })
  }

  const handleResend = () => {
    resend.mutate(
      { challengeId },
      {
        onSuccess: (data) => {
          if (data.challengeId) setChallengeId(data.challengeId)
          if (data.twoFactorMethod) setMethod(data.twoFactorMethod)
          const until = Date.now() + RESEND_COOLDOWN_SECONDS * 1000
          setCooldownUntil(until)
          // Tick every second so the button re-enables itself without a reload.
          const interval = setInterval(() => {
            setNow(Date.now())
            if (Date.now() >= until) clearInterval(interval)
          }, 1000)
        },
      },
    )
  }

  const verifyErrors = verify.data && !verify.data.succeeded ? verify.data.errors : null
  const resendErrors = resend.data && !resend.data.succeeded ? resend.data.errors : null

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="card w-full max-w-sm p-8">
        <div className="mb-8 text-center">
          <h1 className="text-2xl font-bold text-primary-600">{t('auth.twoFactor.title')}</h1>
          <p className="mt-1 text-sm text-gray-500">
            {method === 'Sms' ? t('auth.twoFactor.subtitleSms') : t('auth.twoFactor.subtitleEmail')}
          </p>
        </div>

        {(verify.isError || verifyErrors) && (
          <div className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
            {verifyErrors?.join(', ') || t('auth.twoFactor.verifyFailed')}
          </div>
        )}

        {(resend.isError || resendErrors) && (
          <div className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
            {resendErrors?.join(', ') || t('auth.twoFactor.resendFailed')}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('auth.twoFactor.code')}
            </label>
            <input
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              maxLength={6}
              value={code}
              onChange={(e) => setCode(e.target.value)}
              className="input tracking-widest text-center text-lg"
              placeholder="000000"
              autoFocus
              required
            />
          </div>
          <button type="submit" disabled={verify.isPending} className="btn-primary w-full">
            {verify.isPending ? t('auth.twoFactor.verifying') : t('auth.twoFactor.verify')}
          </button>
        </form>

        <button
          type="button"
          onClick={handleResend}
          disabled={resend.isPending || cooldownRemaining > 0}
          className="btn-outline mt-4 w-full"
        >
          {cooldownRemaining > 0
            ? t('auth.twoFactor.resendCooldown', { seconds: cooldownRemaining })
            : t('auth.twoFactor.resend')}
        </button>
      </div>
    </div>
  )
}
