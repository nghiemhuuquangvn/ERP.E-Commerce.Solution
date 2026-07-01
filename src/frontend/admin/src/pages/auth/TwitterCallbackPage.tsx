import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
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
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="card w-full max-w-sm p-8 text-center">
        {error ? (
          <>
            <div className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</div>
            <button className="btn-primary w-full" onClick={() => navigate('/login')}>
              {t('auth.signIn')}
            </button>
          </>
        ) : (
          <p className="text-sm text-gray-500">{t('auth.externalLogin.completingSignIn')}</p>
        )}
      </div>
    </div>
  )
}
