import { useState } from 'react'
import { View, Text, TextInput, TouchableOpacity, Alert, KeyboardAvoidingView, Platform } from 'react-native'
import { Link } from 'expo-router'
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
      Alert.alert('Error', 'Invalid email or password.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      className="flex-1 bg-white items-center justify-center px-6"
    >
      <View className="w-full max-w-sm">
        <Text className="text-4xl font-bold text-brand-500 mb-1">ERP Shop</Text>
        <Text className="text-gray-500 mb-8">Sign in to your account</Text>

        <TextInput
          className="border border-gray-200 rounded-xl px-4 py-3 mb-3 bg-gray-50"
          placeholder="Email"
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          keyboardType="email-address"
        />
        <TextInput
          className="border border-gray-200 rounded-xl px-4 py-3 mb-6 bg-gray-50"
          placeholder="Password"
          value={password}
          onChangeText={setPassword}
          secureTextEntry
        />
        <TouchableOpacity
          onPress={login}
          disabled={loading}
          className="bg-brand-500 rounded-xl py-4 items-center mb-4"
        >
          <Text className="text-white font-semibold text-base">
            {loading ? 'Signing in…' : 'Sign In'}
          </Text>
        </TouchableOpacity>
        <Link href="/(auth)/register" className="text-center text-sm text-brand-500">
          Don't have an account? Register →
        </Link>
      </View>
    </KeyboardAvoidingView>
  )
}
