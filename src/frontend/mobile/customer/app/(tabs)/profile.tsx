import { View, Text, TouchableOpacity, Alert } from 'react-native'
import { useRouter } from 'expo-router'
import { useAuthStore } from '@/stores/auth.store'

export default function ProfileScreen() {
  const router = useRouter()
  const { user, isAuthenticated, logout } = useAuthStore()

  function handleLogout() {
    Alert.alert('Sign Out', 'Are you sure you want to sign out?', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Sign Out',
        style: 'destructive',
        onPress: () => {
          logout()
          router.replace('/(auth)/login')
        },
      },
    ])
  }

  if (!isAuthenticated) {
    return (
      <View className="flex-1 bg-white items-center justify-center px-6">
        <Text className="text-lg font-semibold text-gray-700 mb-2">Not signed in</Text>
        <TouchableOpacity
          onPress={() => router.push('/(auth)/login')}
          className="bg-brand-500 rounded-xl px-6 py-3"
        >
          <Text className="text-white font-semibold">Sign In</Text>
        </TouchableOpacity>
      </View>
    )
  }

  return (
    <View className="flex-1 bg-gray-50">
      <View className="bg-white p-6 border-b border-gray-100">
        <View className="w-16 h-16 rounded-full bg-brand-100 items-center justify-center mb-3">
          <Text className="text-3xl">👤</Text>
        </View>
        <Text className="text-xl font-bold text-gray-900">
          {user?.firstName} {user?.lastName}
        </Text>
        <Text className="text-gray-500 text-sm">{user?.email}</Text>
      </View>

      <View className="mt-4 mx-4 bg-white rounded-xl overflow-hidden border border-gray-100">
        <MenuItem label="My Orders" icon="📋" onPress={() => router.push('/(tabs)/orders')} />
        <MenuItem label="Settings" icon="⚙️" onPress={() => {}} />
        <MenuItem label="Help & Support" icon="💬" onPress={() => {}} />
      </View>

      <TouchableOpacity
        onPress={handleLogout}
        className="mx-4 mt-4 bg-white rounded-xl border border-red-100 p-4 items-center"
      >
        <Text className="text-red-500 font-semibold">Sign Out</Text>
      </TouchableOpacity>
    </View>
  )
}

function MenuItem({ label, icon, onPress }: { label: string; icon: string; onPress: () => void }) {
  return (
    <TouchableOpacity
      onPress={onPress}
      className="flex-row items-center px-4 py-4 border-b border-gray-50"
    >
      <Text className="text-lg mr-3">{icon}</Text>
      <Text className="flex-1 text-gray-800">{label}</Text>
      <Text className="text-gray-400">›</Text>
    </TouchableOpacity>
  )
}
