import { View, Text, Pressable, Alert } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { ChevronRight, MessageCircle, Package, Settings, User, type LucideIcon } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'
import { Button, Card, EmptyState, Screen } from '@/components/ui'

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
      <Screen scroll={false}>
        <EmptyState
          title={t('profile.notSignedIn')}
          hint={t('profile.signInHint')}
          action={<Button onPress={() => router.push('/(auth)/login')}>{t('common.signIn')}</Button>}
        />
      </Screen>
    )
  }

  return (
    <Screen>
      <View className="-mx-4 -mt-4 mb-4 border-b border-line bg-surface p-6">
        <View className="mb-3 h-16 w-16 items-center justify-center rounded-full bg-brand-soft" accessibilityElementsHidden>
          <User size={32} strokeWidth={icon.stroke} color={colors.brandAction} />
        </View>
        <Text accessibilityRole="header" className="text-xl font-bold text-ink">{user?.fullName || user?.email}</Text>
        <Text className="text-sm text-ink-secondary">{user?.email}</Text>
      </View>

      <View className="overflow-hidden rounded-card border border-line bg-surface">
        <MenuItem label={t('profile.myOrders')} Icon={Package} onPress={() => router.push('/(tabs)/orders')} />
        <MenuItem label={t('profile.settings')} Icon={Settings} onPress={() => {}} />
        <MenuItem label={t('profile.helpAndSupport')} Icon={MessageCircle} onPress={() => {}} />
      </View>

      <View className="mt-4">
        <Card>
          <Text accessibilityRole="header" className="mb-3 text-sm font-semibold text-ink-secondary">{t('common.language')}</Text>
          <View accessibilityRole="radiogroup" accessibilityLabel={t('common.language')} className="flex-row gap-2">
            {(['en', 'vi'] as const).map((code) => {
              const selected = locale === code
              return (
                <Pressable
                  key={code}
                  onPress={() => setLocale(code)}
                  accessibilityRole="radio"
                  accessibilityState={{ selected, checked: selected }}
                  className={`min-h-[48px] flex-1 items-center justify-center rounded-control border ${selected ? 'border-brand-action bg-brand-action' : 'border-line-strong bg-surface'}`}
                >
                  <Text className={selected ? 'font-medium text-on-brand' : 'font-medium text-ink'}>
                    {code === 'en' ? t('common.english') : t('common.vietnamese')}
                  </Text>
                </Pressable>
              )
            })}
          </View>
        </Card>
      </View>

      <View className="mt-4">
        <Button variant="outline" onPress={handleLogout}>{t('common.signOut')}</Button>
      </View>
    </Screen>
  )
}

function MenuItem({ label, Icon, onPress }: { label: string; Icon: LucideIcon; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} accessibilityRole="button" className="min-h-[52px] flex-row items-center border-b border-line px-4">
      <View className="mr-3" accessibilityElementsHidden><Icon size={20} strokeWidth={icon.stroke} color={colors.inkSecondary} /></View>
      <Text className="flex-1 text-ink">{label}</Text>
      <ChevronRight size={20} strokeWidth={icon.stroke} color={colors.inkMuted} />
    </Pressable>
  )
}
