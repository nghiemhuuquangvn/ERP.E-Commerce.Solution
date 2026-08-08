import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { GoogleOAuthProvider, GoogleLogin, type CredentialResponse } from '@react-oauth/google'
import { jwtDecode } from 'jwt-decode'
import { useLogin, useCompleteLogin } from '@/hooks/useAuth'
import { authApi, ADMIN_USER_TYPE } from '@/api/auth.api'
import { getMsalInstance } from '@/lib/msalConfig'
import { loadFacebookSdk } from '@/lib/facebookSdk'

const GOOGLE_CLIENT_ID = (import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined) ?? ''

interface GoogleIdTokenClaims {
  sub: string
}

function GoogleLoginButton({ onError }: { onError: (message: string) => void }) {
  const { t } = useTranslation()
  const completeLogin = useCompleteLogin()

  const handleSuccess = async (credentialResponse: CredentialResponse) => {
    const idToken = credentialResponse.credential
    if (!idToken) {
      onError(t('auth.externalLogin.failed'))
      return
    }
    try {
      const claims = jwtDecode<GoogleIdTokenClaims>(idToken)
      const data = await authApi.externalLogin({
        provider: 'Google',
        providerKey: claims.sub,
        idToken,
        userType: ADMIN_USER_TYPE,
      })
      if (!data.succeeded) {
        onError(data.errors?.join(', ') || t('auth.externalLogin.failed'))
        return
      }
      completeLogin(data)
    } catch {
      onError(t('auth.externalLogin.failed'))
    }
  }

  return (
    <div className="flex justify-center">
      <GoogleLogin onSuccess={handleSuccess} onError={() => onError(t('auth.externalLogin.failed'))} />
    </div>
  )
}

function MicrosoftLoginButton({ onError }: { onError: (message: string) => void }) {
  const { t } = useTranslation()
  const completeLogin = useCompleteLogin()

  const handleClick = async () => {
    try {
      const result = await getMsalInstance().loginPopup({ scopes: ['openid', 'profile', 'email'] })
      if (!result.idToken) {
        onError(t('auth.externalLogin.failed'))
        return
      }
      const claims = jwtDecode<{ oid?: string }>(result.idToken)
      const data = await authApi.externalLogin({
        provider: 'Microsoft',
        providerKey: claims.oid ?? result.uniqueId,
        idToken: result.idToken,
        userType: ADMIN_USER_TYPE,
      })
      if (!data.succeeded) {
        onError(data.errors?.join(', ') || t('auth.externalLogin.failed'))
        return
      }
      completeLogin(data)
    } catch {
      onError(t('auth.externalLogin.failed'))
    }
  }

  return (
    <button type="button" className="btn-outline w-full" onClick={handleClick}>
      {t('auth.externalLogin.continueWithMicrosoft')}
    </button>
  )
}

function FacebookLoginButton({ onError }: { onError: (message: string) => void }) {
  const { t } = useTranslation()
  const completeLogin = useCompleteLogin()

  const handleClick = async () => {
    try {
      await loadFacebookSdk()
      window.FB?.login(
        async (response) => {
          if (!response.authResponse) {
            onError(t('auth.externalLogin.failed'))
            return
          }
          try {
            const data = await authApi.externalLogin({
              provider: 'Facebook',
              providerKey: response.authResponse.userID,
              idToken: response.authResponse.accessToken,
              userType: ADMIN_USER_TYPE,
            })
            if (!data.succeeded) {
              onError(data.errors?.join(', ') || t('auth.externalLogin.failed'))
              return
            }
            completeLogin(data)
          } catch {
            onError(t('auth.externalLogin.failed'))
          }
        },
        { scope: 'public_profile,email' },
      )
    } catch {
      onError(t('auth.externalLogin.failed'))
    }
  }

  return (
    <button type="button" className="btn-outline w-full" onClick={handleClick}>
      {t('auth.externalLogin.continueWithFacebook')}
    </button>
  )
}

function TwitterLoginButton({ onError }: { onError: (message: string) => void }) {
  const { t } = useTranslation()

  const handleClick = async () => {
    try {
      const result = await authApi.getTwitterAuthorizeUrl()
      window.location.href = result.authorizeUrl
    } catch {
      onError(t('auth.externalLogin.failed'))
    }
  }

  return (
    <button type="button" className="btn-outline w-full" onClick={handleClick}>
      {t('auth.externalLogin.continueWithTwitter')}
    </button>
  )
}

function LoginForm() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [ssoError, setSsoError] = useState<string | null>(null)
  const login = useLogin()

  const loginOptions = useQuery({
    queryKey: ['auth', 'login-options'],
    queryFn: () => authApi.getLoginOptions(),
    retry: false,
    staleTime: 30_000,
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setSsoError(null)
    login.mutate({ email, password, userType: ADMIN_USER_TYPE })
  }

  const options = loginOptions.data
  const showSso =
    !!options &&
    (options.googleEnabled || options.microsoftEnabled || options.facebookEnabled || options.twitterEnabled)

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="card w-full max-w-sm p-8">
        <div className="mb-8 text-center">
          <h1 className="text-2xl font-bold text-primary-600">{t('auth.title')}</h1>
          <p className="mt-1 text-sm text-gray-500">{t('auth.subtitle')}</p>
        </div>

        {login.error && (
          <div className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
            {t('auth.invalidCredentials')}
          </div>
        )}
        {ssoError && (
          <div className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">{ssoError}</div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('auth.email')}</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="input"
              placeholder="admin@example.com"
              required
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('auth.password')}</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="input"
              placeholder="••••••••"
              required
            />
          </div>
          <button type="submit" disabled={login.isPending} className="btn-primary w-full">
            {login.isPending ? t('auth.signingIn') : t('auth.signIn')}
          </button>
        </form>

        {options?.twoFactorEnabled && (
          <p className="mt-3 text-center text-xs text-gray-400">{t('auth.twoFactor.enabledNotice')}</p>
        )}

        {showSso && (
          <div className="mt-6 space-y-2 border-t border-gray-100 pt-6">
            {options?.googleEnabled && <GoogleLoginButton onError={setSsoError} />}
            {options?.microsoftEnabled && <MicrosoftLoginButton onError={setSsoError} />}
            {options?.facebookEnabled && <FacebookLoginButton onError={setSsoError} />}
            {options?.twitterEnabled && <TwitterLoginButton onError={setSsoError} />}
          </div>
        )}
      </div>
    </div>
  )
}

export function LoginPage() {
  return (
    <GoogleOAuthProvider clientId={GOOGLE_CLIENT_ID}>
      <LoginForm />
    </GoogleOAuthProvider>
  )
}
