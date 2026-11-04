import { useState } from 'react'
import { Pressable, Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import Constants from 'expo-constants'
import { Alert, BottomSheet, Button, Card, Screen } from '@/components/ui'
import { DetailRows } from '@/components/DetailRows'
import { tenantConfig } from '@/lib/tenant'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore, type Locale } from '@/stores/locale.store'
import { useVisibleModules } from '@/modules/visible'
import { colors } from '@/theme/theme'

/** Profile, the modules that did not fit in the tab bar, language and sign out. */
export default function MoreScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const { locale, setLocale } = useLocaleStore()
  const { overflow } = useVisibleModules()
  const [confirm, setConfirm] = useState(false)

  return (
    <Screen>
      <View className="gap-3">
        <Card>
          <Text accessibilityRole="header" className="text-lg font-semibold text-ink">{user?.name}</Text>
          <Text className="text-sm text-ink-secondary">{user?.email}</Text>
        </Card>

        {overflow.length > 0 ? (
          <Card>
            {overflow.map((m) => (
              <Pressable
                key={m.id}
                onPress={() => router.push(`/(tabs)/${m.route}` as never)}
                accessibilityRole="button"
                accessibilityLabel={t(m.titleKey)}
                className="min-h-[48px] flex-row items-center gap-3"
              >
                <m.icon color={colors.inkSecondary} size={22} />
                <Text className="flex-1 text-base text-ink">{t(m.titleKey)}</Text>
                <Text className="text-ink-muted">›</Text>
              </Pressable>
            ))}
          </Card>
        ) : null}

        <Card>
          <Text accessibilityRole="header" className="mb-2 font-semibold text-ink">{t('common.language')}</Text>
          <View className="flex-row gap-2">
            {(['en', 'vi'] as Locale[]).map((l) => (
              <View key={l} className="flex-1">
                <Button variant={locale === l ? 'primary' : 'outline'} onPress={() => setLocale(l)} accessibilityLabel={l === 'en' ? t('common.english') : t('common.vietnamese')}>
                  {l === 'en' ? t('common.english') : t('common.vietnamese')}
                </Button>
              </View>
            ))}
          </View>
        </Card>

        <Card>
          <DetailRows
            rows={[
              { label: t('more.tenant'), value: tenantConfig.code },
              { label: t('more.server'), value: tenantConfig.apiUrl },
              { label: t('more.version'), value: Constants.expoConfig?.version ?? '' },
            ]}
          />
        </Card>

        <Button variant="outline" onPress={() => setConfirm(true)}>{t('more.signOut')}</Button>
      </View>

      <BottomSheet visible={confirm} title={t('more.signOutTitle')} onClose={() => setConfirm(false)} closeLabel={t('common.close')}>
        <View className="gap-3">
          <Alert tone="info">{t('more.signOutHint')}</Alert>
          <View className="flex-row gap-2">
            <View className="flex-1"><Button variant="outline" onPress={() => setConfirm(false)}>{t('common.back')}</Button></View>
            <View className="flex-1"><Button variant="danger" onPress={() => { setConfirm(false); void logout() }}>{t('more.signOut')}</Button></View>
          </View>
        </View>
      </BottomSheet>
    </Screen>
  )
}
