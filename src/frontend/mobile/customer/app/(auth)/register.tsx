import { useState } from 'react'
import { Text, View } from 'react-native'
import { Link } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { apiClient, isNetworkError } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'
import { Alert, Button, Screen, TextField } from '@/components/ui'

export default function RegisterScreen() {
  const { t } = useTranslation()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [failed, setFailed] = useState(false)
  const [offline, setOffline] = useState(false)
  const [tried, setTried] = useState(false)
  const { setAuth } = useAuthStore()

  async function register() {
    setTried(true)
    if (!firstName || !email || !password) return
    setLoading(true)
    setFailed(false)
    setOffline(false)
    try {
      const { data } = await apiClient.post('/v1/auth/register', { firstName, lastName, email, password })
      setAuth(data.user, data.accessToken)
    } catch (e) {
      setOffline(isNetworkError(e))
      setFailed(true)
    } finally {
      setLoading(false)
    }
  }

  const missing = (value: string) => (tried && !value ? t('auth.required') : undefined)

  return (
    <Screen center edges={['top', 'bottom', 'left', 'right']}>
      <View className="w-full max-w-sm self-center">
        <Text accessibilityRole="header" className="mb-1 text-3xl font-bold text-brand-action">{t('auth.createAccount')}</Text>
        <Text className="mb-8 text-ink-secondary">{t('auth.createAccountSubtitle')}</Text>

        {failed && <View className="mb-4"><Alert tone="danger">{offline ? t('auth.networkError') : t('auth.registerError')}</Alert></View>}

        <TextField label={t('auth.firstNameLabel')} placeholder={t('auth.firstNamePlaceholder')} value={firstName} onChangeText={setFirstName} autoComplete="given-name" textContentType="givenName" returnKeyType="next" required error={missing(firstName)} />
        <TextField label={t('auth.lastNameLabel')} placeholder={t('auth.lastNamePlaceholder')} value={lastName} onChangeText={setLastName} autoComplete="family-name" textContentType="familyName" returnKeyType="next" />
        <TextField label={t('auth.emailLabel')} placeholder={t('auth.emailPlaceholder')} value={email} onChangeText={setEmail} autoCapitalize="none" autoComplete="email" textContentType="emailAddress" keyboardType="email-address" returnKeyType="next" required error={missing(email)} />
        <TextField label={t('auth.passwordLabel')} placeholder={t('auth.passwordHintPlaceholder')} hint={t('auth.passwordHint')} value={password} onChangeText={setPassword} secureTextEntry autoComplete="new-password" textContentType="newPassword" returnKeyType="go" onSubmitEditing={register} required error={missing(password)} />

        <View className="mb-4 mt-3">
          <Button onPress={register} loading={loading}>{loading ? t('auth.creating') : t('auth.createAccount')}</Button>
        </View>
        <Link href="/(auth)/login" accessibilityRole="link" className="min-h-[44px] py-3 text-center text-sm text-brand-action">
          {t('auth.haveAccount')}
        </Link>
      </View>
    </Screen>
  )
}
