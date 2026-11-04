import { useEffect, useState } from 'react'
import { Text, View } from 'react-native'
import { useTranslation } from 'react-i18next'
import { authApi, type LoginResponse } from '@/api/auth'
import { isNetworkError } from '@/api/client'
import { Alert, Button, Screen, TextField } from '@/components/ui'
import { TenantChip } from '@/components/TenantChip'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

const RESEND_SECONDS = 30

export default function LoginScreen() {
  const { t } = useTranslation()
  const signIn = useAuthStore((s) => s.signIn)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')
  const [challenge, setChallenge] = useState<{ id: string; method?: string } | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [tried, setTried] = useState(false)
  const [wait, setWait] = useState(0)

  useEffect(() => {
    if (wait <= 0) return
    const timer = setTimeout(() => setWait((w) => w - 1), 1000)
    return () => clearTimeout(timer)
  }, [wait])

  /** A finished answer signs in; a two-step answer opens the code step; anything else is a refusal. */
  async function handle(run: () => Promise<LoginResponse>, refused: string) {
    setLoading(true)
    setError(null)
    try {
      const r = await run()
      if (r.requiresTwoFactor && r.challengeId) {
        setChallenge({ id: r.challengeId, method: r.twoFactorMethod })
        setCode('')
        setTried(false)
        setWait(RESEND_SECONDS)
      } else if (r.accessToken && r.refreshToken) {
        await signIn(r)
      } else {
        setError(r.errors?.[0] ?? refused)
      }
    } catch (e) {
      // A 401 only says "Unauthorized.": the screen's own sentence is clearer; other refusals (locked account) keep the server's words.
      const unauthorized = (e as { response?: { status?: number } })?.response?.status === 401
      setError(isNetworkError(e) ? t('auth.networkError') : unauthorized ? refused : serverMessage(e, refused))
    } finally {
      setLoading(false)
    }
  }

  function submitCredentials() {
    setTried(true)
    if (!email || !password) return
    void handle(() => authApi.login(email.trim(), password), t('auth.signInError'))
  }

  function submitCode() {
    setTried(true)
    if (!challenge || code.length < 6) return
    void handle(() => authApi.verifyTwoFactor(challenge.id, code), t('auth.codeError'))
  }

  async function resend() {
    if (!challenge || wait > 0) return
    setError(null)
    try {
      await authApi.resendTwoFactor(challenge.id)
      setWait(RESEND_SECONDS)
    } catch (e) {
      setError(isNetworkError(e) ? t('auth.networkError') : serverMessage(e, t('auth.resendError')))
    }
  }

  return (
    <Screen center edges={['top', 'bottom', 'left', 'right']}>
      <View className="w-full max-w-sm self-center">
        <Text accessibilityRole="header" className="mb-1 text-3xl font-bold text-brand-action">{t('auth.appName')}</Text>
        <View className="mb-6 flex-row items-center gap-2">
          <Text className="text-ink-secondary">{challenge ? t('auth.twoFactorTitle') : t('auth.signInSubtitle')}</Text>
          <TenantChip />
        </View>

        {error ? <View className="mb-4"><Alert tone="danger">{error}</Alert></View> : null}

        {challenge ? (
          <>
            <Text className="mb-3 text-ink-secondary">{t('auth.twoFactorHint', { method: challenge.method ?? '' })}</Text>
            <TextField
              label={t('auth.codeLabel')}
              value={code}
              onChangeText={(v) => setCode(v.replace(/\D/g, ''))}
              keyboardType="number-pad"
              textContentType="oneTimeCode"
              autoComplete="one-time-code"
              maxLength={6}
              returnKeyType="go"
              onSubmitEditing={submitCode}
              required
              error={tried && code.length < 6 ? t('auth.codeRequired') : undefined}
            />
            <View className="mt-3 gap-2">
              <Button size="lg" onPress={submitCode} loading={loading}>{t('auth.verify')}</Button>
              <Button variant="ghost" onPress={resend} disabled={wait > 0}>{wait > 0 ? t('auth.resendIn', { seconds: wait }) : t('auth.resend')}</Button>
              <Button variant="ghost" onPress={() => { setChallenge(null); setError(null) }}>{t('common.back')}</Button>
            </View>
          </>
        ) : (
          <>
            <TextField
              label={t('auth.emailLabel')}
              placeholder={t('auth.emailPlaceholder')}
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              autoComplete="email"
              textContentType="username"
              keyboardType="email-address"
              returnKeyType="next"
              required
              error={tried && !email ? t('auth.required') : undefined}
            />
            <TextField
              label={t('auth.passwordLabel')}
              value={password}
              onChangeText={setPassword}
              secureTextEntry
              autoComplete="current-password"
              textContentType="password"
              returnKeyType="go"
              onSubmitEditing={submitCredentials}
              required
              error={tried && !password ? t('auth.required') : undefined}
            />
            <View className="mt-3">
              <Button size="lg" onPress={submitCredentials} loading={loading}>{loading ? t('auth.signingIn') : t('auth.signIn')}</Button>
            </View>
          </>
        )}
      </View>
    </Screen>
  )
}
