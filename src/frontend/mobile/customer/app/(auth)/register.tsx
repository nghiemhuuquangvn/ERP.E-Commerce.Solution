import { useState } from 'react'
import { View, Text, TextInput, TouchableOpacity, Alert, ScrollView } from 'react-native'
import { Link } from 'expo-router'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'

export default function RegisterScreen() {
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
      Alert.alert('Error', 'Registration failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <ScrollView contentContainerClassName="flex-grow items-center justify-center px-6 py-12 bg-white">
      <View className="w-full max-w-sm">
        <Text className="text-3xl font-bold text-brand-500 mb-1">Create Account</Text>
        <Text className="text-gray-500 mb-8">Join ERP Shop today</Text>

        <View className="flex-row gap-3 mb-3">
          <TextInput className="flex-1 border border-gray-200 rounded-xl px-4 py-3 bg-gray-50" placeholder="First Name" value={firstName} onChangeText={setFirstName} />
          <TextInput className="flex-1 border border-gray-200 rounded-xl px-4 py-3 bg-gray-50" placeholder="Last Name" value={lastName} onChangeText={setLastName} />
        </View>
        <TextInput className="border border-gray-200 rounded-xl px-4 py-3 mb-3 bg-gray-50" placeholder="Email" value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" />
        <TextInput className="border border-gray-200 rounded-xl px-4 py-3 mb-6 bg-gray-50" placeholder="Password (min 8 chars)" value={password} onChangeText={setPassword} secureTextEntry />

        <TouchableOpacity onPress={register} disabled={loading} className="bg-brand-500 rounded-xl py-4 items-center mb-4">
          <Text className="text-white font-semibold text-base">{loading ? 'Creating…' : 'Create Account'}</Text>
        </TouchableOpacity>
        <Link href="/(auth)/login" className="text-center text-sm text-brand-500">
          Already have an account? Sign in →
        </Link>
      </View>
    </ScrollView>
  )
}
