import { useState } from 'react'
import { Text, View } from 'react-native'
import { Link } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'
import { Alert, Button, Screen, TextField } from '@/components/ui'

export default function LoginScreen() {
  const { t } = useTranslation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [failed, setFailed] = useState(false)
  const [tried, setTried] = useState(false)
  const { setAuth } = useAuthStore()

  async function login() {
    setTried(true)
    if (!email || !password) return
    setLoading(true)
    setFailed(false)
    try {
      const { data } = await apiClient.post('/v1/auth/login', { email, password })
      setAuth(data.user, data.accessToken)
    } catch {
      setFailed(true)
    } finally {
      setLoading(false)
    }
  }

  return (
    <Screen center edges={['top', 'bottom', 'left', 'right']}>
      <View className="w-full max-w-sm self-center">
        <Text accessibilityRole="header" className="mb-1 text-4xl font-bold text-brand-action">{t('auth.appName')}</Text>
        <Text className="mb-8 text-ink-secondary">{t('auth.signInSubtitle')}</Text>

        {failed && <View className="mb-4"><Alert tone="danger">{t('auth.signInError')}</Alert></View>}

        <TextField
          label={t('auth.emailLabel')}
          placeholder={t('auth.emailPlaceholder')}
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          autoComplete="email"
          textContentType="emailAddress"
          keyboardType="email-address"
          returnKeyType="next"
          required
          error={tried && !email ? t('auth.required') : undefined}
        />
        <TextField
          label={t('auth.passwordLabel')}
          placeholder={t('auth.passwordPlaceholder')}
          value={password}
          onChangeText={setPassword}
          secureTextEntry
          autoComplete="current-password"
          textContentType="password"
          returnKeyType="go"
          onSubmitEditing={login}
          required
          error={tried && !password ? t('auth.required') : undefined}
        />
        <View className="mb-4 mt-3">
          <Button onPress={login} loading={loading}>{loading ? t('auth.signingIn') : t('common.signIn')}</Button>
        </View>
        <Link href="/(auth)/register" accessibilityRole="link" className="min-h-[44px] py-3 text-center text-sm text-brand-action">
          {t('auth.noAccount')}
        </Link>
      </View>
    </Screen>
  )
}
