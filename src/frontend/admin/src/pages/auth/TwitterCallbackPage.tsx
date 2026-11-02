import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Spinner } from '@/components/ui'
import { authApi } from '@/api/auth.api'
import { useCompleteLogin } from '@/hooks/useAuth'

export function TwitterCallbackPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const completeLogin = useCompleteLogin()
  const [error, setError] = useState<string | null>(null)
  const ranOnce = useRef(false)

  useEffect(() => {
    if (ranOnce.current) return
    ranOnce.current = true

    const code = searchParams.get('code')
    const state = searchParams.get('state')
    if (!code || !state) {
      setError(t('auth.externalLogin.twitterCallbackMissingParams'))
      return
    }

    authApi
      .twitterCallback({ code, state })
      .then((data) => {
        if (data.requiresTwoFactor) {
          navigate(`/login/2fa?challengeId=${data.challengeId}&method=${data.twoFactorMethod}`)
          return
        }
        if (!data.succeeded) {
          setError(data.errors?.join(', ') || t('auth.externalLogin.failed'))
          return
        }
        completeLogin(data)
      })
      .catch(() => setError(t('auth.externalLogin.failed')))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="flex min-h-screen items-center justify-center bg-page">
      <div className="card w-full max-w-sm p-8 text-center">
        {error ? (
          <>
            <Alert variant="danger" className="mb-4 text-left">{error}</Alert>
            <Button fullWidth onClick={() => navigate('/login')}>
              {t('auth.signIn')}
            </Button>
          </>
        ) : (
          <p role="status" className="flex items-center justify-center gap-2 text-sm text-ink-muted">
            <Spinner />
            {t('auth.externalLogin.completingSignIn')}
          </p>
        )}
      </div>
    </div>
  )
}
