import { View, Text, TouchableOpacity, Alert } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'

export default function ProfileScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const { user, isAuthenticated, logout } = useAuthStore()
  const { locale, setLocale } = useLocaleStore()

  function handleLogout() {
    Alert.alert(t('profile.signOutConfirmTitle'), t('profile.signOutConfirmMessage'), [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('common.signOut'),
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
        <Text className="text-lg font-semibold text-gray-700 mb-2">{t('profile.notSignedIn')}</Text>
        <TouchableOpacity
          onPress={() => router.push('/(auth)/login')}
          className="bg-brand-500 rounded-xl px-6 py-3"
        >
          <Text className="text-white font-semibold">{t('common.signIn')}</Text>
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
        <MenuItem label={t('profile.myOrders')} icon="📋" onPress={() => router.push('/(tabs)/orders')} />
        <MenuItem label={t('profile.settings')} icon="⚙️" onPress={() => {}} />
        <MenuItem label={t('profile.helpAndSupport')} icon="💬" onPress={() => {}} />
      </View>

      <View className="mt-4 mx-4 bg-white rounded-xl border border-gray-100 p-4">
        <Text className="text-sm font-semibold text-gray-700 mb-3">{t('common.language')}</Text>
        <View className="flex-row gap-2">
          <TouchableOpacity
            onPress={() => setLocale('en')}
            className={`flex-1 rounded-lg py-2.5 items-center border ${
              locale === 'en' ? 'bg-brand-500 border-brand-500' : 'bg-white border-gray-300'
            }`}
          >
            <Text className={locale === 'en' ? 'text-white font-medium' : 'text-gray-700 font-medium'}>
              {t('common.english')}
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            onPress={() => setLocale('vi')}
            className={`flex-1 rounded-lg py-2.5 items-center border ${
              locale === 'vi' ? 'bg-brand-500 border-brand-500' : 'bg-white border-gray-300'
            }`}
          >
            <Text className={locale === 'vi' ? 'text-white font-medium' : 'text-gray-700 font-medium'}>
              {t('common.vietnamese')}
            </Text>
          </TouchableOpacity>
        </View>
      </View>

      <TouchableOpacity
        onPress={handleLogout}
        className="mx-4 mt-4 bg-white rounded-xl border border-red-100 p-4 items-center"
      >
        <Text className="text-red-500 font-semibold">{t('common.signOut')}</Text>
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
