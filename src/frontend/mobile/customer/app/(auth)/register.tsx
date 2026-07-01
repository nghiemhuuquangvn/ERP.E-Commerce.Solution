import { useState } from 'react'
import { View, Text, TextInput, TouchableOpacity, Alert, ScrollView } from 'react-native'
import { Link } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'

export default function RegisterScreen() {
  const { t } = useTranslation()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const { setAuth } = useAuthStore()

  async function register() {
    if (!firstName || !email || !password) return
    setLoading(true)
    try {
      const { data } = await apiClient.post('/v1/auth/register', { firstName, lastName, email, password })
      setAuth(data.user, data.accessToken)
    } catch {
      Alert.alert(t('common.error'), t('auth.registerError'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <ScrollView contentContainerClassName="flex-grow items-center justify-center px-6 py-12 bg-white">
      <View className="w-full max-w-sm">
        <Text className="text-3xl font-bold text-brand-500 mb-1">{t('auth.createAccount')}</Text>
        <Text className="text-gray-500 mb-8">{t('auth.createAccountSubtitle')}</Text>

        <View className="flex-row gap-3 mb-3">
          <TextInput className="flex-1 border border-gray-200 rounded-xl px-4 py-3 bg-gray-50" placeholder={t('auth.firstNamePlaceholder')} value={firstName} onChangeText={setFirstName} />
          <TextInput className="flex-1 border border-gray-200 rounded-xl px-4 py-3 bg-gray-50" placeholder={t('auth.lastNamePlaceholder')} value={lastName} onChangeText={setLastName} />
        </View>
        <TextInput className="border border-gray-200 rounded-xl px-4 py-3 mb-3 bg-gray-50" placeholder={t('auth.emailPlaceholder')} value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" />
        <TextInput className="border border-gray-200 rounded-xl px-4 py-3 mb-6 bg-gray-50" placeholder={t('auth.passwordHintPlaceholder')} value={password} onChangeText={setPassword} secureTextEntry />

        <TouchableOpacity onPress={register} disabled={loading} className="bg-brand-500 rounded-xl py-4 items-center mb-4">
          <Text className="text-white font-semibold text-base">{loading ? t('auth.creating') : t('auth.createAccount')}</Text>
        </TouchableOpacity>
        <Link href="/(auth)/login" className="text-center text-sm text-brand-500">
          {t('auth.haveAccount')}
        </Link>
      </View>
    </ScrollView>
  )
}
