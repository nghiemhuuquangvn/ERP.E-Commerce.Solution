import { useState } from 'react'
import { View, Text, TextInput, TouchableOpacity, Alert, KeyboardAvoidingView, Platform } from 'react-native'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'

export default function LoginScreen() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const { setAuth } = useAuthStore()

  async function login() {
    if (!email || !password) return
    setLoading(true)
    try {
      const { data } = await apiClient.post('/v1/auth/login', { email, password })
      setAuth(data.user, data.accessToken)
    } catch {
      Alert.alert('Login Failed', 'Invalid email or password.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      className="flex-1 bg-gray-50 items-center justify-center px-6"
    >
      <View className="w-full max-w-sm">
        <Text className="text-3xl font-bold text-primary-600 mb-1">ERP Staff</Text>
        <Text className="text-gray-500 mb-8">Sign in to continue</Text>

        <TextInput
          className="bg-white border border-gray-300 rounded-xl px-4 py-3 mb-3 text-gray-900"
          placeholder="Email"
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          keyboardType="email-address"
        />
        <TextInput
          className="bg-white border border-gray-300 rounded-xl px-4 py-3 mb-6 text-gray-900"
          placeholder="Password"
          value={password}
          onChangeText={setPassword}
          secureTextEntry
        />
        <TouchableOpacity
          onPress={login}
          disabled={loading}
          className="bg-primary-600 rounded-xl py-3 items-center"
        >
          <Text className="text-white font-semibold">
            {loading ? 'Signing in…' : 'Sign In'}
          </Text>
        </TouchableOpacity>
      </View>
    </KeyboardAvoidingView>
  )
}
